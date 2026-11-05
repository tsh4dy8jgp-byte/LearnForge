namespace LearnForge.Core;

public sealed record FlashcardExportOptions(FlashcardExportFormat Format = FlashcardExportFormat.Apkg,
    CardPaper Paper = CardPaper.A4, CardPrintMode Mode = CardPrintMode.Duplex, CardBinding Binding = CardBinding.Long,
    double OffsetX = 0, double OffsetY = 0, string? Deck = null, string? Objective = null, string? Tag = null);
