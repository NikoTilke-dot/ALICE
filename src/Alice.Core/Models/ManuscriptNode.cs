using Alice.Core.Enums;

namespace Alice.Core.Models;

/// <summary>Hierarchischer Manuskript-Knoten (Szene, Kapitel, Notiz, ...).</summary>
public sealed class ManuscriptNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neues Dokument";
    public NodeKind Kind { get; set; } = NodeKind.Scene;
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsFolder { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public StoryStatus Status { get; set; } = StoryStatus.Idea;
    public string? PovCharacterId { get; set; }
    public string? Location { get; set; }
    public string Color { get; set; } = "#7C7CF4";
    public int TargetWordCount { get; set; } = 2000;
    public string Tags { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsFavorite { get; set; }
    public List<ManuscriptNode> Children { get; set; } = [];
    public List<Snapshot> Snapshots { get; set; } = [];
    public List<string> LinkedCharacterIds { get; set; } = [];
    public List<string> LinkedWorldIds { get; set; } = [];
    public List<string> LinkedResearchIds { get; set; } = [];

    public int WordCount { get => WordCounter.CountWords(Content); set { } } // ReadOnly-Hinweis: berechnete Eigenschaft, Setter ignoriert (WPF TwoWay-Kompat)
    public int CharCount { get => Content?.Length ?? 0; set { } }
    public int SentenceCount { get => WordCounter.CountSentences(Content ?? string.Empty); set { } }
    public int ParagraphCount { get => WordCounter.CountParagraphs(Content ?? string.Empty); set { } }
    public int ReadingMinutes { get => WordCounter.ReadingMinutes(Content ?? string.Empty); set { } }
}

public sealed class Snapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Snapshot";
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
