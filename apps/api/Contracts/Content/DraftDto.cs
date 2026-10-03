namespace LearnForge.Api.Contracts.Content;

public sealed record DraftDto(string Id, string Title, string Source, int Revision, DateTime UpdatedAt)
{
    public static DraftDto From(AuthoringDraft draft) => new(draft.Id, draft.Title, draft.Source, draft.Revision, draft.UpdatedAt);
}
