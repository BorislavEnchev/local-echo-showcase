# Features

## Core Capabilities

### 🎤 Audio Recording
- **Dual capture modes**: Microphone input and system audio (speaker output)
- **Pause/Resume**: Temporarily suspend recording without starting over
- **Real-time timer**: On-screen recording duration indicator
- **Platform-optimized**:
  - **Windows**: NAudio — `WaveInEvent` (mic) / `WasapiLoopbackCapture` (system audio)
  - **macOS**: AVFoundation — `AVAudioRecorder` with PCM settings
  - **Android**: `AudioRecord` (mic) / `AudioPlaybackCapture` via `MediaProjection` API (system audio, Android 10+)

### 📝 Speech-to-Text Transcription
- **Local Whisper models**: English-optimized (Tiny, Base, Small) and multilingual variants
- **Automatic model download**: One-time download from HuggingFace with China mirror fallback
- **Timestamped transcription**: Each segment tagged with `[HH:mm:ss]` for navigation
- **GPU acceleration**: Automatic Vulkan → CUDA → CPU fallback based on hardware
- **16kHz WAV resampling**: Ensures Whisper compatibility across all platforms
- **Streaming-style output**: Real-time segment emission during processing

### 🤖 AI Summarization (4 Modes)

| Summary Type | Use Case | Output Format |
|---|---|---|
| **Concise** | Quick overview | Bullet-point key ideas |
| **Detailed** | Deep understanding | Multi-section structured report (Overview, Analysis, Takeaways, Facts) |
| **Action Items** | Task extraction | Markdown checklist `[ ] task` |
| **Q&A** | Knowledge extraction | Q&A pairs from content |

- **Local LLMs**: Qwen 2.5 (0.5B, 1.5B), Llama 3.2 (1B), Phi-3 Mini (2.3B)
- **Model-specific optimization**: Context size (4K for Phi-3, 16K for others), GPU layers (20 for Phi-3, 100 for others)
- **Map-Reduce pipeline**: Handles arbitrarily long recordings by chunking, summarizing, and consolidating
- **Smart titling**: AI-generated 3-6 word titles from transcript content
- **Repetition cleaning**: Degenerate model output loop detection and cleanup

### 🧠 Library Brain (RAG Chat)
- **Natural language queries**: Ask questions about your entire transcript library
- **Semantic context retrieval**: Search-based RAG that fetches only relevant entries
- **Inline recording references**: Clickable `[Title](rec:id)` links to jump to entries
- **Fallback logic**: Latest recordings when search yields no results
- **Model-aware context sizing**: Adapts to loaded model's context window

### 📚 Library Management
- **Full-text search**: Search across titles, transcripts, and summaries
- **Debounced search**: 300ms debounce with cancellation for smooth UX
- **Favorites**: Star/bookmark important recordings
- **Swipe-to-delete**: Quick deletion on mobile/touch interfaces
- **Export**: Save transcripts and summaries as `.txt` files
- **Entry details**: Magazine-style detail view with formatted transcript and summary

### ⚙️ Settings & Configuration
- **Whisper model selection**: 6 model variants (Tiny → Small, English/Multilingual)
- **LLM model selection**: 4 model options (Qwen 0.5B → Phi-3 2.3B)
- **Model download management**: Start, cancel, and monitor downloads
- **Theme selection**: System / Light / Dark mode
- **Reset setup**: Clear all downloaded models and restart onboarding
- **Capability detection**: Shows if local AI is supported on current hardware

### 🎨 User Experience
- **Responsive onboarding**: Step-by-step first-run setup (LLM download → Whisper download)
- **Progress indicators**: Download and processing progress bars
- **Status badges**: Real-time operation status display
- **Copy buttons**: One-click copy for transcripts and summaries
- **"Open" links**: Quick navigation from recording view to detail page
- **Dark mode**: Full light/dark theme following system preference
- **AI status indicator**: Color-coded status (green=ready, orange=needs setup, red=unavailable)

---

## Non-Functional Features

### Privacy
- **100% offline operation**: No cloud services, no data transmission
- **No telemetry**: Zero analytics, crash reporting, or usage tracking
- **Local storage**: All data in device-local SQLite database
- **No accounts**: No registration, login, or user profiling

### Resilience
- **Graceful model failures**: Download failures → cleanup partial files → retry
- **Platform fallbacks**: System audio unavailable → microphone fallback
- **Model fallbacks**: Preferred model missing → automatic fallback to any available model
- **Cancellation support**: Model downloads can be cancelled at any time
- **Error messages**: User-friendly error descriptions with actionable guidance

### Performance
- **GPU acceleration**: First-class support for Vulkan (AMD) and CUDA (NVIDIA)
- **Async everywhere**: Non-blocking UI during model downloads and inference
- **Streaming transcription**: Real-time segment output during processing
- **Memory management**: Explicit model unloading to free GPU/CPU memory
- **Chat response limits**: Capped token generation (500 tokens) for quick chat answers
