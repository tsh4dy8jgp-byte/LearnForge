namespace LearnForge.Core;

public sealed record FlashcardExportPayload(string Id, string Title, string Version, FlashcardDeck[] Decks,
    FlashcardUnit[] Units, FlashcardExportOptions Options);
