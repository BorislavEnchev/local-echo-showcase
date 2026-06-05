// 05. Dependency Injection Setup
//
// The composition root in MauiProgram.cs demonstrates clean DI registration
// with platform-conditional configuration.
//
// Registration strategy:
// - Singletons: Shared state, thread-safe services (audio, transcription, summarization, etc.)
// - Transient: Fresh instance per navigation (ViewModels, Pages)

using Microsoft.Extensions.Logging;

// ──────────────────────────────────────────────
// MauiProgram — Composition Root
// ──────────────────────────────────────────────

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseMauiCommunityToolkit();

        // ── LLamaSharp GPU Backend Configuration ──
        //
        // Cascading configuration:
        //   1st priority: Vulkan (AMD, Intel, integrated GPUs)
        //   2nd priority: CUDA (NVIDIA GPUs)
        //   3rd priority: CPU (fallback if neither GPU backend is available)
        LLama.Native.NativeLibraryConfig.All
            .WithVulkan(true)    // AMD / Universal
            .WithCuda(true)      // NVIDIA
            .WithAutoFallback(true);  // CPU if no GPU

        // ── Service Registration ──
        RegisterServices(builder.Services);

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Singletons: shared state, thread-safe services
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<ISummarizationService, SummarizationService>();
        services.AddSingleton<IProService, ProService>();
        services.AddSingleton<LibraryService>();

        // Singleton ViewModel: shared state across app
        services.AddSingleton<OnboardingViewModel>();

        // Transient: fresh instance per navigation
        services.AddTransient<MainViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<LibraryPage>();
        services.AddTransient<EntryDetailPage>();
        services.AddTransient<LibraryChatPage>();
    }
}

// ──────────────────────────────────────────────
// Platform-Specific Configuration Pattern
// ──────────────────────────────────────────────

// Alternative approach (not currently used — current implementation uses
// conditional compilation inside AudioService.cs rather than at DI level):
//
// #if WINDOWS
//     builder.Services.AddSingleton<IAudioService, WindowsAudioService>();
// #elif MACCATALYST
//     builder.Services.AddSingleton<IAudioService, MacAudioService>();
// #elif ANDROID
//     builder.Services.AddSingleton<IAudioService, AndroidAudioService>();
// #endif

// ──────────────────────────────────────────────
// View -> ViewModel Wiring
// ──────────────────────────────────────────────

// Pages receive their ViewModel through constructor injection:
//
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
