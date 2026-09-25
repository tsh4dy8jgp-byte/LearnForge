namespace LearnForge.Api.Contracts.Common;

public sealed class DomainError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
