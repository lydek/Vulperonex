using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Vulperonex.Web.Errors;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class ErrorCodeStatusMapTests
{
    [Theory]
    [InlineData(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCodes.UnknownEventTypeKey, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.ConfigKeySecurityNamespace, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCodes.OAuthCredentialNamespace, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCodes.TwitchClientSecretMissing, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.TwitchOAuthExchangeFailed, StatusCodes.Status400BadRequest)]
    public void Given_Phase5ErrorCode_When_StatusIsResolved_Then_ExpectedHttpStatusIsReturned(
        string errorCode,
        int expectedStatusCode)
    {
        ErrorCodeStatusMap.GetStatusCode(errorCode).Should().Be(expectedStatusCode);
    }
}
