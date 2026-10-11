namespace QuireClone.Domain;

/// <summary>A request breaks a rule of the application. The message is shown to the user.</summary>
public sealed class DomainValidationException(string message) : Exception(message);

/// <summary>A requested entry does not exist (any more). The message is shown to the user.</summary>
public sealed class EntityNotFoundException(string message) : Exception(message);
