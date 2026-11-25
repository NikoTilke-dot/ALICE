namespace Alice.Core.Models;

/// <summary>Zähler für Wort-/Zeichenstatistik.</summary>
public static class WordCounter
{
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // Wörter = durch Whitespace getrennte Tokens mit mindestens einem Buchstaben/Ziffer
        var t = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var n = 0;
        foreach (var w in t)
            if (w.Any(char.IsLetterOrDigit)) n++;
        return n;
    }

    public static int CountSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var count = 0;
        var inSentence = false;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '"' || c == '»') inSentence = true;
            if ((c == '.' || c == '!' || c == '?') && inSentence) { count++; inSentence = false; }
        }
        if (inSentence) count++;
        return count;
    }

    public static int CountParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static int ReadingMinutes(string text) => Math.Max(0, (int)Math.Ceiling(CountWords(text) / 200.0));
    public static int ReadingSeconds(string text) => CountWords(text) * 60 / 200;
}
