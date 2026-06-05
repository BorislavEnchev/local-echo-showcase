// 06. Platform Abstraction Pattern
//
// Handling fundamentally different platform APIs behind a unified interface
// using conditional compilation.
//
// Each platform uses a completely different audio capture API:
//   Windows:  NAudio (WaveInEvent / WasapiLoopbackCapture)
//   macOS:    AVFoundation (AVAudioRecorder)
//   Android:  Android SDK (AudioRecord / AudioPlaybackCapture, API 29+)
//
// These APIs have no common base class and cannot be referenced across platforms.
// Solution: #if blocks with a complete implementation per platform.

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

// ──────────────────────────────────────────────
// AudioService — Cross-Platform Implementation
// ──────────────────────────────────────────────

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

    public bool IsPaused => _isPaused;

    /// <summary>
    /// Starts recording from either microphone or system audio.
    /// Each #if block is a complete implementation for that platform.
    /// </summary>
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

    public Task<AudioStopResult> StopRecordingAsync()
    {
#if WINDOWS
        _waveIn?.StopRecording();
        _writer?.Dispose();
        _waveIn?.Dispose();
        _waveIn = null;
        _writer = null;
        return Task.FromResult(new AudioStopResult(_currentFilePath, _captureMode));
#elif MACCATALYST
        _recorder?.Stop();
        _recorder?.Dispose();
        _recorder = null;
        return Task.FromResult(new AudioStopResult(_currentFilePath, _captureMode));
#elif ANDROID
        throw new NotImplementedException();
#endif
    }

    public void PauseRecording() => _isPaused = true;
    public void ResumeRecording() => _isPaused = false;

    // ── Android-Specific Implementation ──

#if ANDROID
    /// <summary>
    /// Android system audio capture is notably more complex than other platforms.
    /// Requires API 29+ and user permission via MediaProjection dialog.
    /// </summary>
    private async Task<(bool IsSuccess, string? Message)>
        TryStartAndroidSystemAudioAsync()
    {
        // 1. Check API level (requires Android 10+)
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
            return (false, "System audio requires Android 10+");

        // 2. Request MediaProjection permission (shows screen-capture dialog)
        var permission = await MainActivity
            .RequestMediaProjectionPermissionAsync(/* activity */);
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

    private async Task StartAndroidMicrophoneAsync()
    {
        // Android microphone recording implementation
        throw new NotImplementedException();
    }

    private async Task StartAndroidRecordingLoopAsync(AudioRecord record)
    {
        // Recording loop that reads PCM data from the AudioRecord
        throw new NotImplementedException();
    }

    /// <summary>
    /// Android's AudioRecord outputs raw PCM bytes.
    /// Construct WAV headers manually.
    /// </summary>
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
#endif
}
