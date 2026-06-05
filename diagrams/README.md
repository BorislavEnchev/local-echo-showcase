# Architecture Diagrams

This directory contains all architecture diagrams for the LocalEcho showcase. Each diagram is provided as both an editable `.mmd` (Mermaid) file and a rendered `.png` image.

---

## Diagram Index

| # | File | Type | Description |
|---|---|---|---|
| 01 | [system-overview](01-system-overview.mmd) | Flowchart | Three-layer architecture (Presentation, Service, Data/Infrastructure) with view-model-service relationships |
| 02 | [data-flow](02-data-flow.mmd) | Sequence | Full recording → transcription → summarization flow with map-reduce branching |
| 03 | [cross-platform-audio](03-cross-platform-audio.mmd) | Flowchart | `IAudioService` abstraction for Windows (NAudio), macOS (AVFoundation), Android (AudioRecord/MediaProjection) |
| 04 | [dependency-injection](04-dependency-injection.mmd) | Flowchart | Singleton vs Transient DI registration graph with dependency arrows |
| 05 | [map-reduce-summarization](05-map-reduce-summarization.mmd) | Flowchart | Map-reduce pipeline: split → summarize chunks → consolidate into final summary |
| 06 | [model-download-flow](06-model-download-flow.mmd) | Flowchart | Model download flow with HuggingFace / China mirror fallback and cancellation |
| 07 | [output-sanitization](07-output-sanitization.mmd) | Flowchart | Multi-layered LLM output quality control (anti-prompt, repetition, timestamp) |
| 08 | [rag-chat-flow](08-rag-chat-flow.mmd) | Sequence | RAG-based "Library Brain" chat: query → context retrieval → LLM answer → clickable links |

---

## Viewing

- **PNG files**: Open directly in any browser or image viewer
- **MMD files**: Open in any text editor; render with [mermaid-cli](https://github.com/mermaid-js/mermaid-cli) or paste into [Mermaid Live Editor](https://mermaid.live/)

### Regenerating PNGs

```bash
# Requires mmdc (mermaid-cli) installed globally
cd showcase/diagrams
for f in *.mmd; do
    mmdc -i "$f" -o "${f%.mmd}.png" --backgroundColor white
done
```
