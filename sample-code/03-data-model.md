# 03. Data Model

SQLite-backed entity design with clean serialization and metadata.

---

## TranscriptionEntry

```csharp
using SQLite;

public class TranscriptionEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string AudioPath { get; set; } = string.Empty;
    public string SummaryType { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public bool IsFavorite { get; set; }
    public string Tags { get; set; } = string.Empty;
}
```

### Design Rationale

| Property | Purpose |
|---|---|
| `Id` | SQLite auto-increment primary key — doubles as navigation parameter |
| `Title` | AI-generated title (e.g., "AI and the Future of Work") |
| `Transcript` | Full timestamped transcript text (potentially 100K+ chars) |
| `Summary` | AI-generated summary text (varies by SummaryType) |
| `Timestamp` | Recording date/time — used for chronological sorting |
| `AudioPath` | Path to local WAV file (device-local, not portable) |
| `SummaryType` | Stored as string for display ("Concise", "Detailed", etc.) |
| `DurationSeconds` | Recording length — avoids re-reading audio file metadata |
| `IsFavorite` | Bookmark flag — simple boolean toggle |
| `Tags` | String field reserved for future categorization |

### Why Denormalized?

A normalized design might split summaries, tags, and metadata into separate tables. However:
- A single recording produces ~100-500 entries per user — well within SQLite's performance sweet spot
- The app always loads full entries — partial loading adds complexity without benefit
- Simpler queries: `await _database.Table<TranscriptionEntry>().ToListAsync();`
- No relationship management — each entry is self-contained
- Export is trivial: serialize the whole entry

---

## Summary Types Enum

```csharp
public enum SummaryType
{
    Concise,        // Bullet-point key ideas
    Detailed,       // Multi-section structured report
    ActionItems,    // Markdown task checklist
    QuestionAnswer  // Q&A pairs from content
}
```

Used as a strategy selector — the `SummarizationService` selects different system prompts based on the enum value, driving completely different output formats from the same LLM.

---

## SQLite Repository (LibraryService)

```csharp
public class LibraryService
{
    private SQLiteAsyncConnection? _database;

    private async Task Init()
    {
        if (_database is not null) return;

        var dbPath = Path.Combine(
            FileSystem.AppDataDirectory, "LocalEcho.db3");
        _database = new SQLiteAsyncConnection(dbPath);
        await _database.CreateTableAsync<TranscriptionEntry>();
    }

    public async Task<List<TranscriptionEntry>> GetEntriesAsync()
    {
        await Init();
        return await _database!
            .Table<TranscriptionEntry>()
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();
    }

    public async Task<int> SaveEntryAsync(TranscriptionEntry entry)
    {
        await Init();
        if (entry.Id != 0)
            return await _database!.UpdateAsync(entry);
        else
            return await _database!.InsertAsync(entry);
    }

    public async Task<int> DeleteEntryAsync(TranscriptionEntry entry)
    {
        await Init();
        return await _database!.DeleteAsync(entry);
    }

    public async Task<List<TranscriptionEntry>> SearchEntriesAsync(
        string query)
    {
        await Init();
        var allEntries = await _database!
            .Table<TranscriptionEntry>()
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();

        return allEntries
            .Where(e => e.Title.Contains(query,
                StringComparison.OrdinalIgnoreCase)
                || e.Transcript.Contains(query,
                    StringComparison.OrdinalIgnoreCase)
                || e.Summary.Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

### Lazy Initialization Pattern

```csharp
private async Task Init()
{
    if (_database is not null) return;
    // Create connection + table
}
```

- Database connection is created on **first use**, not at construction
- Eliminates startup delay
- No need for async factory or DI registration gymnastics
- Thread-safe enough for single-user desktop app

### Search Strategy

- **Simple substring matching** (not full-text search)
- Three-field scan: Title, Transcript, Summary
- Good enough for hundreds of entries
- Would benefit from SQLite FTS5 for larger libraries

### RAG Context Retrieval

```csharp
public async Task<string> GetRelevantContentForChatAsync(string query)
{
    // Special case: "latest", "recent", "last" → 3 most recent
    if (query.Contains("latest") || query.Contains("recent"))
    {
        return top3MostRecent...;
    }

    // Standard case: search for relevant entries
    var relevant = await SearchEntriesAsync(query);
    if (!relevant.Any())
    {
        // Fallback: return 3 most recent
        return top3MostRecent...;
    }

    // Format as XML-like records
    return "<Record id=\"...\" title=\"...\">\n...\n</Record>";
}
```

**Key decisions:**
- Each entry's transcript is capped at 15K characters to prevent context bloat
- Maximum 5 entries returned per query
- Entries formatted as structured `<Record>` blocks for the LLM prompt
- Fallback to latest entries when search yields nothing
