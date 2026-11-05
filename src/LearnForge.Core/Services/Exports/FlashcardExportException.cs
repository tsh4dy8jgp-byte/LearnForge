namespace LearnForge.Core;

public sealed class FlashcardExportException(string message, bool busy = false) : Exception(message)
{
    public bool Busy { get; } = busy;
}
