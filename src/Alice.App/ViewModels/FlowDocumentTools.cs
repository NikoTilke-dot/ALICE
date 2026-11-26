using System;
using System.IO;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Markup;

namespace Alice.App.ViewModels;

/// <summary>
/// Umwandlung zwischen FlowDocument (seitenbasiertes, formatiertes Bearbeiten)
/// und den bestehenden Persistenz-Formaten:
/// - Klartext (ManuscriptNode.Content) bleibt Source of Truth für Export, Suche, AI und Zähler.
/// - XAML-Entwurf (ManuscriptNode.ContentRich) erhält Fett/Kursiv/Überschriften etc.
/// Zwischen den Editor-Instanzen gibt es KEINEN separaten Textpeicher: das FlowDocument ist
/// das eine Autorenobjekt; der Klartext wird nur abgeleitet (alles TextRange-getrieben).
/// </summary>
public static class FlowDocumentTools
{
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

    public static string ToXaml(FlowDocument doc)
    {
        try { var xaml = XamlWriter.Save(doc); return string.IsNullOrWhiteSpace(xaml) ? string.Empty : xaml; }
        catch { return string.Empty; }
    }

    /// <summary>Abgeleiteter Klartext (für Zähler/Export/Suche/AI). Finalen Absatzumbruch entfernt.</summary>
    public static string ToPlainText(FlowDocument doc)
    {
        var text = new TextRange(doc.ContentStart, doc.ContentEnd).Text;
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
