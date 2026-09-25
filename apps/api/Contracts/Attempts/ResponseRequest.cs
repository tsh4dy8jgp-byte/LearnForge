namespace LearnForge.Api.Contracts.Attempts;

public sealed record ResponseRequest(int Revision, string RequestId, string QuestionId, Answer Answer, bool Check = false);
