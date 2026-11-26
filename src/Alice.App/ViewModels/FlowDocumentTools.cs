using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using Alice.Core.Models;

namespace Alice.App.ViewModels;

/// <summary>
/// Umwandlung zwischen FlowDocument (seitenbasiertes, formatiertes Bearbeiten)
/// und den bestehenden Persistenz-Formaten:
/// - Klartext (ManuscriptNode.Content) bleibt Source of Truth für Export, Suche, AI und Zähler.
/// - XAML-Entwurf (ManuscriptNode.ContentRich) erhält Fett/Kursiv/Überschriften etc.
/// Zwischen den Editor-Instanzen gibt es KEINEN separaten Textpeicher: das FlowDocument ist
/// das eine Autorenobjekt; der Klartext wird nur abgeleitet (alles TextRange-getrieben).
///
/// Kapitel-Struktur (Buch-Anmutung): Das Editor-Dokument eines ManuscriptNode wird aus
/// [Kapitel-Titelseite (generiert, Tag ALICE-CH*)] + [Schreibseiten (Inhalt)] kompiliert.
/// Die Kapitel-Titelseite ist nur Darstellung; persistiert wird ausschließlich der Inhalt
/// (ToPlainText/ToXaml entfernen die Heading-Blöcke automatisch). DieHeading-Nummer/Titel
/// wird aus node.Title abgeleitet; eine bearbeitete Titel-Zeile im Dokument wird beim
/// nächsten Sync nach node.Title zurückschreiben (Rename-Sync).
/// </summary>
public static class FlowDocumentTools
{
    /// <summary>Eindeutiger Tag-Präfix für generierte Kapitel-Heading-Blöcke (Trennlinie für Strip + Titel-Sync).</summary>
    public const string ChapterTagPrefix = "ALICE-CH";
    public const string ChapterNumTag = "ALICE-CH-NUM";
    public const string ChapterTitleTag = "ALICE-CH-TITLE";
    private const string ChapterSpacerTag = "ALICE-CH-SP";
    private const string ChapterDividerTag = "ALICE-CH-DIV";
    private const string ChapterEmptyBodyTag = "ALICE-CH-BODY";

    public static FlowDocument FromPlainText(string plain)
    {
        var doc = new FlowDocument();
        var text = plain ?? string.Empty;
        if (text.Length == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run(string.Empty)));
            return doc;
        }
        // Absätze an Zeilenumbrüchen trennen; leere Absätze bleiben erhalten.
        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var line in lines) doc.Blocks.Add(new Paragraph(new Run(line)));
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Paragraph(new Run(string.Empty)));
        return doc;
    }

    public static FlowDocument FromRichXaml(string? xaml, string plainFallback)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return FromPlainText(plainFallback);
        try
        {
            using var sr = new StringReader(xaml);
            using var xr = System.Xml.XmlReader.Create(sr);
            var loaded = XamlReader.Load(xr) as FlowDocument;
            if (loaded is not null && loaded.Blocks.Count > 0) return loaded;
        }
        catch { /* Fallback auf Klartext */ }
        return FromPlainText(plainFallback);
    }

    /// <summary>Ist dieser Block ein generierter Kapitel-Heading-Block?</summary>
    public static bool IsChapterHeaderBlock(Block block) => (block as FrameworkContentElement)?.Tag is string tag && tag.StartsWith(ChapterTagPrefix, StringComparison.Ordinal);

    /// <summary>Hat dieses FlowDocument eine Kapitel-Titelseite (wurde mit CompileChapterDocument erstellt)?</summary>
    public static bool HasChapterHeader(FlowDocument doc) => doc.Blocks.Count > 0 && IsChapterHeaderBlock(doc.Blocks.First());

    /// <summary>Kapitel-Titel in "Kapitel N"-Präfix und anzeigbaren Titel aufspalten.</summary>
    public static (string Number, string DisplayTitle) SplitChapterTitle(string? title)
    {
        var t = (title ?? string.Empty).Trim();
        var m = System.Text.RegularExpressions.Regex.Match(t, @"^Kapitel\s+(\d+)\s*[-–—:]\s*(.+)$");
        if (m.Success) return ("Kapitel " + m.Groups[1].Value, m.Groups[2].Value.Trim());
        return (string.Empty, t.Length == 0 ? "Neues Kapitel" : t);
    }

    /// <summary>Kapitel-Dokument mit Inhalt aus dem Knoten (ContentRich/Content).</summary>
    public static FlowDocument CompileChapterDocument(ManuscriptNode node)
        => CompileChapterDocumentWithBody(node, FromRichXaml(node.ContentRich, node.Content ?? string.Empty));

    /// <summary>Kapitel-Dokument mit vorgegebenem Schreibseiten-Inhalt (z. B. Snapshot-Restore aus Klartext).</summary>
    public static FlowDocument CompileChapterDocumentWithBody(ManuscriptNode node, FlowDocument bodyDoc)
    {
        var doc = bodyDoc;
        var compiled = new FlowDocument();
        var (number, displayTitle) = SplitChapterTitle(node.Title);

        // Abstand, damit der Titel in der Seitenvertikale rustig sitzt.
        for (int i = 0; i < 7; i++)
        {
            var sp = new Paragraph(new Run(string.Empty)) { Tag = ChapterSpacerTag, FontSize = 24, TextAlignment = TextAlignment.Center, Margin = new Thickness(0) };
            compiled.Blocks.Add(sp);
        }
        // Grosse Kapitel-Ueberschrift "KAPITEL N" (oder der Titel selbst) + optionaler Untertitel.
        var heading = string.IsNullOrWhiteSpace(number) ? displayTitle : number;
        var num = new Paragraph(new Run(heading)) { Tag = ChapterNumTag, FontSize = 28, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        compiled.Blocks.Add(num);
        var subtitleText = string.IsNullOrWhiteSpace(number) ? string.Empty : displayTitle;
        if (!string.IsNullOrWhiteSpace(subtitleText))
        {
            var title = new Paragraph(new Run(subtitleText)) { Tag = ChapterTitleTag, FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            compiled.Blocks.Add(title);
        }
        var div = new Paragraph(new Run("· · ·")) { Tag = ChapterDividerTag, FontSize = 14, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        compiled.Blocks.Add(div);

        // Inhalt anhängen; der erste Inhalts-Block beginnt per Seitenumbruch auf der Schreibseite.
        var body = doc.Blocks.ToList();
        foreach (var b in body) { doc.Blocks.Remove(b); if (!IsChapterHeaderBlock(b)) compiled.Blocks.Add(CloneBlock(b)); }
        if (compiled.Blocks.Count > 0)
        {
            var firstBody = compiled.Blocks.SkipWhile(b => IsChapterHeaderBlock(b)).FirstOrDefault();
            if (firstBody is Paragraph p0) p0.BreakPageBefore = true;
        }
        if (!compiled.Blocks.SkipWhile(b => IsChapterHeaderBlock(b)).Any())
        {
            compiled.Blocks.Add(new Paragraph(new Run(string.Empty)) { Tag = ChapterEmptyBodyTag, BreakPageBefore = true });
        }
        return compiled;
    }

    /// <summary>
    /// Ein Dokument nur mit dem Schreib-Inhalt (ohne generierte Kapitel-Titelseite).
    /// Hauptblöcke werden geklont (Absätze mit Run-Formatierung, Listen mit Marker-Stil).
    /// </summary>
    public static FlowDocument BodyOnly(FlowDocument doc)
    {
        if (!HasChapterHeader(doc)) return doc;
        var body = new FlowDocument();
        foreach (var block in doc.Blocks)
        {
            if (IsChapterHeaderBlock(block)) continue;
            body.Blocks.Add(CloneBlock(block));
        }
        if (body.Blocks.Count == 0) body.Blocks.Add(new Paragraph(new Run(string.Empty)));
        return body;
    }

    /// <summary>
    /// Bearbeitete Kapitel-Titelzeile lesen: neuer vollständiger Knoten-Titel oder null,
    /// wenn unverändert/leer (nichts zu übernehmen).
    /// </summary>
    public static string? ExtractEditedChapterTitle(FlowDocument doc, string? currentTitle)
    {
        if (!HasChapterHeader(doc) || string.IsNullOrWhiteSpace(currentTitle)) return null;
        string? headingText = null, subtitleText = null;
        foreach (var block in doc.Blocks)
        {
            if (block is not Paragraph p) continue;
            var tag = p.Tag as string;
            if (tag == ChapterNumTag) headingText = new TextRange(p.ContentStart, p.ContentEnd).Text.Trim();
            else if (tag == ChapterTitleTag) subtitleText = new TextRange(p.ContentStart, p.ContentEnd).Text.Trim();
        }
        var (number, displayTitle) = SplitChapterTitle(currentTitle);
        var headingExpected = string.IsNullOrWhiteSpace(number) ? displayTitle : number;
        var subtitleExpected = string.IsNullOrWhiteSpace(number) ? string.Empty : displayTitle;
        bool headingChanged = headingText is not null && !string.Equals(headingText, headingExpected, StringComparison.Ordinal);
        bool subtitleChanged = (subtitleText ?? string.Empty) != subtitleExpected;
        if (!headingChanged && !subtitleChanged) return null;
        var newNumber = headingChanged && headingText is not null ? headingText : number;
        var newSub = subtitleChanged ? (subtitleText ?? string.Empty) : subtitleExpected;
        if (string.IsNullOrWhiteSpace(newNumber)) return string.IsNullOrWhiteSpace(newSub) ? null : newSub;
        if (string.IsNullOrWhiteSpace(newSub)) return newNumber;
        return newNumber + ": " + newSub;
    }

    // ---------- Rechtschreib-Dekorationen (rote Unterstreichung) ----------
    /// <summary>
    /// Rechtschreib-Anzeige: unbekannte Wörter in den Schreib-Absätzen mit roter
    /// Unterstreichung markieren. Absätze mit dem Cursor (der den Cursor trägt)
    /// werden übersprungen (Cursor bleibt stabil); Kapitel-Heading bleibt unberührt.
    /// </summary>
    public static void ApplySpellDecorations(FlowDocument doc, Alice.Core.Proofreading.GermanSpelling spell, Paragraph? skipParagraph = null)
    {
        if (!spell.Enabled) return;
        var paragraphs = doc.Blocks.OfType<Paragraph>().ToList();
        foreach (var p in paragraphs)
        {
            if (IsChapterHeaderBlock(p) || ReferenceEquals(p, skipParagraph)) continue;
            var text = new TextRange(p.ContentStart, p.ContentEnd).Text;
            if (text.Length == 0 || text.Length > 8000) continue;
            var issues = spell.CheckText(text, maxIssues: 60)
                .Where(i => i.Kind == Alice.Core.Proofreading.ProofKind.Spelling)
                .ToList();
            if (issues.Count == 0) continue;
            // Inlines neu aufbauen: unbekannte Wörter (roter Underline-Run), Rest mit Original-Formatierung.
            var inlines = SegmentParagraphInlines(p, issues);
            foreach (var old in p.Inlines.ToList()) p.Inlines.Remove(old);
            foreach (var inl in inlines) p.Inlines.Add(inl);
        }
    }

    private static List<Inline> SegmentParagraphInlines(Paragraph source, List<Alice.Core.Proofreading.ProofIssue> issues)
    {
        // Quelle in Runs zerlegen (Text + Formatvorlage + Spannen)
        var sourceRuns = source.Inlines
            .Select(r => r as Run ?? new Run(new TextRange(r.ElementStart, r.ElementEnd).Text) { FontFamily = source.FontFamily, FontSize = source.FontSize })
            .ToList();
        var sourceText = string.Concat(sourceRuns.Select(r => r.Text));
        var result = new List<Inline>();
        int pos = 0;
        foreach (var issue in issues.OrderBy(i => i.Start))
        {
            if (issue.Start < pos) continue;
            EmitSegment(result, sourceRuns, sourceText, pos, issue.Start, mark: false);
            var wordRun = BuildRunFor(sourceRuns, sourceText, issue.Start, issue.Start + issue.Length);
            wordRun.Foreground = System.Windows.Media.Brushes.Red;
            wordRun.TextDecorations = System.Windows.TextDecorations.Underline;
            result.Add(wordRun);
            pos = issue.Start + issue.Length;
        }
        EmitSegment(result, sourceRuns, sourceText, pos, sourceText.Length, mark: false);
        return result;
    }

    private static void EmitSegment(List<Inline> outList, List<Run> src, string srcText, int from, int to, bool mark)
    {
        if (to <= from) return;
        int offset = 0;
        for (int i = 0; i < src.Count; i++)
        {
            var run = src[i];
            int s = offset, e = offset + run.Text.Length;
            offset = e;
            int lo = Math.Max(s, from), hi = Math.Min(e, to);
            if (hi <= lo) continue;
            var clone = CloneRun(run);
            clone.Text = srcText.Substring(lo, hi - lo);
            outList.Add(clone);
        }
    }

    private static Run BuildRunFor(List<Run> src, string srcText, int from, int to)
    {
        int offset = 0;
        for (int i = 0; i < src.Count; i++)
        {
            var run = src[i];
            int s = offset, e = offset + run.Text.Length;
            offset = e;
            if (to <= s || from >= e) continue;
            var clone = CloneRun(run);
            clone.Text = srcText.Substring(Math.Max(s, from), Math.Min(e, to) - Math.Max(s, from));
            return clone;
        }
        return new Run(srcText.Substring(from, to - from));
    }

    private static Block CloneBlock(Block block) => block switch
    {
        Paragraph p => CloneParagraph(p),
        List l => CloneList(l),
        _ => CloneParagraph(new Paragraph(new Run(PlainTextOf(block))))
    };

    private static Paragraph CloneParagraph(Paragraph p)
    {
        var clone = new Paragraph { TextAlignment = p.TextAlignment, FontFamily = p.FontFamily, FontSize = p.FontSize, FontWeight = p.FontWeight, FontStyle = p.FontStyle };
        foreach (var inline in p.Inlines)
        {
            switch (inline)
            {
                case Run r: clone.Inlines.Add(CloneRun(r)); break;
                case LineBreak: clone.Inlines.Add(new LineBreak()); break;
                default: clone.Inlines.Add(new Run(new TextRange(inline.ElementStart, inline.ElementEnd).Text)); break;
            }
        }
        return clone;
    }

    private static List CloneList(List list)
    {
        var clone = new List { MarkerStyle = list.MarkerStyle };
        foreach (var item in list.ListItems)
        {
            var li = new ListItem();
            foreach (var block in item.Blocks)
            {
                if (block is Paragraph p) li.Blocks.Add(CloneParagraph(p));
            }
            clone.ListItems.Add(li);
        }
        return clone;
    }

    private static Run CloneRun(Run r) => new Run(r.Text)
    {
        FontFamily = r.FontFamily, FontSize = r.FontSize, FontWeight = r.FontWeight, FontStyle = r.FontStyle,
        Foreground = r.Foreground, Background = r.Background, TextDecorations = r.TextDecorations
    };

    private static string PlainTextOf(Block block) => new TextRange(block.ContentStart, block.ContentEnd).Text;

    public static string ToXaml(FlowDocument doc)
    {
        try { var xaml = XamlWriter.Save(BodyOnly(doc)); return string.IsNullOrWhiteSpace(xaml) ? string.Empty : xaml; }
        catch { return string.Empty; }
    }

    /// <summary>Abgeleiteter Klartext (für Zähler/Export/Suche/AI). Finalen Absatzumbruch entfernt. Kapitel-Titelseite ist NICHT Teil des Manuskript-Textes.</summary>
    public static string ToPlainText(FlowDocument doc)
    {
        var d = BodyOnly(doc);
        var text = new TextRange(d.ContentStart, d.ContentEnd).Text;
        return text.TrimEnd('\r', '\n');
    }

    /// <summary>Leeren Absatz ans Dokument hängen (für „Next Page" auf der letzten Seite).</summary>
    public static void AppendEmptyParagraph(FlowDocument doc) => doc.Blocks.Add(new Paragraph(new Run(string.Empty)));

    /// <summary>Absatzbasiertes Find &amp; Replace (Formatierung rund um den Treffer bleibt).</summary>
    public static int ReplaceAll(FlowDocument doc, string find, string replace, bool caseSensitive)
    {
        if (doc is null || string.IsNullOrEmpty(find)) return 0;
        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int total = 0;
        foreach (var paragraph in doc.Blocks.OfType<Paragraph>())
        {
            if (IsChapterHeaderBlock(paragraph)) continue;
            var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
            string text = range.Text;
            if (text.IndexOf(find, cmp) < 0) continue;
            int count = 0;
            var builder = new System.Text.StringBuilder();
            int pos = 0;
            int idx;
            while ((idx = text.IndexOf(find, pos, cmp)) >= 0)
            {
                builder.Append(text, pos, idx - pos);
                builder.Append(replace);
                pos = idx + find.Length;
                count++;
            }
            builder.Append(text, pos, text.Length - pos);
            if (count > 0) { range.Text = builder.ToString(); total += count; }
        }
        return total;
    }
}
