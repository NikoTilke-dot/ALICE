namespace Alice.Core.Models;

/// <summary>Root-Projektcontainer.</summary>
public sealed class AliceProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Mein Roman";
    public string Author { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string Genre { get; set; } = "Fantasy";
    public int TargetWordCount { get; set; } = 80000;
    public string TemplateId { get; set; } = "blank";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string FormatVersion { get; set; } = "1.0";

    public List<ManuscriptNode> Nodes { get; set; } = []; // flache Liste mit ParentId; UI baut Baum
    public List<Character> Characters { get; set; } = [];
    public List<WorldEntry> WorldEntries { get; set; } = [];
    public List<TimelineEvent> TimelineEvents { get; set; } = [];
    public List<ResearchItem> Research { get; set; } = [];
    public List<AiConversation> AiConversations { get; set; } = [];
    public List<WritingSession> Sessions { get; set; } = [];
    public List<StoryArc> StoryArcs { get; set; } = [];
    public List<Whiteboard> Whiteboards { get; set; } = [];
    public Guid? ActiveWhiteboardId { get; set; }
    public List<string> CustomCategories { get; set; } = [];
    public List<string> CustomStatuses { get; set; } = [];
    public Dictionary<string, string> CustomFields { get; set; } = new();

    public int TotalWords => Nodes.Where(n => !n.IsFolder).Sum(n => n.WordCount);
    public int TotalScenes => Nodes.Count(n => n.Kind == Enums.NodeKind.Scene || n.Kind == Enums.NodeKind.SubScene);
    public int TotalChapters => Nodes.Count(n => n.Kind == Enums.NodeKind.Chapter);

    public IEnumerable<ManuscriptNode> Roots => Nodes.Where(n => n.ParentId == null).OrderBy(n => n.SortOrder);
    public IEnumerable<ManuscriptNode> ChildrenOf(Guid parentId) => Nodes.Where(n => n.ParentId == parentId).OrderBy(n => n.SortOrder);
    public ManuscriptNode? Find(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);
}
