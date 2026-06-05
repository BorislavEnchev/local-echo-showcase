# Challenges & Solutions

This document details the significant engineering challenges encountered during the development of LocalEcho and the solutions implemented to address them.

---

## 1. Long Transcripts Exceeding LLM Context Windows

### Problem
Hour-long recordings produce transcripts of 50,000+ characters. LLMs have fixed context windows:
- Phi-3 Mini: 4,096 tokens (~5,000 characters)
- Qwen/Llama: 16,384 tokens (~25,000 characters)

Simply truncating would lose information, and loading everything causes `NoKvSlot` crashes.

### Solution: Map-Reduce Chunked Summarization

```
Transcript (100K chars)
    ├── Chunk 1 (25K) ──→ Summary 1
    ├── Chunk 2 (25K) ──→ Summary 2
    ├── Chunk 3 (25K) ──→ Summary 3
    └── Chunk 4 (25K) ──→ Summary 4
                │
                ▼
         Consolidation
                │
                ▼
         Final Summary
```

**Implementation details:**
- Chunking respects line boundaries (splits on `\n`) to avoid breaking mid-sentence
- Each chunk is summarized with the same prompt for consistency
- A final consolidation pass merges chunk summaries, removes redundancy, and ensures coherence
- Short transcripts (< chunk size) get a direct single-pass skip to avoid overhead
- The `ConsolidateSummariesAsync` step is context-size aware, truncating if needed

### Key Insight
The single-pass approach works well for most recordings. The map-reduce pipeline handles the long-tail cases without penalizing common use cases.

---

## 2. GPU Memory Management for LLMs

### Problem
Consumer GPUs (4-8GB VRAM) struggle to hold both model weights and KV cache simultaneously. Phi-3 Mini alone is ~2.3GB Q4 quantized, and a 4K KV cache adds ~1.5GB. On a 4GB GPU, this is tight. On integrated GPUs with shared memory, it's worse.

### Solution: Context-Size and Layer Allocation Tuning

```csharp
// Model-specific configuration
var contextSize = targetModel == LlmModelType.Phi3Mini ? 4096u : 16384u;
var gpuLayers = targetModel == LlmModelType.Phi3Mini ? 20 : 100;
```

**Phi-3 Mini (2.3GB):**
- 4K context (smaller KV cache footprint)
- 20 GPU layers (keep ~3.5GB on GPU, spill ~1GB to system RAM)
- 5K chunk size for summaries (limits input tokens)

**Qwen/Llama (0.5-1.5GB):**
- 16K context (larger KV cache but small weights leave room)
- 100 GPU layers (full GPU offload)
- 25K chunk size for summaries

### Additional Safeguards
- `StatelessExecutor` is used instead of creating a persistent `LLamaContext` — this avoids double-allocating KV cache
- Memory is freed after each inference via `UnloadModel()` method
- A try-catch for `NoKvSlot` exceptions provides a graceful fallback message rather than a crash

---

## 3. Cross-Platform Audio Capture

### Problem
Each target platform uses fundamentally different audio APIs:

| Platform | Microphone | System Audio |
|---|---|---|
| Windows | `WaveInEvent` | `WasapiLoopbackCapture` |
| macOS | `AVAudioRecorder` | N/A (platform limitation) |
| Android | `AudioRecord` | `AudioPlaybackCapture` (API 29+) |

No single .NET audio library supports all three platforms for both capture modes.

### Solution: Platform-Specialized Code Behind a Unified Interface

```csharp
public interface IAudioService
{
    Task<AudioStartResult> StartRecordingAsync(bool systemAudio = false);
    Task<AudioStopResult> StopRecordingAsync();
    void PauseRecording();
    void ResumeRecording();
    bool IsPaused { get; }
}
```

**Inside the implementation:**
- Conditional compilation (`#if WINDOWS`, `#elif MACCATALYST`, `#elif ANDROID`) keeps platform-specific code isolated
- Each block uses the native API directly with no abstraction overhead
- The `AudioStartResult` record captures whether microphone fallback was used (common on Android where system audio requires user-granted permissions)
- Android has a particularly complex flow: request `MediaProjection` permission → fall back to microphone if denied

### Challenge: WAV Headers on Android
Android's `AudioRecord` outputs raw PCM data. We manually construct 44-byte WAV headers after recording completes:
```csharp
private static byte[] BuildWavHeader(...) { /* RIFF + fmt + data chunks */ }
```

---

## 4. Model Download Resilience

### Problem
Whisper and LLM models are 75MB to 2.3GB downloads from HuggingFace. In some regions, HuggingFace is slow or blocked. Partial downloads from network interruptions would leave corrupt model files.

### Solution: Streaming with Mirror Fallback and Cleanup

```
Download Flow:
    1. Try HuggingFace (primary) ──→ Success? → Done
         │
         └── Failure? → Try hf-mirror.com (China mirror)
              │
              └── Failure? → Clean up partial file, throw
```

**Key implementation details:**
- `HttpCompletionOption.ResponseHeadersRead` enables streaming without buffering
- Progress reported at ≥0.5% intervals to avoid flooding the UI thread
- Partial files are deleted on any failure to prevent loading corrupt models
- Download timeout set to 30 minutes for large files
- `CancellationToken` support enables user cancellation

---

## 5. Model Output Quality Control

### Problem
Small LLMs (especially 0.5B-1.5B) exhibit degenerate behaviors:
- **Repetition loops**: Repeating the same phrase indefinitely
- **Anti-prompt leakage**: Outputting `<|im_end|>` or similar tokens
- **Timestamp inclusion**: Whisper-style `[00:00:00]` timestamps bleeding into summaries

### Solution: Multi-Layered Output Sanitization

```
Raw LLM Output
    ├── 1. Anti-prompt stripping (remove trailing control tokens)
    ├── 2. Repetition detection (collapse 3+ identical lines)
    ├── 3. Timestamp awareness (don't count timestamp-only diffs as unique)
    └── 4. Sampling penalty (repeat_penalty = 1.2)
```

**Repetition cleaning logic:**
```csharp
// Compare content AFTER removing timestamps to avoid treating
// "[00:00:01] Hello" and "[00:00:02] Hello" as different lines
string cleanContent = trimmed;
if (trimmed.StartsWith("[") && trimmed.Contains("] "))
{
    var idx = trimmed.IndexOf("] ");
    cleanContent = trimmed.Substring(idx + 2).Trim();
}
```

This prevents the model from looping on phrases like "Thank you for watching" or "I hope this helps" that small models tend to fixate on.

---

## 6. Phi-3 Mini Crashes on Long Inputs

### Problem
Phi-3 Mini is a 4K-context model. When the application sent inputs exceeding this limit, the model crashed with `NoKvSlot` errors — the single most common crash reported by users.

### Solution: Context-Size-Dependent Branching

Every input path is now context-size-aware:

| Operation | Phi-3 Mini Limit | Qwen/Llama Limit |
|---|---|---|
| Summary chunk size | 5,000 chars | 25,000 chars |
| Chat context | 6,000 chars | 32,000 chars |
| Max output tokens | 800 tokens | 1,500 tokens |
| GPU layers | 20 layers | 100 layers |

```csharp
int chunkMaxChars = _currentModelType == LlmModelType.Phi3Mini ? 5000 : 25000;
```

Additionally, `NoKvSlot` and `OutOfMemoryException` exceptions are caught and replaced with a user-friendly message:
> *"The transcript is too long for this AI model. Try a shorter recording or switch to a smaller model (e.g. Qwen 0.5B) in Settings."*

---

## 7. Android Build Configuration

### Problem
Building .NET MAUI for Android requires specific SDK paths that differ per developer machine. Hard-coding paths causes build failures across environments.

### Solution: Centralized Build Properties with User Profile Path

```xml
<Project>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net9.0-android'">
    <AndroidSdkDirectory>$(UserProfile)\AppData\Local\Android\Sdk</AndroidSdkDirectory>
    <JavaSdkDirectory>$(UserProfile)\AppData\Local\Microsoft\Jdk\jdk-17.0.18+8</JavaSdkDirectory>
  </PropertyGroup>
</Project>
```

Using `$(UserProfile)` ensures the path adapts to each developer's machine. The `Directory.Build.props` file is scoped to Android builds only.

---

## 8. Security: AI Content Moderation

### Problem
As an App Store requirement (particularly Microsoft Store), the application needs a mechanism for users to report inappropriate AI-generated content, even though all models run locally.

### Solution: Email-Based Reporting

The Settings page includes a "Report AI Content or Issues" button that opens the system email client with a pre-addressed message to `info.localecho@gmail.com`:

```csharp
var message = new EmailMessage
{
    Subject = "LocalEcho - AI Content Report / Issue",
    To = new List<string> { "info.localecho@gmail.com" }
};
await Email.Default.ComposeAsync(message);
```

Because all AI runs locally, the app itself cannot generate or host inappropriate content — the reporting mechanism exists to address potential model biases or hallucinations in a transparent way.

---

## 9. UI Polish for Windows

### Problem
.NET MAUI's default Windows rendering has several polish issues:
- Buttons show arrow cursor instead of hand cursor
- Editor and Entry controls show inner borders that look outdated
- Focus/visual state feedback is minimal

### Solution: Platform-Specific Handler Customization

```csharp
#if WINDOWS
// Hand cursor for buttons
ButtonHandler.Mapper.AppendToMapping("Cursor", (handler, view) => {
    // Set InputSystemCursorShape.Hand on pointer enter
});

// Borderless editors
EditorHandler.Mapper.AppendToMapping("NoBorder", (handler, view) => {
    platformView.BorderThickness = new Thickness(0);
    platformView.Background = null;
});
#endif
```

These customizations are applied at startup in `MauiProgram.cs` and only affect Windows, keeping platform-specific polish isolated.
