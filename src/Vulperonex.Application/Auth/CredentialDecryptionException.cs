namespace Vulperonex.Application.Auth;

public sealed class CredentialDecryptionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
