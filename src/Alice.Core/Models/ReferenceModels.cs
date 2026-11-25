namespace Alice.Core.Models;

/// <summary>Timeline-Ereignis.</summary>
public sealed class TimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neues Ereignis";
    public string Description { get; set; } = string.Empty;
    public string Era { get; set; } = string.Empty;
    public string DateLabel { get; set; } = string.Empty; // freie Angabe, z.B. "Jahr 300", "3 Tage nach Ankunft"
    public DateTime? Date { get; set; }
    public string Kind { get; set; } = "Story"; // Story, Character, World, Chapter, Scene
    public string TimelineName { get; set; } = "Hauptzeitlinie";
    public string? LinkedNodeId { get; set; }
    public string? LinkedCharacterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Recherche-Eintrag.</summary>
public sealed class ResearchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neue Recherche";
    public string Content { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Kind { get; set; } = "Note"; // Note, Link, File, Quote
    public string Tags { get; set; } = string.Empty;
    public List<string> LinkedNodeIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>AI-Nachricht.</summary>
public sealed class AiMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>AI-Konversation.</summary>
public sealed class AiConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neue Konversation";
    public string ProviderId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string SystemInstruction { get; set; } = string.Empty;
    public List<AiMessage> Messages { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Schreibsitzung für Statistik.</summary>
public sealed class WritingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public int WordsAdded { get; set; }
    public int WordsTotalAtEnd { get; set; }
    public TimeSpan Duration { get; set; }
    public int SessionWords { get; set; }
}

/// <summary>Story-Bogen / Plot-Thread.</summary>
public sealed class StoryArc
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neuer Bogen";
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#7C7CF4";
    public List<string> NodeIds { get; set; } = [];
}
