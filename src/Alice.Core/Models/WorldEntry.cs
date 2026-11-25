namespace Alice.Core.Models;

/// <summary>Weltbau-Eintrag (Land, Stadt, Fraktion, Magiesystem, ...).</summary>
public sealed class WorldEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neuer Eintrag";
    public string Category { get; set; } = "Allgemein";
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public List<string> RelatedWorldIds { get; set; } = [];
    public List<string> RelatedCharacterIds { get; set; } = [];
    public string CustomFieldsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
