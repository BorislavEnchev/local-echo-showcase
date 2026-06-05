// 01. Service Interfaces
//
// Clean, interface-based abstractions enable testability, platform flexibility,
// and clear separation of concerns.
//
// Design highlights:
// - Record types for return values: Immutable, value-equatable, with named parameters
// - UsedFallback flag: Captures whether microphone fallback was triggered
// - IAsyncEnumerable<string>: Streams transcription segments as they're processed
// - Full lifecycle management: Download -> Load -> Use -> Unload
// - Capability checking: GetCapabilityAsync() returns whether local AI is supported

// ──────────────────────────────────────────────
// IAudioService — Cross-Platform Audio Capture
// ──────────────────────────────────────────────

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

// ──────────────────────────────────────────────
// ITranscriptionService — Speech-to-Text
// ──────────────────────────────────────────────

public interface ITranscriptionService
{
    IAsyncEnumerable<string> TranscribeAsync(
        string audioFilePath,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    Task EnsureModelExistsAsync(
        GgmlType modelType,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    GgmlType CurrentModelType { get; }
    Task<bool> IsModelDownloadedAsync(GgmlType modelType);
}

// ──────────────────────────────────────────────
// ISummarizationService — LLM Summarization
// ──────────────────────────────────────────────

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

// ──────────────────────────────────────────────
// IProService — Licensing Interface
// ──────────────────────────────────────────────

public interface IProService
{
    bool IsPro { get; }
    Task<bool> ActivateProAsync(string licenseKey);
    void DeactivatePro();
}
