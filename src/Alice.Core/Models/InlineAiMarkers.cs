using System.Text.RegularExpressions;

namespace Alice.Core.Models;

/// <summary>Inline-KI-Marker im Manuskript: {{AI: Anweisung}} – werden nie automatisch
/// an die KI gesendet, im Export entfernt, nur auf ausdrücklichen Wunsch ausgeführt.</summary>
public static class InlineAiMarkers
{
    private static readonly Regex Marker = new(@"\{\{AI:(.*?)\}\}", RegexOptions.Singleline | RegexOptions.Compiled);

    public sealed record Found(int Index, int Length, string Instruction);

    /// <summary>Alle abgeschlossenen Marker finden (unabgeschlossene bleiben unangetastet).</summary>
    public static IReadOnlyList<Found> Extract(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return Marker.Matches(text).Select(m => new Found(m.Index, m.Length, m.Groups[1].Value.Trim())).ToList();
    }

    /// <summary>Abgeschlossene Marker aus dem Export entfernen (Anweisung nicht mit exportieren).</summary>
    public static string Strip(string text) => string.IsNullOrEmpty(text) ? text : Marker.Replace(text, string.Empty);

    /// <summary>Marker erzeugen (für „Marker einfügen“).</summary>
    public static string Create(string instruction) => "{{AI:" + (instruction ?? string.Empty).Trim() + "}}";
}
