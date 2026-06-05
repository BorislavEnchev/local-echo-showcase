// 03. Data Model
//
// SQLite-backed entity design with clean serialization and metadata.
//
// Design rationale:
// - Denormalized: A single recording produces ~100-500 entries — well within SQLite's sweet spot
// - The app always loads full entries — partial loading adds complexity without benefit
// - Simpler queries and export: just serialize the whole entry

using System.Text;
using SQLite;

// ──────────────────────────────────────────────
// TranscriptionEntry
// ──────────────────────────────────────────────

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

// ──────────────────────────────────────────────
// Summary Types Enum
// ──────────────────────────────────────────────

public enum SummaryType
{
    Concise,        // Bullet-point key ideas
    Detailed,       // Multi-section structured report
    ActionItems,    // Markdown task checklist
    QuestionAnswer  // Q&A pairs from content
}

// ──────────────────────────────────────────────
// SQLite Repository (LibraryService)
// ──────────────────────────────────────────────

public class LibraryService
{
    private SQLiteAsyncConnection? _database;

    /// <summary>
    /// Lazy initialization — database connection is created on first use, not at construction.
    /// Eliminates startup delay with no need for async factory or DI registration gymnastics.
    /// </summary>
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

    /// <summary>
    /// Simple substring matching (not full-text search) across Title, Transcript, and Summary.
    /// Good enough for hundreds of entries; would benefit from SQLite FTS5 for larger libraries.
    /// </summary>
    public async Task<List<TranscriptionEntry>> SearchEntriesAsync(string query)
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

    // ── RAG Context Retrieval ──

    /// <summary>
    /// Retrieves relevant content for RAG-style chat queries.
    /// Special-cases "latest"/"recent" keywords, falls back to most recent entries.
    /// Each entry's transcript is capped at 15K characters to prevent context bloat.
    /// Maximum 5 entries returned per query.
    /// </summary>
    public async Task<string> GetRelevantContentForChatAsync(string query)
    {
        // Special case: "latest", "recent", "last" -> 3 most recent
        var lowerQuery = query.ToLowerInvariant();
        if (lowerQuery.Contains("latest") ||
            lowerQuery.Contains("recent") ||
            lowerQuery.Contains("last"))
        {
            return FormatEntries(await GetTop3MostRecent());
        }

        // Standard case: search for relevant entries
        var relevant = await SearchEntriesAsync(query);

        // Cap at 5 entries to prevent context bloat
        if (relevant.Count > 5)
            relevant = relevant.Take(5).ToList();

        // Fallback: return 3 most recent
        if (relevant.Count == 0)
            relevant = await GetTop3MostRecent();

        // Format as XML-like records for the LLM context
        return FormatEntries(relevant);
    }

    private Task<List<TranscriptionEntry>> GetTop3MostRecent()
    {
        // Returns the 3 most recent entries
        throw new NotImplementedException();
    }

    private string FormatEntries(List<TranscriptionEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine($"<Record id=\"{entry.Id}\" " +
                $"title=\"{entry.Title}\">");
            sb.AppendLine($"Date: {entry.Timestamp:yyyy-MM-dd HH:mm}");

            if (!string.IsNullOrWhiteSpace(entry.Summary))
                sb.AppendLine($"Summary: {entry.Summary}");

            if (!string.IsNullOrWhiteSpace(entry.Transcript))
            {
                var transcript = entry.Transcript;
                if (transcript.Length > 15000)  // Cap per-entry length
                    transcript = transcript[..15000] +
                        "... [Truncated]";
                sb.AppendLine($"Transcript: {transcript}");
            }
            sb.AppendLine("</Record>");
        }
        return sb.ToString();
    }
}
