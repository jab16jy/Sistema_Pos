namespace POS.Domain.Exceptions;

public sealed class UnauthorizedActionException : DomainException
{
    public UnauthorizedActionException(string action)
        : base($"The action '{action}' requires administrator authorization.")
    {
    }
}
