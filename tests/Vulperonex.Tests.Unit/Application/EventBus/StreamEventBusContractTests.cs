using FluentAssertions;
using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.EventBus;

public sealed class StreamEventBusContractTests
{
    [Fact]
    public void Given_StreamEventBusContract_When_Inspected_Then_ItExposesPublishSubscribeAndIdleMethods()
    {
        var methodNames = typeof(IStreamEventBus)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        methodNames.Should().BeEquivalentTo("PublishAsync", "Subscribe", "WaitForIdleAsync");
    }

    [Fact]
    public void Given_PublishContract_When_Inspected_Then_ItAcceptsStreamEventAndCancellationToken()
    {
        var publish = typeof(IStreamEventBus).GetMethod("PublishAsync");

        publish.Should().NotBeNull();
        publish!.ReturnType.Should().Be<Task>();
        publish.GetParameters().Should().SatisfyRespectively(
            parameter => parameter.ParameterType.Should().Be<IStreamEvent>(),
            parameter => parameter.ParameterType.Should().Be<CancellationToken>());
    }

    [Fact]
    public void Given_SubscribeContract_When_Inspected_Then_ItRequiresStreamEventHandler()
    {
        var subscribe = typeof(IStreamEventBus).GetMethod("Subscribe");

        subscribe.Should().NotBeNull();
        subscribe!.ReturnType.Should().Be<IDisposable>();

        var genericParameter = subscribe.GetGenericArguments().Should().ContainSingle().Subject;
        genericParameter.GetGenericParameterConstraints().Should().Contain(typeof(IStreamEvent));

        var handlerParameter = subscribe.GetParameters().Should().ContainSingle().Subject;
        handlerParameter.ParameterType.Should().Be(typeof(Func<,,>).MakeGenericType(
            genericParameter,
            typeof(CancellationToken),
            typeof(Task)));
    }

    [Fact]
    public void Given_WaitForIdleContract_When_Inspected_Then_ItDoesNotExposeHandlerErrorCounts()
    {
        var waitForIdle = typeof(IStreamEventBus).GetMethod("WaitForIdleAsync");

        waitForIdle.Should().NotBeNull();
        waitForIdle!.ReturnType.Should().Be<Task>();
        waitForIdle.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<CancellationToken>();
    }
}
