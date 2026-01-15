namespace Plankton.Core.Domain.ExceptionHandling;

public sealed class RateLimitExceededException() : DomainException("Rate limit exceeded");