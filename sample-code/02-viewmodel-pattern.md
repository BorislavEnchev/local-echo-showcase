# 02. ViewModel Pattern

MVVM with CommunityToolkit.Mvvm source generators for clean, maintainable presentation logic.

---

## MainViewModel — Core Recording & Transcription Flow

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ISummarizationService _summarizationService;
    private readonly LibraryService _libraryService;

    private IDispatcherTimer? _recordingTimer;
    private int _secondsRecording;
    private string _currentAudioPath = string.Empty;
    private int _currentEntryId;

    // ── Observable Properties (source-generated) ──

    [ObservableProperty]
    public partial bool IsRecording { get; set; }

    [ObservableProperty]
    public partial string RecordingTime { get; set; } = "00:00";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready to record";

    [ObservableProperty]
    public partial string TranscriptText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double TranscriptionProgress { get; set; }

    [ObservableProperty]
    public partial bool IsSummarizing { get; set; }

    // ── Commands (source-generated from methods) ──

    [RelayCommand]
    private async Task RecordMic()
    {
        await StartRecordingAsync(systemAudio: false);
    }

    [RelayCommand]
    private async Task RecordSystem()
    {
        await StartRecordingAsync(systemAudio: true);
    }

    [RelayCommand]
    private async Task StopRecording()
    {
        if (!IsRecording) return;

        StatusText = "Stopping...";
        var stopResult = await _audioService.StopRecordingAsync();
        _currentAudioPath = stopResult.FilePath;

        IsRecording = false;
        _recordingTimer?.Stop();

        StatusText = "Transcribing...";
        IsTranscriptionProgressBarVisible = true;

        var builder = new StringBuilder();
        await foreach (var segment in _transcriptionService
            .TranscribeAsync(_currentAudioPath, 
                progress => TranscriptionProgress = progress))
        {
            builder.AppendLine(segment);
            TranscriptText = builder.ToString();
        }

        // Save to library
        var entry = new TranscriptionEntry { ... };
        await _libraryService.SaveEntryAsync(entry);
    }

    [RelayCommand]
    private async Task Summarize()
    {
        if (string.IsNullOrWhiteSpace(TranscriptText)) return;

        StatusText = "Summarizing with local AI...";
        IsSummarizing = true;

        var summaryType = SelectedSummaryTypeIndex switch
        {
            1 => SummaryType.Detailed,
            2 => SummaryType.ActionItems,
            3 => SummaryType.QuestionAnswer,
            _ => SummaryType.Concise
        };

        var summary = await Task.Run(() =>
            _summarizationService.SummarizeAsync(TranscriptText, summaryType));

        SummaryText = summary;
        StatusText = "Summary Complete";
    }

    // ── Helper Methods ──

    private async Task StartRecordingAsync(bool systemAudio)
    {
        if (IsRecording) return;

        var status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted) return;

        var startResult = await _audioService.StartRecordingAsync(systemAudio);
        IsRecording = true;
        _recordingTimer?.Start();
    }
}
```

## Key MVVM Patterns

### 1. Source-Generated Properties
The `[ObservableProperty]` attribute generates:
- `INotifyPropertyChanged` implementation
- `partial` property with getter/setter
- Automatic change notification

```csharp
// What you write:
[ObservableProperty]
public partial bool IsRecording { get; set; }

// What's generated:
private bool _isRecording;
public bool IsRecording
{
    get => _isRecording;
    set
    {
        if (_isRecording != value)
        {
            _isRecording = value;
            OnPropertyChanged();
        }
    }
}
```

### 2. Source-Generated Commands
The `[RelayCommand]` attribute converts async methods into `ICommand` properties:

```csharp
// What you write:
[RelayCommand]
private async Task RecordMic() { ... }

// What's generated (accessible in XAML):
// public ICommand RecordMicCommand { get; }
```

- Handles `CanExecute` for `IsEnabled` binding
- Async void → async Task conversion for fire-and-forget safety
- Automatic `NotifyCanExecuteChangedFor` support

### 3. Constructor Injection
All dependencies are injected via the constructor — registered as Transient in DI:

```csharp
public MainViewModel(
    IAudioService audioService,
    ITranscriptionService transcriptionService,
    ISummarizationService summarizationService,
    LibraryService libraryService,
    OnboardingViewModel onboardingViewModel)
{
    _audioService = audioService;
    _transcriptionService = transcriptionService;
    _summarizationService = summarizationService;
    _libraryService = libraryService;
    Onboarding = onboardingViewModel;
}
```

### 4. UI Updates on Main Thread
Since callbacks from services may arrive on background threads, UI updates are marshaled:

```csharp
await foreach (var segment in _transcriptionService
    .TranscribeAsync(_currentAudioPath, progress =>
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TranscriptionProgress = progress;
        });
    }))
```

**Note:** CommunityToolkit.Mvvm's `[ObservableProperty]` automatically marshals property changes to the UI thread when the property is set from a background thread, but explicit `BeginInvokeOnMainThread` is used for progress callbacks that arrive within `Action<double>` delegates.

### 5. Lifecycle Management
ViewModels are created via DI and tied to page lifecycle:

```csharp
// Page code-behind
protected override async void OnAppearing()
{
    base.OnAppearing();
    await _viewModel.InitializeAsync();
}
```

- **Singleton**: `OnboardingViewModel` (shared state across the app)
- **Transient**: `MainViewModel`, `LibraryViewModel` (fresh instance per navigation)
