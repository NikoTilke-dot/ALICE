namespace Alice.Core.Settings;

/// <summary>Ein DeepAI-Modell aus dem lokalen Katalog (DeepAI Code, pi-Provider).</summary>
public sealed record DeepAiModel(string Id, string Label, int TheosPerRequest, bool IsUnmetered)
{
    public string DisplayText =>
        IsUnmetered ? $"{Label} (0 Theos – unlimitiert)" : $"{Label} (~{TheosPerRequest} Theos/Anfrage)";
}

/// <summary>
/// Alle in DeepAI Code auf diesem PC verfügbaren Modelle.
/// Quelle: DeepAI Code Orchestrator-Katalog (legitim, 24.11.2026),
/// nur Provider mit leerer Constraint-Liste (installiert/aktiviert).
/// Theo-Preise: offizielle DeepAI-Website deepai.org/chat/ai-code (Frontier-Modelle
/// verbrauchen Theos, 12 unmetered-Modelle nie). Typische Theos pro Anfrage;
/// längere Aufgaben verbrauchen mehr.
/// </summary>
public static class DeepAiCatalog
{
    public static IReadOnlyList<DeepAiModel> Models { get; } =
    [
        // Unmetered (0 Theos)
        new("deepai/muse-ember", "Muse Ember (aktuell)", 0, true),
        new("deepai/glm-5.3-flash", "GLM 5.3 Flash", 0, true),
        new("deepai/mimo-2.6-flash", "MiMo 2.6 Flash", 0, true),
        new("deepai/qwen-3.8-flash", "Qwen 3.8 Flash", 0, true),
        new("deepai/minimax-m3.1-flash", "MiniMax M3.1 Flash", 0, true),
        new("deepai/solar-mini-4", "Solar Mini 4", 0, true),
        new("deepai/gpt-6-luna", "GPT-6 Luna", 0, true),
        new("deepai/claude-haiku-5-5", "Claude Haiku 5.5", 0, true),
        new("deepai/mimo-2.6-pro", "MiMo 2.6 Pro", 0, true),
        new("deepai/glyph-cluster", "Glyph Cluster", 0, true),
        new("deepai/deepai-cyber", "DeepAI Cyber", 0, true),
        new("deepai/deepseek-v4.1-flash", "DeepSeek V4.1 Flash (DeepSeek Harness)", 0, true),
        // Frontier (Theos/Anfrage)
        new("deepai/minimax-m3", "MiniMax M3", 2, false),
        new("deepai/glm-5.3", "GLM 5.3", 3, false),
        new("deepai/kimi-k3", "Kimi K3", 3, false),
        new("deepai/kimi-k3.1-pro", "Kimi K3.1 Pro", 3, false),
        new("deepai/gpt-6.1-terra", "GPT-6.1 Terra", 5, false),
        new("deepai/muse-spark-1.3", "Muse Spark 1.3", 6, false),
        new("deepai/gemini-4-hydrogen", "Gemini 4 Hydrogen", 8, false),
        new("deepai/gpt-6.1-sol", "GPT-6.1 Sol", 10, false),
        new("deepai/gemini-4-argon", "Gemini 4 Argon", 10, false),
        new("deepai/gemini-4-carbon", "Gemini 4 Carbon", 10, false),
        new("deepai/claude-sonnet-5-5", "Claude Sonnet 5.5", 10, false),
        new("deepai/claude-opus-5-5", "Claude Opus 5.5", 15, false),
        new("deepai/muse-wonder", "Muse Wonder", 15, false),
        new("deepai/qwen-3.8-max-prime", "Qwen 3.8 Max Prime", 15, false),
        new("deepai/claude-fable-5-2", "Claude Fable 5.2", 20, false),
        new("deepai/gpt-6-astra", "GPT-6 Astra", 50, false),
    ];
}
