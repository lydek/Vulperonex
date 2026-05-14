using FluentAssertions;
using Vulperonex.Application.Auth;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Auth;

public sealed class CredentialDecryptionExceptionTests
{
    [Fact]
    public void Given_MessageAndInnerException_When_Constructed_Then_ExceptionCarriesBoth()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new CredentialDecryptionException("OAuth credential could not be decrypted.", inner);

        exception.Message.Should().Be("OAuth credential could not be decrypted.");
        exception.InnerException.Should().BeSameAs(inner);
    }
}
