# 01. Service Interfaces

Clean, interface-based abstractions enable testability, platform flexibility, and clear separation of concerns.

---

## `IAudioService` — Cross-Platform Audio Capture

```csharp
public enum AudioCaptureMode
{
    Microphone,
    System
}

public sealed record AudioStartResult(
    AudioCaptureMode ActualMode,
    bool UsedFallback,
    string? Message = null);

public sealed record AudioStopResult(
    string FilePath,
    AudioCaptureMode CaptureMode);

public interface IAudioService
{
    Task<AudioStartResult> StartRecordingAsync(bool systemAudio = false);
    Task<AudioStopResult> StopRecordingAsync();
    void PauseRecording();
    void ResumeRecording();
    bool IsPaused { get; }
}
```

**Design highlights:**
- **Record types** for return values: Immutable, value-equatable, with named parameters
- **`UsedFallback` flag**: Captures whether microphone fallback was triggered (e.g., Android system audio denied)
- **Optional `Message`**: Provides user-facing context (e.g., "System audio unavailable, using mic")
- **Simple contract**: 4 methods + 1 property — easy to mock, implement, and test

---

## `ITranscriptionService` — Speech-to-Text

```csharp
public interface ITranscriptionService
{
    IAsyncEnumerable<string> TranscribeAsync(
        string audioFilePath,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    Task EnsureModelExistsAsync(
        GgmlType modelType,
        Action<double> progressCallback = null,
        CancellationToken ct = default);

    GgmlType CurrentModelType { get; }
    Task<bool> IsModelDownloadedAsync(GgmlType modelType);
}
```

**Design highlights:**
- **`IAsyncEnumerable<string>`**: Streams transcription segments as they're processed, enabling real-time UI updates
- **Progress callback**: `Action<double>` from 0.0 to 1.0 for indeterminate operations
- **Cancellation token**: Full cancellation support for long-running operations
- **Separation of concerns**: Model management (`EnsureModelExistsAsync`, `IsModelDownloadedAsync`) is separate from inference (`TranscribeAsync`)

---

## `ISummarizationService` — LLM Summarization

```csharp
public enum LlmModelType
{
    Qwen05B,   // Smallest, fastest (~400MB)
    Qwen15B,   // Good balance (~1GB)
    Llama1B,   // Meta's small model (~1.3GB)
    Phi3Mini   // Microsoft's small model (~2.3GB)
}

public sealed record SummarizationCapability(
    bool IsSupported,
    string Reason);

public interface ISummarizationService
{
    Task<string> SummarizeAsync(string transcript,
        SummaryType type = SummaryType.Concise);

    Task<string> GenerateTitleAsync(string transcript);

    Task<bool> IsModelDownloadedAsync(LlmModelType modelType);
    Task<bool> IsAnyModelAvailableAsync();

    Task DownloadModelAsync(LlmModelType modelType,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    Task<bool> LoadModelAsync(LlmModelType? modelType = null);
    void UnloadModel();

    bool IsModelLoaded { get; }
    LlmModelType? CurrentModelType { get; }
    bool IsSupportedOnCurrentDevice { get; }

    Task<string> ChatWithContextAsync(string context, string question);
    Task<SummarizationCapability> GetCapabilityAsync();
}
```

**Design highlights:**
- **Full lifecycle management**: Download → Load → Use → Unload
- **Capability checking**: `GetCapabilityAsync()` returns whether local AI is supported on the current device, with a human-readable reason if not
- **Null model type**: `LoadModelAsync(null)` auto-selects the best available model
- **`ChatWithContextAsync`**: RAG-style Q&A that accepts a pre-retrieved context + question
- **`SummaryType` enum** drives the strategy pattern for 4 different summary modes

---

## `IProService` — Licensing Interface

```csharp
public interface IProService
{
    bool IsPro { get; }
    Task<bool> ActivateProAsync(string licenseKey);
    void DeactivatePro();
}
```

**Design highlights:**
- Minimal, forward-compatible interface
- The current implementation returns `IsPro = true` (fully free), but the interface allows future monetization without breaking changes
- `string licenseKey` parameter anticipates future activation flows
