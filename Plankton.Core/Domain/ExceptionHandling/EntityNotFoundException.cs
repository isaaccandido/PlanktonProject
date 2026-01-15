namespace Plankton.Core.Domain.ExceptionHandling;

public sealed class EntityNotFoundException(string message) : DomainException(message);