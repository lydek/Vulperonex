using System.Text.Json;
using FluentAssertions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Expressions;
using Vulperonex.Plugins.Abstractions;
using Xunit;

namespace Vulperonex.Tests.Unit.Plugins;

public sealed class PluginContractTests
{
    [Fact]
    public void Given_PluginContextContracts_When_Inspected_Then_TheyDoNotExposeServiceProviderOrFullRegistry()
    {
        var propertyTypes = typeof(IPluginContext).GetProperties()
            .Select(property => property.PropertyType)
            .Concat(typeof(IPluginActionContext).GetProperties().Select(property => property.PropertyType))
            .ToArray();

        propertyTypes.Should().NotContain(typeof(IServiceProvider));
        propertyTypes.Should().NotContain(typeof(IStreamEventTypeRegistry));
    }

    [Fact]
    public void Given_PluginEventTypeRegistrar_When_PluginRegistersCustomKey_Then_KeyIsWorkflowVisible()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var registrar = new PluginEventTypeRegistrar(registry);

        registrar.Register(new PluginEventTypeMetadata("custom.event", "Custom event"));

        registry.IsKnownForWorkflow("custom.event").Should().BeTrue();
    }

    [Fact]
    public void Given_PluginEventTypeRegistrarInterface_When_Inspected_Then_ItDoesNotDependOnApplicationEventTypes()
    {
        var registerMethod = typeof(IPluginEventTypeRegistrar).GetMethod(nameof(IPluginEventTypeRegistrar.Register));
        var parameterTypes = registerMethod!.GetParameters().Select(parameter => parameter.ParameterType);

        parameterTypes.Should().NotContain(typeof(StreamEventTypeMetadata));
        parameterTypes.Should().AllSatisfy(parameterType =>
            parameterType.Namespace.Should().NotStartWith("Vulperonex.Application"));
    }

    [Fact]
    public async Task Given_InvokePluginAction_When_Executed_Then_RegisteredPluginReceivesJsonElementParams()
    {
        var plugin = new RecordingPlugin();
        var executor = new InvokePluginActionExecutor(new StaticPluginRegistry([plugin]), new TemplateResolver());
        using var document = JsonDocument.Parse("""{"count":3,"enabled":true,"name":"test"}""");
        var action = new InvokePluginAction
        {
            PluginId = "test-plugin",
            ActionId = "do-work",
            Params = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone()),
        };
        var streamEvent = new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };
        var context = new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "rule-1", Name = "Rule", EventTypeKey = StreamEventKeys.UserSentMessage },
            ActionIndex: 2);

        await executor.ExecuteAsync(action, context, TestContext.Current.CancellationToken);

        plugin.Calls.Should().ContainSingle();
        plugin.Calls[0].ActionId.Should().Be("do-work");
        plugin.Calls[0].Context.Params["name"].GetString().Should().Be("test");
        plugin.Calls[0].Context.Params["count"].GetInt32().Should().Be(3);
        plugin.Calls[0].Context.Params["enabled"].GetBoolean().Should().BeTrue();
        plugin.Calls[0].Context.Args.Should().BeEmpty();
        plugin.Calls[0].Context.ActionExecutionKey.Should().Be(new ActionExecutionKey(streamEvent.EventId, "rule-1", 2));
    }

    [Fact]
    public async Task Given_InvokePluginActionWithArgs_When_Executed_Then_PluginReceivesResolvedArgs()
    {
        var plugin = new RecordingPlugin();
        var executor = new InvokePluginActionExecutor(new StaticPluginRegistry([plugin]), new TemplateResolver());
        var action = new InvokePluginAction
        {
            PluginId = "test-plugin",
            ActionId = "do-work",
            Args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["target"] = "{Args.Target}",
                ["message"] = "hello {Trigger.DisplayName}",
            },
        };
        var streamEvent = new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };
        var context = new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "rule-1", Name = "Rule", EventTypeKey = StreamEventKeys.UserSentMessage },
            ActionIndex: 2,
            ExpressionContext: new ExpressionContext(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DisplayName"] = "Alice",
                },
                new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Target"] = "Bob",
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)));

        await executor.ExecuteAsync(action, context, TestContext.Current.CancellationToken);

        plugin.Calls.Should().ContainSingle();
        plugin.Calls[0].Context.Args.Should().Equal(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["target"] = "Bob",
                ["message"] = "hello Alice",
            });
    }

    private sealed class RecordingPlugin : IVulperonexPlugin
    {
        public string Name => "test-plugin";
        public List<(string ActionId, IPluginActionContext Context)> Calls { get; } = [];

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteActionAsync(
            string actionId,
            IPluginActionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((actionId, context));
            return Task.CompletedTask;
        }
    }
}
