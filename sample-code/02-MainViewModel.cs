// 02. ViewModel Pattern
//
// MVVM with CommunityToolkit.Mvvm source generators for clean, maintainable presentation logic.
//
// Key patterns:
// 1. Source-Generated Properties: [ObservableProperty] generates INotifyPropertyChanged
// 2. Source-Generated Commands: [RelayCommand] converts async methods into ICommand properties
// 3. Constructor Injection: All dependencies injected via constructor
// 4. UI Updates on Main Thread: Callbacks marshaled to UI thread
// 5. Lifecycle Management: ViewModels created via DI and tied to page lifecycle

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// ──────────────────────────────────────────────
// MainViewModel — Core Recording & Transcription Flow
// ──────────────────────────────────────────────

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
        var entry = new TranscriptionEntry { /* ... */ };
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

    // ── Constructor Injection ──

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
}

// ──────────────────────────────────────────────
// Generated Code Examples (for reference)
// ──────────────────────────────────────────────

// [ObservableProperty] generates:
// private bool _isRecording;
// public bool IsRecording
// {
//     get => _isRecording;
//     set
//     {
//         if (_isRecording != value)
//         {
//             _isRecording = value;
//             OnPropertyChanged();
//         }
//     }
// }

// [RelayCommand] on RecordMic() generates:
// public ICommand RecordMicCommand { get; }

// ──────────────────────────────────────────────
// Page Code-Behind Wiring
// ──────────────────────────────────────────────

// public partial class MainPage : ContentPage
// {
//     private readonly MainViewModel _viewModel;
//
//     public MainPage(MainViewModel viewModel)
//     {
//         InitializeComponent();
//         _viewModel = viewModel;
//         BindingContext = _viewModel;
//     }
//
//     protected override async void OnAppearing()
//     {
//         base.OnAppearing();
//         await _viewModel.InitializeAsync();
//     }
// }
