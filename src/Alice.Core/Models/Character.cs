namespace Alice.Core.Models;

/// <summary>Charakterprofil.</summary>
public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Neue Figur";
    public string Aliases { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Appearance { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Goals { get; set; } = string.Empty;
    public string Motivations { get; set; } = string.Empty;
    public string Fears { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Weaknesses { get; set; } = string.Empty;
    public string Secrets { get; set; } = string.Empty;
    public string Conflicts { get; set; } = string.Empty;
    public string Arc { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CustomFieldsJson { get; set; } = "{}";
    public List<CharacterRelationship> Relationships { get; set; } = [];
    public List<string> Appearances { get; set; } = []; // Manuskript-Knoten-Ids
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CharacterRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TargetCharacterId { get; set; } = string.Empty;
    public string Type { get; set; } = "Allgemein"; // Familie, Freund, Feind, Romantik, Rivale, Verbündeter, Mentor, Custom
    public string Description { get; set; } = string.Empty;
}
