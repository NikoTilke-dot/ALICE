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
public enum ExportFormat { Docx, Pdf, Epub, Txt, Markdown }

/// <summary>Importformat.</summary>
public enum ImportFormat { Docx, Txt, Markdown, Rtf }

/// <summary>Themen.</summary>
public enum ThemeKind { Dark, Light, Midnight, Forest }

/// <summary>Arbeitsbereichs-Modus.</summary>
public enum WorkspaceMode { Editor, Outliner, Corkboard, Timeline, Characters, World, Research, Dashboard, Statistics, Storyboard }
