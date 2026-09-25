using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Attempts;

public sealed record ResponseRequest(int Revision, [property: Required, MaxLength(64)] string RequestId,
    [property: Required, MaxLength(100)] string QuestionId, Answer Answer, bool Check = false);
