using Alice.Core.Enums;
using Alice.Core.Models;

namespace Alice.Core.Templates;

/// <summary>Story-Strukturvorlagen.</summary>
public static class StoryTemplates
{
    public static IReadOnlyList<(string Id, string Name, string[] Chapters)> All { get; } =
    [
        ("blank", "Leeres Projekt", []),
        ("three-act", "Drei-Akt-Struktur", ["Achter Anfang", "Enthüllung", "Point of No Return", "Sieg oder Niederlage", "Auflösung"]),
        ("five-act", "Fünf-Akt-Struktur", ["Exposition", "Komplikation", "Klimax", "Fallende Handlung", "Auflösung"]),
        ("heroes-journey", "Heldenreise", ["Gewöhnliche Welt", "Ruf zum Abenteuer", "Weigerung", "Begegnung mit dem Mentor", "Schwelle überschreiten", "Tests", "Annäherung", "Heimsuchung", "Preis", "Belohnung", "Rückweg", "Auferstehung", "Heimkehr"]),
        ("save-the-cat", "Save-the-Cat Beats", ["Opening Image", "Theme Stated", "Set-Up", "Catalyst", "Debatte", "Break Into Two", "B-Story", "Fun and Games", "Midpoint", "Bad Guys Close In", "All Is Lost", "Dark Night of the Soul", "Break Into Three", "Finale", "Final Image"]),
        ("mystery", "Mystery", ["Fall", "Ermittler", "Verdächtige", "Hinweise", "Falsche Spur", "Enthüllung", "Auflösung"]),
        ("romance", "Romantik", ["Alltag", "Erste Begegnung", "Annäherung", "Konflikt", "Geständnis", "Happy End"]),
        ("fantasy", "Fantasy", ["Prolog", "Die neue Welt", "Der Ruf", "Ausbildung", "Die Reise", "Die Prüfung", "Der Kampf", "Epilog"]),
        ("thriller", "Thriller", ["Auftakt", "Eskalation", "Plot-Twist", "Jagd", "Finale"]),
    ];

    public static AliceProject CreateFromTemplate(string templateId, string title, string author)
    {
        var proj = new AliceProject { Title = title, Author = author, TemplateId = templateId, Synopsis = string.Empty };
        var tpl = All.FirstOrDefault(t => t.Id == templateId);
        if (tpl.Name is null || tpl.Chapters.Length == 0)
        {
            // Standard-Struktur für Blank
            var chapter = new ManuscriptNode { Title = "Kapitel 1", Kind = NodeKind.Chapter, SortOrder = 0 };
            proj.Nodes.Add(chapter);
            var scene = new ManuscriptNode { Title = "Szene 1", Kind = NodeKind.Scene, ParentId = chapter.Id, SortOrder = 0, TargetWordCount = 2000 };
            proj.Nodes.Add(scene);
            return proj;
        }
        var i = 0;
        foreach (var ch in tpl.Chapters)
        {
            var node = new ManuscriptNode { Title = ch, Kind = NodeKind.Chapter, SortOrder = i++ };
            proj.Nodes.Add(node);
            var scene = new ManuscriptNode { Title = "Szene 1", Kind = NodeKind.Scene, ParentId = node.Id, SortOrder = 0, TargetWordCount = 2000 };
            proj.Nodes.Add(scene);
        }
        return proj;
    }
}
