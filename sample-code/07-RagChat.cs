// 07. RAG Chat — "Library Brain"
//
// Retrieval-Augmented Generation over the local transcript library,
// enabling natural language Q&A.
//
// Architecture flow:
// 1. User asks a question
// 2. LibraryService retrieves relevant entries (keyword search + fallback)
// 3. Context formatted as XML-like <Record> blocks
// 4. SummarizationService generates answer with model-specific context limits
// 5. UI parses [Title](rec:<id>) links into clickable navigation spans

using System.Text;
using System.Text.RegularExpressions;

// ──────────────────────────────────────────────
// Chat Generation (in SummarizationService)
// ──────────────────────────────────────────────

public partial class SummarizationService : ISummarizationService
{
    /// <summary>
    /// Generates a chat response using RAG-style context.
    /// Reuses the same local LLM as summarization but with a chat-optimized prompt.
    /// </summary>
    public async Task<string> ChatWithContextAsync(
        string context, string question)
    {
        var systemPrompt =
            "You are the Library Brain, a precision-focused assistant. " +
            "Answer questions based on the <Record> data provided. " +
            "CRITICAL RULES:\n" +
            "1. Provide original, concise answers. " +
            "If referring to a recording, ALWAYS use this format: " +
            "[Title](rec:<id>).\n" +
            "2. If the user asks for 'latest' or 'recent' recordings, " +
            "the records provided are already sorted by date.\n" +
            "3. If the provided records don't contain the answer, say: " +
            "'I couldn't find a clear answer in the most relevant " +
            "recordings. Could you provide more details?'\n" +
            "4. Use synonyms for better matching but stay strictly " +
            "within the provided context.";

        // Model-specific context limit
        int contextChars = _currentModelType == LlmModelType.Phi3Mini
            ? 6000 : 32000;

        var userPrompt =
            $"[LIBRARY CONTEXT]\n{TruncateIfNeeded(context, contextChars)}\n\n" +
            $"[USER QUESTION]\n{question}";

        // Chat responses are capped at 500 tokens for speed
        return await GenerateResponseAsync(systemPrompt, userPrompt, 500);
    }
}

// ──────────────────────────────────────────────
// Chat UI — Inline Recording Links
// ──────────────────────────────────────────────

/// <summary>
/// Parses [Title](rec:<id>) patterns into clickable spans.
/// When a user clicks a recording link, they navigate to EntryDetailPage.
/// </summary>
public static class ChatMessageHelper
{
    /// <summary>
    /// Parses LLM response text and creates a Label with clickable recording links.
    /// Pattern matches: [Title](rec:42)
    /// </summary>
    public static Label CreateChatBubble(string text, bool isUser)
    {
        if (isUser)
        {
            return new Label { Text = text };
        }

        var formatted = new FormattedString();
        var regex = new Regex(@"\[(.*?)\]\(rec:(\d+)\)");
        var lastIndex = 0;

        foreach (Match match in regex.Matches(text))
        {
            // Add plain text before the match
            if (match.Index > lastIndex)
                formatted.Spans.Add(new Span
                {
                    Text = text[lastIndex..match.Index]
                });

            var title = match.Groups[1].Value;
            var id = match.Groups[2].Value;

            // Create a clickable link span
            var linkSpan = new Span
            {
                Text = title,
                TextColor = Color.FromArgb("#ac99ea"),
                TextDecorations = TextDecorations.Underline
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) =>
            {
                await Shell.Current.GoToAsync(
                    $"EntryDetailPage?id={id}");
            };
            linkSpan.GestureRecognizers.Add(tapGesture);
            formatted.Spans.Add(linkSpan);

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
            formatted.Spans.Add(new Span
            {
                Text = text[lastIndex..]
            });

        return new Label
        {
            FormattedText = formatted,
            TextColor = Colors.White
        };
    }
}
