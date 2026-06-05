# 05. Dependency Injection Setup

The composition root in `MauiProgram.cs` demonstrates clean DI registration with platform-conditional configuration.

---

## Service Registration

```csharp
using Microsoft.Extensions.Logging;

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
        LLama.Native.NativeLibraryConfig.All
            .WithVulkan(true)    // AMD / Universal
            .WithCuda(true)      // NVIDIA
            .WithAutoFallback(true);  // CPU if no GPU

        // ── Service Registration ──
        // Singletons: shared state, thread-safe services
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services
            .AddSingleton<ITranscriptionService, TranscriptionService>();
        builder.Services
            .AddSingleton<ISummarizationService, SummarizationService>();
        builder.Services.AddSingleton<IProService, ProService>();
        builder.Services.AddSingleton<LibraryService>();

        // Singleton ViewModel: shared state across app
        builder.Services.AddSingleton<OnboardingViewModel>();

        // Transient: fresh instance per navigation
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<EntryDetailPage>();
        builder.Services.AddTransient<LibraryChatPage>();

        return builder.Build();
    }
}
```

---

## Registration Strategy

### Singletons (Shared State)

| Service | Reason |
|---|---|
| `IAudioService` | Single recording session; holds device state |
| `ITranscriptionService` | Manages Whisper model lifecycle (download, load, cache) |
| `ISummarizationService` | Manages LLM model lifecycle (~2GB memory allocation) |
| `IProService` | Feature flag state; rarely changes |
| `LibraryService` | Database connection; thread-safe singleton |
| `OnboardingViewModel` | Shared setup wizard state across pages |

### Transients (Per Navigation)

| Service | Reason |
|---|---|
| `MainViewModel` | Fresh recording session per page visit |
| `LibraryViewModel` | Refreshes data on each navigation |
| `MainPage`, `SettingsPage`, etc. | Pages are disposable; fresh instances avoid memory leaks |

---

## Platform-Specific Configuration

```csharp
#if WINDOWS
// Windows-only package reference (NAudio, Vulkan)
builder.Services.AddSingleton<IAudioService, AudioService>();
#endif
```

While the current implementation uses conditional compilation **inside** `AudioService.cs` rather than at the DI level, the pattern supports swapping entire services per platform:

```csharp
// Alternative approach (not currently used):
#if WINDOWS
    builder.Services.AddSingleton<IAudioService, WindowsAudioService>();
#elif MACCATALYST
    builder.Services.AddSingleton<IAudioService, MacAudioService>();
#elif ANDROID
    builder.Services.AddSingleton<IAudioService, AndroidAudioService>();
#endif
```

---

## GPU Backend Cascading

```csharp
LLama.Native.NativeLibraryConfig.All
    .WithVulkan(true)       // 1st priority: AMD / integrated GPUs
    .WithCuda(true)         // 2nd priority: NVIDIA GPUs
    .WithAutoFallback(true); // 3rd priority: CPU fallback
```

This cascading configuration:
- Prioritizes Vulkan (works on AMD, Intel, and many integrated GPUs)
- Falls back to CUDA (NVIDIA-optimized)
- Falls back to CPU if neither GPU backend is available
- All configured at startup with a single config object

---

## View → ViewModel Wiring

Pages receive their ViewModel through constructor injection:

```csharp
// Page code-behind
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
```

**Key details:**
- ViewModel is injected, not instantiated by the page
- `BindingContext` is set in the constructor — available before `OnAppearing`
- `InitializeAsync()` is called from `OnAppearing`, not the constructor — avoids async constructor issues
- Pages don't know about service dependencies — only the ViewModel does
