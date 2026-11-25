using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Alice.Core.Enums;
using Alice.Core.Models;
using Alice.Core.Services;

namespace Alice.Data;

/// <summary>Import/Export: TXT, Markdown, DOCX (via OpenXML-ZIP), EPUB (ZIP+HTML), PDF als sauberer Text-Druck-PDF (ohne externe PDF-Lib, Drucklayout).</summary>
public sealed class ImportExportService : IImportExportService
{
    public async Task<AliceProject> ImportAsync(string filePath, ImportFormat format, CancellationToken ct = default)
    {
        var title = Path.GetFileNameWithoutExtension(filePath);
        var project = new AliceProject { Title = title };
        string text = format switch
        {
            ImportFormat.Txt or ImportFormat.Markdown => await File.ReadAllTextAsync(filePath, ct),
            ImportFormat.Rtf => StripRtf(await File.ReadAllTextAsync(filePath, ct)),
            ImportFormat.Docx => await ReadDocxTextAsync(filePath, ct),
            _ => await File.ReadAllTextAsync(filePath, ct)
        };
        // Kapitelaufteilung an Markdown-Headings oder "Kapitel"
        var chapter = new ManuscriptNode { Title = "Kapitel 1", Kind = NodeKind.Chapter, SortOrder = 0 };
        project.Nodes.Add(chapter);
        var currentScene = new ManuscriptNode { Title = "Szene 1", Kind = NodeKind.Scene, ParentId = chapter.Id, SortOrder = 0 };
        project.Nodes.Add(currentScene);
        var sb = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("# ", StringComparison.Ordinal))
            {
                if (sb.Length > 0) { currentScene.Content = sb.ToString().Trim(); sb.Clear(); }
                var newChapter = new ManuscriptNode { Title = t[2..].Trim(), Kind = NodeKind.Chapter, SortOrder = project.Nodes.Count(n => n.Kind == NodeKind.Chapter) };
                project.Nodes.Add(newChapter);
                currentScene = new ManuscriptNode { Title = "Szene 1", Kind = NodeKind.Scene, ParentId = newChapter.Id, SortOrder = 0 };
                project.Nodes.Add(currentScene);
            }
            else sb.AppendLine(line);
            ct.ThrowIfCancellationRequested();
        }
        currentScene.Content = sb.ToString().Trim();
        return project;
    }

    public async Task ExportAsync(AliceProject project, string outputPath, ExportFormat format, CancellationToken ct = default) => await ExportAsync(project, outputPath, format, new ExportOptions(), ct);

    public async Task ExportAsync(AliceProject project, string outputPath, ExportFormat format, ExportOptions options, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath))!; Directory.CreateDirectory(dir);
        var manuscript = BuildFullText(project, options);
        switch (format)
        {
            case ExportFormat.Txt: await File.WriteAllTextAsync(outputPath, manuscript, ct); break;
            case ExportFormat.Markdown:
                await File.WriteAllTextAsync(outputPath, BuildMarkdown(project, options), ct); break;
            case ExportFormat.Docx: await WriteDocxAsync(project, outputPath, ct, options); break;
            case ExportFormat.Epub: await WriteEpubAsync(project, outputPath, ct); break;
            case ExportFormat.Pdf: await WritePdfAsync(project, outputPath, ct, options); break;
            case ExportFormat.Rtf: await WriteRtfAsync(project, outputPath, ct, options); break;
            case ExportFormat.Odt: await WriteOdtAsync(project, outputPath, ct, options); break;
            case ExportFormat.Html: await WriteHtmlAsync(project, outputPath, ct, options); break;
            case ExportFormat.Zip: await WriteZipAsync(project, outputPath, ct); break;
        }
        // Verifikation: Datei muss existieren und nicht leer sein
        var info = new FileInfo(outputPath);
        if (!info.Exists || info.Length == 0) throw new IOException("Export fehlgeschlagen: Datei leer oder fehlend.");
    }

    private static string BuildFullText(AliceProject p, ExportOptions options)
    {
        var manuscript = BuildManuscriptText(p, includeNotes: false, includeBoards: options.Scope != "Nur Storyboard");
        return options.Scope == "Nur Storyboard" ? BuildBoardsText(p) : manuscript;
    }

    private static string BuildBoardsText(AliceProject p)
    {
        var sb = new StringBuilder();
        foreach (var wb in p.Whiteboards)
        {
            sb.AppendLine($"Storyboard: {wb.Title}"); sb.AppendLine(new string('-', wb.Title.Length + 12));
            foreach (var n in wb.Nodes.OrderBy(n => n.IsBookPanel ? 0 : 1))
            {
                sb.AppendLine($"[{BoardNodeTypes.LabelOf(n.NodeType)}] {n.Title}");
                if (!string.IsNullOrWhiteSpace(n.Text)) sb.AppendLine(n.Text);
            }
            foreach (var c in wb.Connections)
            {
                var a = wb.Nodes.FirstOrDefault(n => n.Id == c.FromNodeId); var b = wb.Nodes.FirstOrDefault(n => n.Id == c.ToNodeId);
                if (a is not null && b is not null) sb.AppendLine($"  {a.Title} --[{BoardConnectionKinds.LabelOf(c.Kind)}{(string.IsNullOrWhiteSpace(c.Label) ? "" : ": " + c.Label)}]--> {b.Title}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildManuscriptText(AliceProject p, bool includeNotes, bool includeBoards = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine(p.Title); sb.AppendLine(p.Author); sb.AppendLine(new string('=', Math.Max(10, p.Title.Length)));
        if (!string.IsNullOrWhiteSpace(p.Synopsis)) { sb.AppendLine(); sb.AppendLine(p.Synopsis); }
        foreach (var ch in p.Nodes.Where(n => n.Kind == NodeKind.Chapter).OrderBy(n => n.SortOrder))
        {
            sb.AppendLine(); sb.AppendLine(); sb.AppendLine(ch.Title); sb.AppendLine(new string('-', ch.Title.Length));
            foreach (var sc in p.Nodes.Where(n => n.ParentId == ch.Id).OrderBy(n => n.SortOrder))
            {
                sb.AppendLine(); sb.AppendLine($"* * *"); sb.AppendLine();
                sb.AppendLine(sc.Content);
                if (includeNotes && !string.IsNullOrWhiteSpace(sc.Notes)) sb.AppendLine($"\n[Notiz: {sc.Notes}]");
            }
        }
        // lose Szenen ohne Kapitel
        foreach (var sc in p.Nodes.Where(n => (n.Kind == NodeKind.Scene || n.Kind == NodeKind.SubScene) && (n.ParentId == null || p.Find(n.ParentId.Value)?.Kind != NodeKind.Chapter)).OrderBy(n => n.SortOrder))
        { sb.AppendLine(); sb.AppendLine(sc.Content); }
        // Storyboard-Boards (Panels + Verbindungen) als Anhang
        if (includeBoards) foreach (var wb in p.Whiteboards)
        {
            sb.AppendLine(); sb.AppendLine(); sb.AppendLine($"Storyboard: {wb.Title}"); sb.AppendLine(new string('-', wb.Title.Length + 12));
            foreach (var n in wb.Nodes.OrderBy(n => n.IsBookPanel ? 0 : 1))
            {
                sb.AppendLine($"[{BoardNodeTypes.LabelOf(n.NodeType)}] {n.Title}");
                if (!string.IsNullOrWhiteSpace(n.Text)) sb.AppendLine(n.Text);
                if (!string.IsNullOrWhiteSpace(n.Status) || !string.IsNullOrWhiteSpace(n.Tags)) sb.AppendLine($"  Status: {n.Status}  Tags: {n.Tags}".Trim());
            }
            foreach (var c in wb.Connections)
            {
                var a = wb.Nodes.FirstOrDefault(n => n.Id == c.FromNodeId); var b = wb.Nodes.FirstOrDefault(n => n.Id == c.ToNodeId);
                if (a is not null && b is not null) sb.AppendLine($"  {a.Title} --[{BoardConnectionKinds.LabelOf(c.Kind)}{(string.IsNullOrWhiteSpace(c.Label) ? "" : ": " + c.Label)}]--> {b.Title}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildMarkdown(AliceProject p, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        if (options.Scope == "Nur Storyboard") return "# Storyboard\n\n" + BuildBoardsText(p);
        var sb = new StringBuilder();
        sb.AppendLine($"# {p.Title}\n\n*{p.Author}*");
        if (!string.IsNullOrWhiteSpace(p.Synopsis)) sb.AppendLine($"\n> {p.Synopsis}");
        foreach (var ch in p.Nodes.Where(n => n.Kind == NodeKind.Chapter).OrderBy(n => n.SortOrder))
        {
            sb.AppendLine($"\n## {ch.Title}");
            foreach (var sc in p.Nodes.Where(n => n.ParentId == ch.Id).OrderBy(n => n.SortOrder))
            { sb.AppendLine("\n* * *\n"); sb.AppendLine(sc.Content); }
        }
        return sb.ToString();
    }

    private static string StripRtf(string rtf)
    {
        var sb = new StringBuilder(); var skip = 0; var brace = 0;
        foreach (var c in rtf)
        {
            if (c == '{') brace++;
            else if (c == '}') brace = Math.Max(0, brace - 1);
            else if (brace == 0) sb.Append(c);
        }
        return sb.ToString();
    }

    private static async Task<string> ReadDocxTextAsync(string path, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("Keine document.xml in DOCX gefunden.");
        using var s = entry.Open(); using var r = new StreamReader(s, Encoding.UTF8);
        var xml = await r.ReadToEndAsync(ct);
        var doc = XDocument.Parse(xml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paras = doc.Descendants(w + "p").Select(p => string.Join("", p.Descendants(w + "t").Select(t => t.Value)));
        return string.Join("\n", paras);
    }

    private static async Task WriteDocxAsync(AliceProject p, string path, CancellationToken ct, ExportOptions? options = null)
    {
        var text = BuildFullText(p, options ?? new ExportOptions());
        var paras = text.Split('\n').Select(l => $"<w:p><w:r><w:t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(l)}</w:t></w:r></w:p>").ToList();
        var docXml = $"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>{string.Join("", paras)}</w:body></w:document>";
        var contentTypes = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>";
        var rels = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            var e = zip.CreateEntry(name); using var s = e.Open(); using var w = new StreamWriter(s, Encoding.UTF8); w.Write(content);
        }
        Add("[Content_Types].xml", contentTypes); Add("_rels/.rels", rels); Add("word/document.xml", docXml);
        await Task.CompletedTask;
    }

    private static async Task WriteEpubAsync(AliceProject p, string path, CancellationToken ct)
    {
        var text = System.Security.SecurityElement.Escape(BuildManuscriptText(p, false));
        var xhtml = $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>{System.Security.SecurityElement.Escape(p.Title)}</title></head><body><h1>{System.Security.SecurityElement.Escape(p.Title)}</h1><pre>{text}</pre></body></html>";
        var container = "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>";
        var opf = $"<package xmlns=\"http://www.idpf.org/2007/opf\" unique-identifier=\"bookid\" version=\"2.0\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>{System.Security.SecurityElement.Escape(p.Title)}</dc:title><dc:creator>{System.Security.SecurityElement.Escape(p.Author)}</dc:creator><dc:language>de</dc:language></metadata><manifest><item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/><item id=\"content\" href=\"content.xhtml\" media-type=\"application/xhtml+xml\"/></manifest><spine toc=\"ncx\"><itemref idref=\"content\"/></spine></package>";
        var ncx = "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\"><head><meta name=\"dtb:uid\" content=\"alice\"/></head><docTitle><text>ALICE</text></docTitle><navMap><navPoint id=\"np1\" playOrder=\"1\"><navLabel><text>Manuskript</text></navLabel><content src=\"content.xhtml\"/></navPoint></navMap></ncx>";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        // mimetype zuerst, unkomprimiert
        var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression); using (var s = mime.Open()) { var b = Encoding.ASCII.GetBytes("application/epub+zip"); s.Write(b, 0, b.Length); }
        void Add(string name, string content)
        {
            var e = zip.CreateEntry(name); using var s = e.Open(); using var w = new StreamWriter(s, Encoding.UTF8); w.Write(content);
        }
        Add("META-INF/container.xml", container); Add("OEBPS/content.opf", opf); Add("OEBPS/toc.ncx", ncx); Add("OEBPS/content.xhtml", xhtml);
        await Task.CompletedTask;
    }

    private static async Task WritePdfAsync(AliceProject p, string path, CancellationToken ct, ExportOptions? options = null)
    {
        // Minimales valides PDF 1.4 ohne externe Lib, mit Optionen (Titelblatt, TOC, Seitenzahlen, Kapitel-Umbrüche, Format, Schriftgröße)
        options ??= new ExportOptions();
        var text = BuildFullText(p, options);
        var isLetter = options.PageFormat == "Letter";
        var (pageW, pageH, topY) = isLetter ? (612, 792, 740) : (595, 842, 800);
        var perPage = 45;
        var chapterTitles = p.Nodes.Where(n => n.Kind == NodeKind.Chapter).Select(n => n.Title).ToHashSet();
        var rawLines = Wrap(text, 85).Take(4000).ToList();
        // Layout: KapitelneueSeite + Titelblatt + TOC einbauen
        var lines = new List<string>();
        if (options.TitlePage)
        {
            lines.AddRange(Enumerable.Repeat("", 18));
            lines.Add(p.Title); lines.Add(p.Author); lines.Add(DateTime.Now.ToString("dd.MM.yyyy"));
            lines.AddRange(Enumerable.Repeat("", Math.Max(1, perPage - (18 + 3))));
        }
        if (options.IncludeToc)
        {
            lines.Add("Inhaltsverzeichnis");
            foreach (var ch in p.Nodes.Where(n => n.Kind == NodeKind.Chapter).OrderBy(n => n.SortOrder)) lines.Add(ch.Title);
            lines.Add("");
        }
        foreach (var l in rawLines)
        {
            if (options.ChapterNewPage && chapterTitles.Contains(l.Trim()) && lines.Count % perPage != 0)
                while (lines.Count % perPage != 0) lines.Add("");
            lines.Add(l);
        }
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var pages = new List<int>();
        sb.Append("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");
        sb.Append("2 0 obj<< /Type /Pages /Kids [] >>endobj\n");
        var objNum = 3; var pageObjs = new List<string>();
        int pageNo = 0;
        for (int i = 0; i < lines.Count; i += perPage)
        {
            pageNo++;
            var chunk = lines.Skip(i).Take(perPage).ToList();
            var content = $"BT /F1 {options.FontSize} Tf 50 {topY} Td ";
            content += string.Join(" ", chunk.Select(l => $"({EscapePdf(l)}) Tj 0 -14 Td "));
            if (options.PageNumbers) content += $" 1 0 0 1 280 20 Tm ({pageNo}) Tj";
            content += " ET";
            var pageId = objNum++; var contentId = objNum++;
            pages.Add(pageId);
            pageObjs.Add($"{pageId} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageW} {pageH}] /Contents {contentId} 0 R /Resources << /Font << /F1 9 0 R >> >> >>endobj");
            pageObjs.Add($"{contentId} 0 obj<< /Length {content.Length} >>stream\n{content}\nendstream endobj");
        }
        foreach (var o in pageObjs) sb.Append(o + "\n");
        var fontId = objNum++;
        sb.Append($"{fontId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");
        var final = sb.ToString().Replace("/Kids []", $"/Kids [{string.Join(" ", pages.Select(x => $"{x} 0 R"))}] /Count {pages.Count}").Replace("/F1 9 0 R", $"/F1 {fontId} 0 R");
        await File.WriteAllTextAsync(path, final, Encoding.ASCII, ct);
    }

    private static string RtfEscape(string s) => s.Replace(@"\", @"\\").Replace("{", @"\{").Replace("}", @"\}").Replace("\n", @"\par\n");

    private static async Task WriteRtfAsync(AliceProject p, string path, CancellationToken ct, ExportOptions options)
    {
        var text = BuildFullText(p, options);
        var rtf = "{\\rtf1\\ansi\\deff0\\deflang1031 {\\fonttbl{\\f0\\fnil\\fcharset0 Segoe UI;}}\n{\\colortbl ;\\red0\\green0\\blue0;}\n" +
            "{\\viewkind4\\uc1\\pard\\lang1031\\f0\\fs24 " + RtfEscape(text).Replace("\n", "\\par\n") + "}\n";
        await File.WriteAllTextAsync(path, rtf, Encoding.UTF8, ct);
    }

    private static async Task WriteOdtAsync(AliceProject p, string path, CancellationToken ct, ExportOptions options)
    {
        var text = System.Security.SecurityElement.Escape(BuildFullText(p, options));
        var paras = string.Join("", text.Split('\n').Select(l => $"<text:p>{l}</text:p>"));
        var content = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><office:body><office:text>{paras}</office:text></office:body></office:document-content>";
        var manifest = "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string c) { var e = zip.CreateEntry(name); using var s = e.Open(); using var w = new StreamWriter(s, Encoding.UTF8); w.Write(c); }
        Add("mimetype", "application/vnd.oasis.opendocument.text"); Add("content.xml", content); Add("META-INF/manifest.xml", manifest);
        await Task.CompletedTask;
    }

    private static async Task WriteHtmlAsync(AliceProject p, string path, CancellationToken ct, ExportOptions options)
    {
        var esc = System.Security.SecurityElement.Escape(BuildFullText(p, options));
        var body = string.Join("", esc.Split('\n').Select(l => string.IsNullOrWhiteSpace(l) ? "<br/>" : "<p>" + l + "</p>"));
        var html = $"<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>{System.Security.SecurityElement.Escape(p.Title)}</title></head><body><h1>{System.Security.SecurityElement.Escape(p.Title)}</h1><p><em>{System.Security.SecurityElement.Escape(p.Author)}</em></p>{body}</body></html>";
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct);
    }

    private static async Task WriteZipAsync(AliceProject p, string path, CancellationToken ct)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string c) { var e = zip.CreateEntry(name); using var s = e.Open(); using var w = new StreamWriter(s, Encoding.UTF8); w.Write(c); }
        Add("alice-project.json", System.Text.Json.JsonSerializer.Serialize(p, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Add("manuskript.md", BuildMarkdown(p));
        Add("storyboard.md", "# Storyboard\n\n" + BuildBoardsText(p));
        Add("README.txt", $"ALICE Projekt: {p.Title} von {p.Author}\nExport {DateTime.Now:yyyy-MM-dd HH:mm}\n");
        await Task.CompletedTask;
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var para in text.Split('\n'))
        {
            var l = para;
            while (l.Length > width) { var cut = l.LastIndexOf(' ', width); if (cut <= 0) cut = width; yield return l[..cut]; l = l[cut..].TrimStart(); }
            yield return l;
        }
    }
    private static string EscapePdf(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
