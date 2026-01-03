namespace Plankton.Core.Domain.ExceptionHandling;

public abstract class DomainException(string message) : Exception(message);

public sealed class InvalidCommandException(string message, string[]? allowedArgs = null) : DomainException(message)
{
    public string[]? AllowedArgs { get; } = allowedArgs;
}

public sealed class UnauthorizedCommandException() : DomainException("Unauthorized");

public sealed class RateLimitExceededException() : DomainException("Rate limit exceeded");