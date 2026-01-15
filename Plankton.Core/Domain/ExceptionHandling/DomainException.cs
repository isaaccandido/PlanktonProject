namespace Plankton.Core.Domain.ExceptionHandling;

public abstract class DomainException(string message) : Exception(message);