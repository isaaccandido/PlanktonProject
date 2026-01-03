namespace Plankton.Core.Domain.ExceptionHandling;

public abstract class DomainException(string message) : Exception(message);

public sealed class InvalidCommandException(string message) : DomainException(message);

public sealed class UnauthorizedCommandException() : DomainException("Unauthorized");

public sealed class RateLimitExceededException() : DomainException("Rate limit exceeded");