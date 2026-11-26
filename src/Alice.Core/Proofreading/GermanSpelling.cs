// Deutsche Rechtschreibprüfung für ALICE (lokal, keine externen Aufrufe).
// Wortliste: igerman98-Hunspell-Wörter (GPLv2/GPLv3/OASIS, Bjoern Jacke + Franz
// Michael Baumann, Quelle hlt.sztaki.hu/resources/hunspell/German), auf die
// Basisformen ohne Affix-Flags reduziert (151 824 Formen, de_DE.words).
// Eigene/bekanntmachungswidrige Absichten ersetzt die Engine nicht still: AutoCorrect
// greift nur bei eindeutig sicheren Regeln (Doppelwort, Doppel-Leerzeichen,
// dreifache Buchstaben, Satzgroßschreibung). Alle Korrekturen laufen über TextRange
// und sind damit im RichTextBox-Undo (Ctrl+Z) rückgängig zu machen.

using System.Collections;

namespace Alice.Core.Proofreading;

/// <summary>Fehlertypen der lokalen Prüfung (Rechtschreib-/Tipp-/Interpunktion).</summary>
public enum ProofKind { Spelling, RepeatedWord, DoubleSpace, RepeatedChar, Capitalization, Punctuation }

/// <summary>Ein Prüf-Fund: Wort-Fundstelle im Text + Kategorie + Optional Vorschlag.</summary>
public sealed record ProofIssue(int Start, int Length, string Word, ProofKind Kind, string? Suggestion);

/// <summary>
/// Lokale де-DE-Prüfengine: Wortlisten-Abgleich + Tippfehler-Regeln.
/// Reine Logik (kein WPF) – testbar, kein Netzwerk, kein AI-Tag.
/// </summary>
public sealed class GermanSpelling
{
    private readonly HashSet<string> _builtin = new(StringComparer.Ordinal);
    private readonly HashSet<string> _personal = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _project = new(StringComparer.OrdinalIgnoreCase);

    public bool Enabled { get; set; } = true;
    public bool GrammarRules { get; set; } = true;

    public GermanSpelling()
    {
        foreach (var w in LoadBuiltInWords()) _builtin.Add(w);
    }

    private static IEnumerable<string> LoadBuiltInWords()
    {
        // eingebettete Ressourcen lesen (de_DE.words)
        var asm = typeof(GermanSpelling).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".words", StringComparison.OrdinalIgnoreCase)) continue;
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) continue;
            using var sr = new StreamReader(s, System.Text.Encoding.UTF8);
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (!string.IsNullOrWhiteSpace(line)) yield return line.Trim();
            }
        }
    }

    /// <summary>Persönliches Wörterbuch (eigene/fiktive Wörter).</summary>
    public void SetPersonalWords(IEnumerable<string> words)
    {
        _personal.Clear();
        foreach (var w in words) if (!string.IsNullOrWhiteSpace(w)) _personal.Add(w.Trim());
    }

    public IReadOnlyList<string> GetPersonalWords() => _personal.ToList();

    /// <summary>Projekt-Wörter automatisch einbeziehen (Figuren-/Welt-Namen).</summary>
    public void SetProjectWords(IEnumerable<string> words)
    {
        _project.Clear();
        foreach (var w in words) if (!string.IsNullOrWhiteSpace(w)) _project.Add(w.Trim());
    }

    public void AddPersonalWord(string word)
    {
        if (!string.IsNullOrWhiteSpace(word)) _personal.Add(word.Trim());
    }

    public bool IsKnownWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return true;
        var w = word.Trim();
        if (_personal.Contains(w) || _project.Contains(w)) return true;
        // Zahlwörter / reine Zeichenfolgen
        if (w.All(char.IsDigit)) return true;
        // Wortliste (wie Gedruckt + Kleinschreibung)
        if (_builtin.Contains(w)) return true;
        if (char.IsUpper(w[0]))
        {
            var lower = char.ToLowerInvariant(w[0]) + w.Substring(1);
            if (_builtin.Contains(lower)) return true;
            if (w.Length > 1 && char.IsUpper(w[1]) && _builtin.Contains(w.ToLowerInvariant())) return true;
        }
        else
        {
            var upper = char.ToUpperInvariant(w[0]) + w.Substring(1);
            if (_builtin.Contains(upper)) return true;
        }
        // Komposita: Bindestrich-Teile einzeln prüfen
        foreach (var part in w.Split('-'))
            if (part.Length > 2 && !IsKnownWordSingle(part)) return false;
        return false;
    }

    private bool IsKnownWordSingle(string w) => w != string.Empty && (_builtin.Contains(w) || _personal.Contains(w) || _project.Contains(w));

    /// <summary>Worttoken im Text finden (Buchstaben + Umlaute + ß + Binnen-Apostroph).</summary>
    public static System.Text.RegularExpressions.Regex TokenRegex { get; } =
        new System.Text.RegularExpressions.Regex(@"[\p{L}][\p{L}'’\-]*[\p{L}]|[\p{L}]", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Text prüfen: Rechtschreibfehler + Regel-Funde. maxIssues begrenzt die
    /// Rückgabe (für Live-Unterstreichung nur die relevante Menge).
    /// </summary>
    public IReadOnlyList<ProofIssue> CheckText(string text, int maxIssues = 500)
    {
        if (!Enabled || string.IsNullOrEmpty(text)) return Array.Empty<ProofIssue>();
        var issues = new List<ProofIssue>();
        var tokens = TokenRegex.Matches(text).Select(m => (Index: m.Index, Length: m.Length, Word: m.Value)).ToList();

        for (int i = 0; i < tokens.Count; i++)
        {
            var (idx, len, word) = tokens[i];
            if (word.Length <= 1) continue;
            var clean = word.Trim('\'', '’');
            if (clean.Length == 0) continue;
            if (!IsKnownWord(clean))
            {
                issues.Add(new ProofIssue(idx, len, clean, ProofKind.Spelling, Suggest(clean)?.FirstOrDefault()));
            }
            if (issues.Count >= maxIssues) return issues;
        }

        if (GrammarRules)
        {
            // Doppelwort: "die die" (case-insensitiv, Abstand dazwischen)
            for (int i = 1; i < tokens.Count; i++)
            {
                var a = tokens[i - 1];
                var b = tokens[i];
                if (a.Word.Length == 0 || b.Word.Length == 0) continue;
                if (string.Equals(a.Word, b.Word, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new ProofIssue(b.Index, b.Length, b.Word, ProofKind.RepeatedWord, string.Empty));
            }
            // Dreifache Buchstaben: "woort" (ittäin oo) -> nur wenn dadurch ein bekanntes Wort entsteht
            foreach (var (idx, len, word) in tokens)
            {
                if (word.Length < 4) continue;
                string? fixedWord = null;
                for (int k = 0; k < word.Length - 2 && fixedWord is null; k++)
                {
                    if (word[k] == word[k + 1] && word[k] == word[k + 2])
                    {
                        var cand = word.Remove(k, 1);
                        if (IsKnownWord(cand)) fixedWord = cand;
                    }
                }
                if (fixedWord is not null) issues.Add(new ProofIssue(idx, len, word, ProofKind.RepeatedChar, fixedWord));
            }
            // Doppel-Leerzeichen
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "  +"))
                issues.Add(new ProofIssue(m.Index, m.Length, "  ", ProofKind.DoubleSpace, " "));
            // Satzgroßschreibung: Kleinbuchstabe nach . ! ? + Leerzeichen
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"(?<=[.!?]\s)[a-zäöüß]"))
            {
                var up = char.ToUpperInvariant(m.Value[0]);
                issues.Add(new ProofIssue(m.Index, m.Length, m.Value, ProofKind.Capitalization, up.ToString()));
            }
        }
        return issues.OrderBy(i => i.Start).Take(maxIssues).ToList();
    }

    /// <summary>Kandidaten-Vorschläge für ein unbekanntes Wort (eingrenzung durch Anfangsbuchstaben + Länge + Edit-Distanz).</summary>
    public IReadOnlyList<string> Suggest(string word, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(word)) return Array.Empty<string>();
        var w = word.Trim();
        var candidates = new List<(string Word, int Dist)>();
        int maxDist = w.Length >= 7 ? 2 : (w.Length >= 4 ? 2 : 1);
        foreach (var dict in new[] { _personal, _project, _builtin })
        {
            foreach (var cand in dict)
            {
                if (cand.Length < w.Length - 2 || cand.Length > w.Length + 2) continue;
                if (char.ToLowerInvariant(cand[0]) != char.ToLowerInvariant(w[0])) continue;
                int d = Levenshtein(w.ToLowerInvariant(), cand.ToLowerInvariant());
                if (d <= maxDist) candidates.Add((cand, d));
                if (candidates.Count > 4000) break;
            }
            if (candidates.Count > 200) break;
        }
        return candidates.OrderBy(c => c.Dist).ThenBy(c => c.Word, StringComparer.Ordinal).Select(c => c.Word).Distinct().Take(max).ToList();
    }

    /// <summary>Automatische Korrektur-Optionen (sichere Regeln).</summary>
    public string AutoCorrect(string text)
    {
        if (!Enabled || !GrammarRules || string.IsNullOrEmpty(text)) return text;
        var outText = text;
        // Doppel-Leerzeichen -> einfach
        outText = System.Text.RegularExpressions.Regex.Replace(outText, "  +", " ");
        // Doppelwort entfernen
        outText = System.Text.RegularExpressions.Regex.Replace(outText, @"\b(\p{L}+)\s+\1\b", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Dreifache Buchstaben nur bei bekanntem Resultat
        outText = TokenRegex.Replace(outText, m =>
        {
            var word = m.Value;
            if (word.Length < 4) return word;
            for (int k = 0; k < word.Length - 2; k++)
            {
                if (word[k] == word[k + 1] && word[k] == word[k + 2])
                {
                    var cand = word.Remove(k, 1);
                    if (IsKnownWord(cand)) return cand;
                }
            }
            return word;
        });
        // Satzgroßschreibung
        outText = System.Text.RegularExpressions.Regex.Replace(outText, @"(?<=[.!?]\s)[a-zäöüß]", m => char.ToUpperInvariant(m.Value[0]).ToString());
        return outText;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = Enumerable.Range(0, b.Length + 1).ToArray();
        var cur = new int[b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[^1];
    }
}
