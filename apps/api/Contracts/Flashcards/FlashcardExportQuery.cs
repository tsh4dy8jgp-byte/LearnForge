namespace LearnForge.Api.Contracts.Flashcards;

// Query values use the same camelCase spelling as JSON. Minimal API enum query binding is case-sensitive.
public sealed record FlashcardExportQuery(string Format = "apkg", string Paper = "a4", string Mode = "duplex",
    string Binding = "long", double OffsetX = 0, double OffsetY = 0, string? Deck = null, string? Objective = null, string? Tag = null)
{
    public FlashcardExportOptions Options() => new(Parse<FlashcardExportFormat>(Format), Parse<CardPaper>(Paper),
        Parse<CardPrintMode>(Mode), Parse<CardBinding>(Binding), OffsetX, OffsetY, Deck, Objective, Tag);
    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw new DomainError(400, $"Unknown {typeof(T).Name} value.");
}
