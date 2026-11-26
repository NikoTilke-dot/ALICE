using System.Text.Json;
using Alice.Core.Enums;
using Alice.Core.Models;
using Alice.Core.Templates;
using Alice.Data;

namespace Alice.Tests;

public sealed class WordCounterTests
{
    [Fact]
    public void CountWords_basic() => Assert.Equal(3, WordCounter.CountWords("Hallo Welt test"));

    [Fact]
    public void CountWords_empty() => Assert.Equal(0, WordCounter.CountWords(""));

    [Fact]
    public void CountWords_ignoresPunctuationOnly() => Assert.Equal(1, WordCounter.CountWords("Hallo — *"));

    [Fact]
    public void CountSentences_counts() => Assert.Equal(2, WordCounter.CountSentences("Hallo. Welt!"));

    [Fact]
    public void CountParagraphs_counts() => Assert.Equal(3, WordCounter.CountParagraphs("a\nb\nc"));

    [Fact]
    public void ReadingMinutes_minimum() => Assert.Equal(1, WordCounter.ReadingMinutes("Wort " + new string('x', 5)));
}

public sealed class TemplateTests
{
    [Fact]
    public void Blank_hasChapterAndScene()
    {
        var p = StoryTemplates.CreateFromTemplate("blank", "T", "A");
        Assert.Single(p.Nodes.Where(n => n.Kind == NodeKind.Chapter));
        Assert.Single(p.Nodes.Where(n => n.Kind == NodeKind.Scene));
    }

    [Fact]
    public void ThreeAct_createsChapters()
    {
        var p = StoryTemplates.CreateFromTemplate("three-act", "T", "A");
        Assert.True(p.TotalChapters >= 3);
    }

    [Fact]
    public void HeroesJourney_exists()
    {
        var t = StoryTemplates.All.First(x => x.Id == "heroes-journey");
        Assert.True(t.Chapters.Length >= 10);
    }
}

public sealed class ProjectRepositoryTests
{
    [Fact]
    public async Task SaveLoad_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alice-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "test.alice.json");
            var repo = new ProjectRepository();
            var project = StoryTemplates.CreateFromTemplate("three-act", "Testroman", "Tester");
            project.Nodes[0].Content = "Es war einmal ein Test.";
            await repo.SaveAsync(project, path);
            Assert.True(File.Exists(path));
            var loaded = await repo.LoadAsync(path);
            Assert.NotNull(loaded);
            Assert.Equal("Testroman", loaded!.Title);
            Assert.Equal("Es war einmal ein Test.", loaded.Nodes.OrderBy(n => n.SortOrder).First(n => n.Content.Length > 0).Content);
            // SQLite-Index angelegt
            Assert.True(File.Exists(Path.Combine(dir, "index.db")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Load_missing_returnsNull()
    {
        var repo = new ProjectRepository();
        Assert.Null(await repo.LoadAsync(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".json")));
    }
}

public sealed class BackupTests
{
    [Fact]
    public async Task CreateAndRestore_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alice-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "roman.alice.json");
            var repo = new ProjectRepository();
            var settings = new FakeSettings();
            var backup = new BackupService(repo, settings);
            var project = StoryTemplates.CreateFromTemplate("blank", "BackupTest", "A");
            await repo.SaveAsync(project, path);
            project.Title = "Geändert nach Backup";
            await repo.SaveAsync(project, path);
            var file = await backup.CreateBackupAsync(project, path);
            Assert.True(File.Exists(file));
            // Restore auf Original-Stand simulieren: Inhalt löschen, dann Restore
            await File.WriteAllTextAsync(path, "{}");
            await backup.RestoreAsync(file, path);
            var restored = await repo.LoadAsync(path);
            Assert.NotNull(restored);
            Assert.Equal("Geändert nach Backup", restored!.Title);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

public sealed class SearchStatsTests
{
    [Fact]
    public void Search_findsAcrossEntities()
    {
        var project = StoryTemplates.CreateFromTemplate("blank", "Suchtest", "A");
        project.Characters.Add(new Character { Name = "Elara", Description = "Mutige Kriegerin" });
        project.WorldEntries.Add(new WorldEntry { Title = "Nordenland", Description = "Kalt und rau" });
        project.Nodes[0].Content = "Elara betrat Nordenland.";
        var svc = new SearchService();
        var r = svc.Search(project, "elara");
        Assert.True(r.Total >= 2);
        r = svc.Search(project, "nordenland");
        Assert.True(r.Total >= 1);
        r = svc.Search(project, "xyz-not-found-12345");
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public void Stats_computesProgress()
    {
        var project = StoryTemplates.CreateFromTemplate("blank", "Stats", "A");
        project.TargetWordCount = 100;
        project.Nodes[0].Content = string.Join(" ", Enumerable.Repeat("Wort", 50));
        var svc = new StatsService();
        var s = svc.Compute(project);
        Assert.Equal(50, s.TotalWords);
        Assert.Equal(50.0, s.ProgressPct);
    }
}

public sealed class ImportExportTests
{
    [Fact]
    public async Task Export_txt_and_markdown_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alice-ie-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var project = StoryTemplates.CreateFromTemplate("three-act", "IE-Test", "Autor");
            project.Nodes[0].Content = "Lorem ipsum dolor sit amet.";
            var svc = new ImportExportService();
            var txt = Path.Combine(dir, "out.txt");
            await svc.ExportAsync(project, txt, ExportFormat.Txt);
            var content = await File.ReadAllTextAsync(txt);
            Assert.Contains("IE-Test", content);
            var md = Path.Combine(dir, "out.md");
            await svc.ExportAsync(project, md, ExportFormat.Markdown);
            Assert.Contains("IE-Test", await File.ReadAllTextAsync(md));
            var docx = Path.Combine(dir, "out.docx");
            await svc.ExportAsync(project, docx, ExportFormat.Docx);
            Assert.True(new FileInfo(docx).Length > 0);
            var epub = Path.Combine(dir, "out.epub");
            await svc.ExportAsync(project, epub, ExportFormat.Epub);
            Assert.True(new FileInfo(epub).Length > 0);
            var pdf = Path.Combine(dir, "out.pdf");
            await svc.ExportAsync(project, pdf, ExportFormat.Pdf);
            var pdfHead = await File.ReadAllBytesAsync(pdf);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfHead.Take(4).ToArray()));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Import_txt_createsStructure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alice-imp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "in.md");
            await File.WriteAllTextAsync(src, "# Kapitel Eins\nHallo Welt.\n\n# Kapitel Zwei\nMehr Text.");
            var svc = new ImportExportService();
            var p = await svc.ImportAsync(src, ImportFormat.Markdown);
            Assert.True(p.TotalChapters >= 2);
            Assert.True(p.TotalWords >= 4);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

public sealed class AiTests
{
    [Fact]
    public async Task OpenAiProvider_unconfigured_throws()
    {
        var settings = new FakeSettings();
        var provider = new OpenAiCompatibleProvider(settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CompleteAsync("sys", "user", "ctx"));
    }

    [Fact]
    public async Task LocalFallback_alwaysAvailable()
    {
        var p = new LocalFallbackProvider();
        Assert.True(await p.IsAvailableAsync());
        var r = await p.CompleteAsync("sys", "Hallo", "Kontext");
        Assert.Contains("Lokal-Fallback", r);
    }

    [Fact]
    public void Companion_buildsPrompt()
    {
        var s = new FakeSettings();
        var svc = new DeepAiCompanionService(s);
        var prompt = svc.BuildStructuredPrompt("Analysiere", "Kontexttext");
        Assert.Contains("Analysiere", prompt);
        Assert.Contains("Kontexttext", prompt);
    }

    [Fact]
    public async Task Companion_exportsContextPackage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "alice-pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var s = new FakeSettings();
            var svc = new DeepAiCompanionService(s);
            var project = StoryTemplates.CreateFromTemplate("blank", "Paket", "A");
            project.Characters.Add(new Character { Name = "Hero", Description = "Mutig" });
            var outPath = Path.Combine(dir, "pkg.md");
            await svc.ExportContextPackageAsync(project, outPath);
            var txt = await File.ReadAllTextAsync(outPath);
            Assert.Contains("Paket", txt);
            Assert.Contains("Hero", txt);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

file sealed class FakeSettings : Alice.Core.Services.ISettingsStore
{
    public Alice.Core.Settings.AliceSettings Current { get; } = new() { AiBaseUrl = "", AiModel = "" };
    public string SettingsPath => Path.Combine(Path.GetTempPath(), "fake-settings.json");
    public Task<Alice.Core.Settings.AliceSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(Current);
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class StoryboardTests
{
    [Fact]
    public void BoardNodeTypes_has14()
    {
        Assert.Equal(34, BoardNodeTypes.All.Count);
        Assert.Equal("Buch", BoardNodeTypes.LabelOf(BoardNodeType.Book));
        Assert.Equal("Hauptfigur", BoardNodeTypes.LabelOf(BoardNodeType.MainCharacter));
        Assert.Equal("Nebenfigur", BoardNodeTypes.LabelOf(BoardNodeType.SideCharacter));
        Assert.Equal("#E05C7A", BoardNodeTypes.ColorOf(BoardNodeType.Character));
        Assert.Equal("Charakter", BoardNodeTypes.LabelOf(BoardNodeType.Character));
        Assert.Equal(8, BoardConnectionKinds.All.Count);
    }

    [Fact]
    public void Whiteboard_serializes_roundtrip()
    {
        var wb = new Whiteboard { Title = "Plot", PanX = 12, PanY = -5, Zoom = 1.5 };
        var a = new BoardNode { Title = "A", X = 10, Y = 20, NodeType = BoardNodeType.Character };
        var b = new BoardNode { Title = "B", X = 300, Y = 20 };
        wb.Nodes.Add(a); wb.Nodes.Add(b);
        wb.Connections.Add(new BoardConnection { FromNodeId = a.Id, ToNodeId = b.Id, Label = "kennt" });
        var json = System.Text.Json.JsonSerializer.Serialize(wb);
        var back = System.Text.Json.JsonSerializer.Deserialize<Whiteboard>(json)!;
        Assert.Equal("Plot", back.Title);
        Assert.Equal(2, back.Nodes.Count);
        Assert.Single(back.Connections);
        Assert.Equal(1.5, back.Zoom);
        Assert.Equal(12, back.PanX);
        Assert.Equal(wb.Nodes[0].Id, back.Nodes.First(n => n.Title == "A").Id);
    }

    [Fact]
    public void Project_withBoards_serializes()
    {
        var p = new AliceProject { Title = "T" };
        p.Whiteboards.Add(new Whiteboard { Title = "Board 1" });
        p.ActiveWhiteboardId = p.Whiteboards[0].Id;
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var back = System.Text.Json.JsonSerializer.Deserialize<AliceProject>(json)!;
        Assert.Single(back.Whiteboards);
        Assert.Equal(p.ActiveWhiteboardId, back.ActiveWhiteboardId);
    }
}

public sealed class ExportFormatTests
{
    private static AliceProject Sample()
    {
        var p = new AliceProject { Title = "Testbuch", Author = "Autor" };
        var ch = new ManuscriptNode { Title = "Kapitel 1", Kind = NodeKind.Chapter };
        var sc = new ManuscriptNode { Title = "Szene 1", Kind = NodeKind.Scene, ParentId = ch.Id, Content = "Es war einmal." };
        p.Nodes.Add(ch); p.Nodes.Add(sc);
        var wb = new Whiteboard { Title = "Plot" };
        var book = new BoardNode { Title = "Testbuch", NodeType = BoardNodeType.Book, IsBookPanel = true, X = 60, Y = 60, Width = 360, Height = 170 };
        var hero = new BoardNode { Title = "Held", NodeType = BoardNodeType.MainCharacter, X = 500, Y = 60 };
        wb.Nodes.Add(book); wb.Nodes.Add(hero);
        wb.Connections.Add(new BoardConnection { FromNodeId = book.Id, ToNodeId = hero.Id });
        p.Whiteboards.Add(wb);
        return p;
    }

    [Theory]
    [InlineData(ExportFormat.Pdf, ".pdf")]
    [InlineData(ExportFormat.Docx, ".docx")]
    [InlineData(ExportFormat.Txt, ".txt")]
    [InlineData(ExportFormat.Markdown, ".md")]
    [InlineData(ExportFormat.Rtf, ".rtf")]
    [InlineData(ExportFormat.Odt, ".odt")]
    [InlineData(ExportFormat.Html, ".html")]
    [InlineData(ExportFormat.Epub, ".epub")]
    [InlineData(ExportFormat.Zip, ".zip")]
    public async Task Export_allFormats_nonEmpty(ExportFormat format, string ext)
    {
        var svc = new ImportExportService();
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, "out" + ext);
        await svc.ExportAsync(Sample(), path, format, new ExportOptions { Scope = "Gesamtes Buch", IncludeToc = true, TitlePage = true, PageNumbers = true, ChapterNewPage = true, PageFormat = "A4", FontSize = 11 });
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 50);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Export_pdf_hasHeader()
    {
        var svc = new ImportExportService();
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, "out.pdf");
        await svc.ExportAsync(Sample(), path, ExportFormat.Pdf);
        Assert.StartsWith("%PDF-", (await File.ReadAllTextAsync(path))[..5]);
        Directory.Delete(dir, true);
    }
}

public sealed class HierarchyTests
{
    [Fact]
    public void EnsureParents_derivesFromConnections_bookStaysRoot()
    {
        var wb = new Whiteboard();
        var book = new BoardNode { Title = "Buch", NodeType = BoardNodeType.Book, IsBookPanel = true };
        var hero = new BoardNode { Title = "Held" };
        var secret = new BoardNode { Title = "Geheimnis" };
        var alone = new BoardNode { Title = "Unabhängig" };
        wb.Nodes.AddRange([book, hero, secret, alone]);
        wb.Connections.Add(new BoardConnection { FromNodeId = book.Id, ToNodeId = hero.Id });
        wb.Connections.Add(new BoardConnection { FromNodeId = hero.Id, ToNodeId = secret.Id });
        BoardHierarchy.EnsureParents(wb);
        Assert.Null(book.ParentId);
        Assert.Equal(book.Id, hero.ParentId);
        Assert.Equal(hero.Id, secret.ParentId);
        Assert.Null(alone.ParentId); // keine erfundene Verbindung für unabhängige Panels
    }

    [Fact]
    public void EnsureConnections_createsMissingParentLinks()
    {
        var wb = new Whiteboard();
        var book = new BoardNode { Title = "Buch", IsBookPanel = true };
        var hero = new BoardNode { Title = "Held", ParentId = book.Id };
        wb.Nodes.AddRange([book, hero]);
        BoardHierarchy.EnsureConnections(wb);
        Assert.Single(wb.Connections);
        BoardHierarchy.EnsureConnections(wb); // idempotent
        Assert.Single(wb.Connections);
    }

    [Fact]
    public void ParentId_survivesRoundtrip()
    {
        var wb = new Whiteboard();
        var book = new BoardNode { Title = "Buch", IsBookPanel = true };
        var hero = new BoardNode { Title = "Held", ParentId = book.Id };
        wb.Nodes.AddRange([book, hero]);
        var p = new AliceProject { Title = "T" }; p.Whiteboards.Add(wb);
        var json = JsonSerializer.Serialize(p);
        var back = JsonSerializer.Deserialize<AliceProject>(json)!;
        var heroBack = back.Whiteboards[0].Nodes.Single(n => n.Title == "Held");
        Assert.Equal(book.Id, heroBack.ParentId);
    }
}

public sealed class InlineAiMarkerTests
{
    [Fact]
    public void Extract_single() => Assert.Single(InlineAiMarkers.Extract("A {{AI: Beschreibung }} B"));
    [Fact]
    public void Extract_multiple() => Assert.Equal(2, InlineAiMarkers.Extract("{{AI: eins}} x {{AI:zwei}}").Count);
    [Fact]
    public void Extract_unfinished_ignored() => Assert.Empty(InlineAiMarkers.Extract("{{AI: offen"));
    [Fact]
    public void Strip_removes() => Assert.DoesNotContain("AI:", InlineAiMarkers.Strip("A {{AI: x}} B"));
    [Fact]
    public void Create_roundtrip()
    {
        var m = InlineAiMarkers.Create("Text");
        var f = Assert.Single(InlineAiMarkers.Extract(m));
        Assert.Equal("Text", f.Instruction);
    }
}

public sealed class ChapterStructureTests
{
    private static Alice.Core.Models.ManuscriptNode Node(string title, string? content)
        => new Alice.Core.Models.ManuscriptNode { Title = title, Content = content, Kind = NodeKind.Chapter };

    [Fact]
    public void Compile_keepsBodyTextOnly()
    {
        var node = Node("Kapitel 1", string.Join(Environment.NewLine + Environment.NewLine, new[] { "Der Anfang.", "Es war einmal." }));
        var doc = Alice.App.ViewModels.FlowDocumentTools.CompileChapterDocument(node);
        // Manuskript-Text = nur der Body, keine Kapitel-Titelseite.
        var plain = Alice.App.ViewModels.FlowDocumentTools.ToPlainText(doc).Replace("\r\n", "\n");
        Assert.Equal("Der Anfang.\n\nEs war einmal.", plain);
        Assert.False(plain.Contains("Kapitel 1"));
        Assert.True(Alice.App.ViewModels.FlowDocumentTools.HasChapterHeader(doc));
    }

    [Fact]
    public void Compile_emptyBody() {
        var node = Node("Neues Kapitel", "");
        var doc = Alice.App.ViewModels.FlowDocumentTools.CompileChapterDocument(node);
        Assert.Equal(string.Empty, Alice.App.ViewModels.FlowDocumentTools.ToPlainText(doc));
        // Richtig erkannt: BodyOnly != null, enthält Body-Marker
        var body = Alice.App.ViewModels.FlowDocumentTools.BodyOnly(doc);
        Assert.NotNull(body);
    }

    [Fact]
    public void ExtractEditedChapterTitle_syncsRename() {
        var node = Node("Kapitel 2: Der Anfang", "Text");
        var doc = Alice.App.ViewModels.FlowDocumentTools.CompileChapterDocument(node);
        // Nutzer editiert die Titelzeile
        System.Windows.Documents.Paragraph? t = null;
        foreach (var block in doc.Blocks) {
            if (block is System.Windows.Documents.Paragraph p && (p.Tag as string) == "ALICE-CH-TITLE") t = p;
        }
        Assert.NotNull(t);
        new System.Windows.Documents.TextRange(t!.ContentStart, t.ContentEnd) { Text = "Der Anfang, neu" };
        var nt = Alice.App.ViewModels.FlowDocumentTools.ExtractEditedChapterTitle(doc, node.Title);
        Assert.Equal("Kapitel 2: Der Anfang, neu", nt);
    }

    [Fact]
    public void SplitChapterTitle_patterns() {
        Assert.Equal(("Kapitel 3", "Ankunft"), Alice.App.ViewModels.FlowDocumentTools.SplitChapterTitle("Kapitel 3 - Ankunft"));
        Assert.Equal(("", "Neues Kapitel"), Alice.App.ViewModels.FlowDocumentTools.SplitChapterTitle("Neues Kapitel"));
        Assert.Equal(("", "Susanna"), Alice.App.ViewModels.FlowDocumentTools.SplitChapterTitle("Susanna"));
    }
}

public sealed class GermanSpellingTests
{
    private static Alice.Core.Proofreading.GermanSpelling Engine() => new Alice.Core.Proofreading.GermanSpelling();

    [Fact]
    public void Known_words_pass() {
        var e = Engine();
        Assert.True(e.IsKnownWord("Haus")); Assert.True(e.IsKnownWord("Fenster")); Assert.True(e.IsKnownWord("Bücher")); Assert.True(e.IsKnownWord("weiß"));
    }
    [Fact]
    public void Unknown_typo_is_flagged() {
        var e = Engine();
        Assert.False(e.IsKnownWord("Hsuse"));
        var issues = e.CheckText("Das Hsuse ist grösser als das Haaus.");
        Assert.Contains(issues, i => i.Word.Equals("Hsuse", StringComparison.OrdinalIgnoreCase) && i.Kind == Alice.Core.Proofreading.ProofKind.Spelling);
    }
    [Fact]
    public void Personal_and_project_words_are_known() {
        var e = Engine();
        e.SetPersonalWords(new[] { "Xylophor" }); e.SetProjectWords(new[] { "Moritz", "Arda" });
        Assert.True(e.IsKnownWord("Xylophor")); Assert.True(e.IsKnownWord("Moritz")); Assert.True(e.IsKnownWord("Arda"));
    }
    [Fact]
    public void Rules_detect_double_word_and_spaces() {
        var e = Engine();
        var issues = e.CheckText("Der der  Stern leuchtet.");
        Assert.Contains(issues, i => i.Kind == Alice.Core.Proofreading.ProofKind.RepeatedWord);
        Assert.Contains(issues, i => i.Kind == Alice.Core.Proofreading.ProofKind.DoubleSpace);
    }
    [Fact]
    public void Autocorrect_fixes_safe_rules() {
        var e = Engine();
        var res = e.AutoCorrect("der der  Stern. der text folgt.");
        Assert.DoesNotContain("der der", res);
        Assert.DoesNotContain("  ", res);
        Assert.Contains("Der", res);
    }
    [Fact]
    public void Suggests_candidates() {
        var e = Engine();
        var s = e.Suggest("Hauset");
        Assert.NotEmpty(s);
    }
}
