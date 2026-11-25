namespace Alice.Core.Enums;

/// <summary>Art eines Manuskript-Knotens (Binder).</summary>
public enum NodeKind
{
    Folder,
    Series,
    Book,
    Part,
    Chapter,
    Scene,
    SubScene,
    Document,
    Note,
    Research,
    Archive
}

/// <summary>Schreibstatus.</summary>
public enum StoryStatus
{
    Idea,
    Planned,
    Draft,
    InProgress,
    Revision,
    Editing,
    Completed
}

/// <summary>Exportformat.</summary>
public enum ExportFormat { Docx, Pdf, Epub, Txt, Markdown, Rtf, Odt, Html, Zip }

/// <summary>Export-Optionen (Umfang + PDF-Layout).</summary>
public sealed class ExportOptions
{
    public string Scope { get; set; } = "Gesamtes Buch"; // Gesamtes Buch | Nur Manuskript | Nur Storyboard
    public bool IncludeToc { get; set; }
    public bool TitlePage { get; set; }
    public bool PageNumbers { get; set; }
    public bool ChapterNewPage { get; set; }
    public int FontSize { get; set; } = 11;
    public string PageFormat { get; set; } = "A4"; // A4 | Letter
}

/// <summary>Importformat.</summary>
public enum ImportFormat { Docx, Txt, Markdown, Rtf }

/// <summary>Themen.</summary>
public enum ThemeKind { Dark, Light, Midnight, Forest }

/// <summary>Arbeitsbereichs-Modus.</summary>
public enum WorkspaceMode { Editor, Outliner, Corkboard, Timeline, Characters, World, Research, Dashboard, Statistics, Storyboard }
