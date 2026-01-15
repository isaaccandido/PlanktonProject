namespace Plankton.Core.Domain.ExceptionHandling;

public sealed class InvalidCommandException(string message, string[]? allowedArgs = null) : DomainException(message)
{
    public string[]? AllowedArgs { get; } = allowedArgs;
}