namespace Alice.Core.Models;

/// <summary>Typ eines Storyboard-Nodes (Vorlage mit Farbe + Symbol).</summary>
public enum BoardNodeType { Book, MainCharacter, SideCharacter, Antagonist, SupportingCharacter, CharacterIdea, Note, Character, Location, World, Continent, Country, Kingdom, City, Village, Faction, Culture, Religion, MagicSystem, Technology, Lore, Event, Chapter, Scene, Plot, Subplot, Conflict, TurningPoint, Object, Mystery, Idea, Research, Custom, AiNote }

/// <summary>Typ einer Verbindung zwischen Nodes.</summary>
public enum BoardConnectionKind { Normal, ParentChild, Dependency, Relation, Temporal, Conflict, Reference, Custom }

/// <summary>Verknüpfungsart eines Nodes zu einem ALICE-Projektobjekt.</summary>
public enum BoardLinkKind { None, Character, World, Manuscript, Timeline }

/// <summary>Katalog der Node-Vorlagen: Farbe + Glyph (Segoe MDL2) + deutsches Label.</summary>
public static class BoardNodeTypes
{
    public sealed record Info(BoardNodeType Type, string Label, string Color, string Glyph);

    public static IReadOnlyList<Info> All { get; } =
    [
        new(BoardNodeType.Book, "Buch", "#8B7CF0", "\uE8F1"),
        new(BoardNodeType.MainCharacter, "Hauptfigur", "#E05C7A", "\uE77B"),
        new(BoardNodeType.SideCharacter, "Nebenfigur", "#F0A35E", "\uE77B"),
        new(BoardNodeType.Antagonist, "Antagonist", "#B91C1C", "\uE77B"),
        new(BoardNodeType.SupportingCharacter, "Unterstützende Figur", "#FB7185", "\uE77B"),
        new(BoardNodeType.CharacterIdea, "Charakteridee", "#F9A8D4", "\uE854"),
        new(BoardNodeType.Note, "Allgemeine Notiz", "#5B8DEF", "\uE70B"),
        new(BoardNodeType.Character, "Charakter", "#E05C7A", "\uE77B"),
        new(BoardNodeType.Location, "Ort", "#2EA8A0", "\uE707"),
        new(BoardNodeType.World, "Welt / Worldbuilding", "#8B7CF0", "\uE91B"),
        new(BoardNodeType.Continent, "Kontinent", "#34D399", "\uE707"),
        new(BoardNodeType.Country, "Land", "#F59E0B", "\uE707"),
        new(BoardNodeType.Kingdom, "Königreich", "#FBBF24", "\uE74C"),
        new(BoardNodeType.City, "Stadt", "#38BDF8", "\uE707"),
        new(BoardNodeType.Village, "Dorf", "#A3E635", "\uE707"),
        new(BoardNodeType.Faction, "Fraktion", "#F0A35E", "\uE716"),
        new(BoardNodeType.Culture, "Kultur", "#C084FC", "\uE716"),
        new(BoardNodeType.Religion, "Religion", "#E7C3F5", "\uE716"),
        new(BoardNodeType.MagicSystem, "Magiesystem", "#818CF8", "\uE9D9"),
        new(BoardNodeType.Technology, "Technologie", "#94A3B8", "\uE9B9"),
        new(BoardNodeType.Lore, "Lore", "#A78BFA", "\uE91B"),
        new(BoardNodeType.Event, "Ereignis", "#E0C45C", "\uE853"),
        new(BoardNodeType.Plot, "Plot", "#F472B6", "\uE853"),
        new(BoardNodeType.Subplot, "Nebenplot", "#DB2777", "\uE853"),
        new(BoardNodeType.TurningPoint, "Wendepunkt", "#F97316", "\uE853"),
        new(BoardNodeType.Chapter, "Kapitel", "#7C7CF4", "\uE8F1"),
        new(BoardNodeType.Scene, "Szene", "#4ADE80", "\uE8F1"),
        new(BoardNodeType.Object, "Gegenstand", "#9FA2BC", "\uE8F1"),
        new(BoardNodeType.Mystery, "Geheimnis", "#C084FC", "\uE72E"),
        new(BoardNodeType.Conflict, "Konflikt", "#F87171", "\uE711"),
        new(BoardNodeType.Idea, "Idee", "#FBBF24", "\uE854"),
        new(BoardNodeType.Research, "Recherche", "#38BDF8", "\uE721"),
        new(BoardNodeType.Custom, "Eigene Kategorie", "#9FA2BC", "\uE70B"),
        new(BoardNodeType.AiNote, "KI-Notiz", "#F472B6", "\uE8A0"),
    ];

    public static Info Of(BoardNodeType t) => All.FirstOrDefault(x => x.Type == t) ?? All[0];
    public static string LabelOf(BoardNodeType t) => Of(t).Label;
    public static string ColorOf(BoardNodeType t) => Of(t).Color;
    public static string GlyphOf(BoardNodeType t) => Of(t).Glyph;
}

/// <summary>Label-Katalog für Verbindungstypen.</summary>
public static class BoardConnectionKinds
{
    public static string LabelOf(BoardConnectionKind k) => k switch
    {
        BoardConnectionKind.Normal => "Normale Verbindung",
        BoardConnectionKind.ParentChild => "Eltern-Kind",
        BoardConnectionKind.Dependency => "Abhängigkeit",
        BoardConnectionKind.Relation => "Beziehung",
        BoardConnectionKind.Temporal => "Zeitliche Verbindung",
        BoardConnectionKind.Conflict => "Konflikt",
        BoardConnectionKind.Reference => "Verweis",
        _ => "Eigener Verbindungstyp",
    };

    public static IReadOnlyList<(BoardConnectionKind Kind, string Label)> All { get; } =
        Enum.GetValues<BoardConnectionKind>().Select(k => (k, LabelOf(k))).ToList();
}

/// <summary>Ein frei platzierbarer Node auf dem Storyboard.</summary>
public sealed class BoardNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neue Notiz";
    public string Text { get; set; } = string.Empty;
    public BoardNodeType NodeType { get; set; } = BoardNodeType.Note;
    public string Category { get; set; } = string.Empty; // freie Kategorie (überschreibt ggf. Typ-Label)
    public string Status { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty; // Glyph; leer = Typ-Glyph
    public string Color { get; set; } = "#1A1B2A"; // Kartenhintergrund; leer = Standard
    public string AccentColor { get; set; } = string.Empty; // Kategorie-Farbe; leer = Typ-Farbe
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Parent-Panel dieser Zeile. Null = Root (Buchpanel) oder unabhängig (per Canvas erstellt, keine erfundene Verbindung).</summary>
    public Guid? ParentId { get; set; }
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 120;
    public bool IsCollapsed { get; set; }
    public bool IsBookPanel { get; set; } // großes Standard-Panel mit Buchtitel (Startpunkt, nicht löschbar)
    public string Tags { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public BoardLinkKind LinkKind { get; set; } = BoardLinkKind.None;
    public string LinkId { get; set; } = string.Empty; // Id des verknüpften Projektobjekts
    public int ZIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string EffectiveGlyph => string.IsNullOrWhiteSpace(Symbol) ? BoardNodeTypes.GlyphOf(NodeType) : Symbol;
    public string EffectiveAccent => string.IsNullOrWhiteSpace(AccentColor) ? BoardNodeTypes.ColorOf(NodeType) : AccentColor;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double RightX => X + Width;
    public double MidY => Y + Height / 2;
}

/// <summary>Hierarchie-Helfer: Parent-IDs aus bestehenden Verbindungen ableiten (Migration),
/// Verbindungen aus Parent-IDs erzeugen. Erfindet niemals neue Beziehungen aus Position.</summary>
public static class BoardHierarchy
{
    /// <summary>Für Nodes ohne ParentId, die eine eingehende Verbindung haben, Parent aus der Verbindung übernehmen.
    /// Das Buchpanel (IsBookPanel) bleibt immer ohne Parent. Keine neuen Verbindungen, keine Positionsheuristik.</summary>
    public static void EnsureParents(Whiteboard board)
    {
        var byId = board.Nodes.ToDictionary(n => n.Id);
        foreach (var c in board.Connections)
        {
            if (byId.TryGetValue(c.ToNodeId, out var child) && !child.IsBookPanel && child.ParentId is null
                && byId.ContainsKey(c.FromNodeId))
                child.ParentId = c.FromNodeId;
        }
    }
    /// <summary>Erzeugt fehlende Parent→Kind-Verbindungen (idempotent, keine Duplikate).</summary>
    public static void EnsureConnections(Whiteboard board)
    {
        var existing = board.Connections.Select(c => (c.FromNodeId, c.ToNodeId)).ToHashSet();
        foreach (var n in board.Nodes)
        {
            if (n.ParentId is { } pid && existing.Add((pid, n.Id)) && board.Nodes.Any(x => x.Id == pid))
                board.Connections.Add(new BoardConnection { FromNodeId = pid, ToNodeId = n.Id });
        }
    }
}

/// <summary>Verbindungslinie zwischen zwei Nodes.</summary>
public sealed class BoardConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public BoardConnectionKind Kind { get; set; } = BoardConnectionKind.Normal;
    public string Color { get; set; } = "#4A4D6B"; // dunkle Standardlinie
    public bool HasArrow { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Optionale Gruppe / Frame, die Nodes zusammenfasst.</summary>
public sealed class BoardGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neue Gruppe";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 280;
    public string Color { get; set; } = "#2A2452";
    public bool IsCollapsed { get; set; }
}

/// <summary>Benutzerdefinierter Panel-Typ (dauerhaft im Projekt gespeichert).</summary>
public sealed class CustomBoardType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Eigener Typ";
    public string Symbol { get; set; } = "\uE70B";
    public string Color { get; set; } = "#9FA2BC";
    public string Description { get; set; } = string.Empty;
}

/// <summary>Ein Whiteboard (Canvas) mit eigenem Pan-/Zoom-Zustand.</summary>
public sealed class Whiteboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Neues Storyboard";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<BoardNode> Nodes { get; set; } = [];
    public List<BoardConnection> Connections { get; set; } = [];
    public List<BoardGroup> Groups { get; set; } = [];
    // Canvas-Zustand (wird gespeichert, damit das Board exakt wiederhergestellt wird)
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double Zoom { get; set; } = 1.0;
    public bool ShowGrid { get; set; } = true;
}
