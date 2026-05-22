using FluentAssertions;
using Vulperonex.Application.Expressions;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Expressions;

public sealed class TemplateResolverTests
{
    [Fact]
    public void Given_TemplateWithoutPlaceholders_When_Resolved_Then_OriginalStringInstanceIsReturned()
    {
        var resolver = new TemplateResolver();
        var template = "plain text";

        var resolved = resolver.Resolve(template, ExpressionContext.Empty);

        resolved.Should().BeSameAs(template);
    }

    [Fact]
    public void Given_TemplateWithKnownPlaceholders_When_Resolved_Then_ContextValuesAreExpanded()
    {
        var resolver = new TemplateResolver();
        var context = NewContext();

        var resolved = resolver.Resolve(
            "{Trigger.EventTypeKey}:{Member.DisplayName}:{Step.Lookup.Avatar}:{Args.Target}",
            context);

        resolved.Should().Be("user.message:Alice:https://example.test/avatar.png:bob");
    }

    [Fact]
    public void Given_TemplateWithMissingPlaceholder_When_ResolvedInFailSoftMode_Then_MissingValueIsEmpty()
    {
        var resolver = new TemplateResolver();

        var resolved = resolver.Resolve("Hello {Trigger.Missing}", NewContext());

        resolved.Should().Be("Hello ");
    }

    [Fact]
    public void Given_TemplateWithMissingPlaceholder_When_ResolvedInStrictMode_Then_ExceptionNamesPlaceholder()
    {
        var resolver = new TemplateResolver();

        var act = () => resolver.Resolve(
            "Hello {Trigger.Missing}",
            NewContext(),
            new TemplateResolutionOptions { StrictMissingPlaceholders = true });

        act.Should()
            .Throw<TemplateMissingPlaceholderException>()
            .Which.Placeholder.Should()
            .Be("Trigger.Missing");
    }

    private static ExpressionContext NewContext()
    {
        return new ExpressionContext(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EventTypeKey"] = "user.message",
            },
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Lookup"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Avatar"] = "https://example.test/avatar.png",
                },
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Target"] = "bob",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisplayName"] = "Alice",
            });
    }
}
