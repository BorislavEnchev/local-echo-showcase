# 06. Platform Abstraction Pattern

Handling fundamentally different platform APIs behind a unified interface using conditional compilation.

---

## The Challenge

Each platform uses a completely different audio capture API:

| Platform | Library | Microphone API | System Audio API |
|---|---|---|---|
| Windows | NAudio | `WaveInEvent` | `WasapiLoopbackCapture` |
| macOS | AVFoundation | `AVAudioRecorder` | N/A |
| Android | Android SDK | `AudioRecord` | `AudioPlaybackCapture` (API 29+) |

These APIs have no common base class and cannot be referenced across platforms.

---

## The Solution: Conditional Compilation

```csharp
using System;
using System.IO;
using System.Threading.Tasks;

#if WINDOWS
using NAudio.Wave;
#elif MACCATALYST
using AVFoundation;
using Foundation;
#elif ANDROID
using Android.Media;
using Android.Media.Projection;
#endif

public class AudioService : IAudioService
{
    private string _currentFilePath = string.Empty;
    private bool _isPaused;
    private AudioCaptureMode _captureMode = AudioCaptureMode.Microphone;

    // ── Platform-Specific State ──
#if WINDOWS
    private IWaveIn? _waveIn;
    private WaveFileWriter? _writer;
    private TaskCompletionSource<bool>? _stopTcs;
#elif MACCATALYST
    private AVAudioRecorder? _recorder;
#elif ANDROID
    private AudioRecord? _audioRecord;
    private MediaProjection? _mediaProjection;
#endif

    public async Task<AudioStartResult> StartRecordingAsync(
        bool systemAudio = false)
    {
#if WINDOWS
        _currentFilePath = Path.Combine(
            FileSystem.AppDataDirectory, "recording.wav");

        if (systemAudio)
        {
            _waveIn = new WasapiLoopbackCapture();
            _captureMode = AudioCaptureMode.System;
        }
        else
        {
            _waveIn = new WaveInEvent();
            _captureMode = AudioCaptureMode.Microphone;
        }

        _writer = new WaveFileWriter(
            _currentFilePath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += (s, e) =>
        {
            if (!_isPaused)
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        };
        _waveIn.StartRecording();
        return new AudioStartResult(_captureMode, false);

#elif MACCATALYST
        var url = NSUrl.FromFilename(_currentFilePath);
        var settings = new AVAudioRecorderSettings
        {
            AudioFormat = AudioToolbox.AudioFormatType.LinearPCM,
            SampleRate = 16000,
            NumberChannels = 1,
            LinearPcmBitDepth = 16
        };
        _recorder = AVAudioRecorder.Create(url, settings, out var error);
        _recorder.Record();
        return new AudioStartResult(_captureMode, false);

#elif ANDROID
        // Android system audio requires MediaProjection permission
        if (systemAudio)
        {
            var result = await TryStartAndroidSystemAudioAsync();
            if (result.IsSuccess) return result;

            // Fallback to microphone
            await StartAndroidMicrophoneAsync();
            return new AudioStartResult(_captureMode, true,
                "System audio unavailable. Switched to microphone.");
        }

        await StartAndroidMicrophoneAsync();
        return new AudioStartResult(_captureMode, false);
#endif
    }
}
```

---

## Pattern Breakdown

### 1. Platform-Specific Usings

```csharp
#if WINDOWS
using NAudio.Wave;
#elif MACCATALYST
using AVFoundation;
#elif ANDROID
using Android.Media;
#endif
```

Each platform's native library is only imported where it's available. This avoids compile errors on platforms that don't have the library.

### 2. Platform-Specific State

```csharp
#if WINDOWS
    private IWaveIn? _waveIn;
    private WaveFileWriter? _writer;
#elif MACCATALYST
    private AVAudioRecorder? _recorder;
#elif ANDROID
    private AudioRecord? _audioRecord;
#endif
```

Each platform tracks its own audio state objects. The compiler eliminates unused fields based on the active target framework.

### 3. Full Implementation Per Platform

```csharp
public async Task StartRecordingAsync(...)
{
#if WINDOWS
    // 60 lines of NAudio-specific code
#elif MACCATALYST
    // 20 lines of AVFoundation-specific code
#elif ANDROID
    // 100+ lines of Android-specific code (including MediaProjection flow)
#endif
}
```

Each `#if` block is a **complete implementation** for that platform. No shared abstractions at the API level — the interface is the abstraction.

---

## Android System Audio Complexity

Android's system audio capture is notably more complex than other platforms:

```csharp
private async Task<(bool IsSuccess, string? Message)>
    TryStartAndroidSystemAudioAsync()
{
    // 1. Check API level (requires Android 10+)
    if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        return (false, "System audio requires Android 10+");

    // 2. Request MediaProjection permission (shows screen-capture dialog)
    var permission = await MainActivity
        .RequestMediaProjectionPermissionAsync(activity);
    if (permission.Result != Result.Ok)
        return (false, "Permission not granted");

    // 3. Create MediaProjection session
    _mediaProjection = manager.GetMediaProjection(
        (int)permission.Result, permission.Data);

    // 4. Configure playback capture
    var captureConfig = new AudioPlaybackCaptureConfiguration
        .Builder(_mediaProjection)
        .AddMatchingUsage(AudioUsageKind.Media)
        .AddMatchingUsage(AudioUsageKind.Game)
        .Build();

    // 5. Create AudioRecord with capture config
    var record = new AudioRecord.Builder()
        .SetAudioFormat(audioFormat)
        .SetAudioPlaybackCaptureConfig(captureConfig)
        .Build();

    // 6. Start recording loop
    await StartAndroidRecordingLoopAsync(record);
}
```

This complexity is why the app gracefully falls back to microphone if system audio fails.

---

## WAV Header Construction (Android-Specific)

Android's `AudioRecord` outputs raw PCM bytes. We construct WAV headers manually:

```csharp
private static byte[] BuildWavHeader(
    long pcmBytes, int sampleRate, short channels, short bitsPerSample)
{
    int byteRate = sampleRate * channels * bitsPerSample / 8;
    short blockAlign = (short)(channels * bitsPerSample / 8);

    using var writer = new BinaryWriter(new MemoryStream(44));

    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + (int)pcmBytes);
    writer.Write(Encoding.ASCII.GetBytes("WAVE"));
    writer.Write(Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16);  // Subchunk1Size for PCM
    writer.Write((short)1);  // AudioFormat = PCM
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(byteRate);
    writer.Write(blockAlign);
    writer.Write(bitsPerSample);
    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write((int)pcmBytes);

    return ((MemoryStream)writer.BaseStream).ToArray();
}
```

---

## Pattern Pros & Cons

### Pros
- **No abstraction overhead**: Direct native API calls — zero wrappers or adapters
- **Compile-time safety**: Wrong-platform code never compiles; catches issues early
- **Full API access**: No need for plugin authors to expose every native feature
- **Single file**: Co-location aids understanding and maintenance

### Cons
- **File size**: Single `AudioService.cs` is ~400 lines — large but manageable
- **Testing**: Platform-specific code is hard to unit test without additional abstractions
- **New platform**: Adding a new platform requires modifying the shared file
- **Readability**: Mixed `#if` blocks can be harder to read than separate files
