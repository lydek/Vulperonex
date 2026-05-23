using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Application.Counters;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Time;
using Vulperonex.Application.Twitch;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Auth;
using Vulperonex.Infrastructure.Counters;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Expressions;
using Vulperonex.Infrastructure.Logging;
using Vulperonex.Infrastructure.Members;
using Vulperonex.Infrastructure.Overlay;
using Vulperonex.Infrastructure.Settings;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Infrastructure.Time;
using Vulperonex.Infrastructure.Workflows;
using Vulperonex.Web.Configuration;
using Vulperonex.Web.Members;
using Vulperonex.Web.Ports;
using Vulperonex.Web.Simulation;
using Vulperonex.Web.SignalR;
using Vulperonex.Web.TwitchAuth;
using Vulperonex.Web.Validation;

namespace Vulperonex.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddVulperonexWeb(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddOpenApi();
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddSingleton<IDatabasePathResolver, DatabasePathResolver>();
        services.AddSingleton<IPortAvailabilityProbe, SocketPortAvailabilityProbe>();
        services.AddSingleton<PortPairAllocator>();
        services.AddSingleton<IStreamEventTypeRegistry, InMemoryStreamEventTypeRegistry>();
        services.AddSingleton<IStreamEventBus, InMemoryStreamEventBus>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SimulationAliasRegistry>();
        services.AddOverlayHistory<OverlayChatPayload>("chat", defaultCapacity: 30);
        services.AddOverlayHistory<OverlayAlertPayload>("alerts", defaultCapacity: 20);
        services.AddOverlayHistory<OverlayMemberPayload>("member", defaultCapacity: 20);
        services.AddOverlayHistory<OverlayWidgetPayload>("widgets", defaultCapacity: 20);
        services.AddSingleton<IOverlayWidgetEmitter, SignalROverlayWidgetEmitter>();
        services.AddSingleton<IOverlayEffectEmitter, SignalROverlayEffectEmitter>();
        services.AddSingleton<TwitchOAuthSessionStore>();
        services.AddSingleton<PlatformConnectionNotifier>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<MachineKeyProvider>(serviceProvider =>
            new MachineKeyProvider(
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetRequiredService<IConfiguration>()["Security:RootPath"]));
        services.AddScoped<DatabaseBootstrapper>();
        services.AddScoped<IOAuthTokenStore, OAuthTokenStore>();
        services.AddScoped<TwitchAccessTokenProvider>();
        services.AddScoped<ITwitchHelixClient, TwitchHelixClient>();
        services.AddSingleton<TwitchTokenEndpoint>();
        services.AddSingleton<ITwitchTokenEndpoint>(serviceProvider => serviceProvider.GetRequiredService<TwitchTokenEndpoint>());
        services.AddScoped<WorkflowRuleValidator>();
        services.AddScoped<IWorkflowRuleQueryService, WorkflowRuleQueryService>();
        services.AddScoped<IRuleSnapshotCache, InMemoryRuleSnapshotCache>();
        services.AddScoped<IWorkflowRuleRepository, WorkflowRuleRepository>();
        services.AddScoped<IWorkflowTimerRepository, WorkflowTimerRepository>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IMemberQueryService, MemberQueryService>();
        services.AddScoped<IMemberAdminService, MemberAdminService>();
        services.AddScoped<IMemberResolver, MemberResolver>();
        services.AddScoped<IMemberStreamStateRepository, MemberStreamStateRepository>();
        services.AddScoped<ICounterRepository, CounterRepository>();
        services.AddSingleton<ISimulationAdapter, SimulationAdapter>();
        services.AddScoped<WorkflowConditionEvaluator>();
        services.AddScoped<ITemplateResolver, TemplateResolver>();
        services.AddScoped<IExpressionEvaluator, NCalcExpressionEvaluator>();
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<IWorkflowActionExecutionStore, InMemoryWorkflowActionExecutionStore>();
        services.AddSingleton<IWorkflowThrottleService, InMemoryWorkflowThrottleService>();
        services.AddSingleton<IChatOutbox, InMemoryChatOutbox>();
        services.AddScoped<IWorkflowActionExecutor>(serviceProvider =>
            new SendChatMessageActionExecutor(
                serviceProvider.GetRequiredService<IChatOutbox>(),
                serviceProvider.GetRequiredService<TemplateRenderer>()));
        services.AddScoped<IWorkflowActionExecutor, InvokeSubWorkflowActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, DelayActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, StopIfActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, RandomPickerActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, UpdateCounterActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, TriggerCheckInActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, AddLotteryTicketsActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, EmitSystemEventActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, TriggerEffectActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, EmitOverlayWidgetActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, LookupTwitchUserActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, ShoutoutActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, RefundTwitchRedemptionActionExecutor>();
        // Default sender is only a fallback. Real platform registrations must happen before this method.
        if (!services.Any(service => service.ServiceType == typeof(IPlatformChatSender)))
        {
            services.AddSingleton<IPlatformChatSender, NoOpPlatformChatSender>();
        }
        services.AddScoped<WorkflowEngine>();
        services.AddScoped<IWorkflowRuleInvoker>(serviceProvider => serviceProvider.GetRequiredService<WorkflowEngine>());
        services.AddHostedService<SimulationAdapterStartupService>();
        services.AddHostedService<MemberModuleHostedService>();
        services.AddHostedService<OverlayEventForwarder>();
        services.AddHostedService<ChatOutboxDispatcher>();
        services.AddHostedService<DatabaseMigrationStartupService>();
        services.AddSingleton<AppLogsSink>(provider =>
            new AppLogsSink(provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddHostedService<AppLogsCleanupWorker>();
        services.AddHostedService<Vulperonex.Web.Logging.LogLevelHotReloadWorker>();
        services.AddDbContext<VulperonexDbContext>((serviceProvider, options) =>
        {
            var databasePath = serviceProvider.GetRequiredService<IDatabasePathResolver>().Resolve();
            options.UseSqlite($"Data Source={databasePath}");
        });

        return services;
    }
}

internal static class OverlayHistoryServiceCollectionExtensions
{
    public static IServiceCollection AddOverlayHistory<TPayload>(
        this IServiceCollection services,
        string hubName,
        int defaultCapacity)
    {
        services.AddSingleton(new OverlayHistoryOptions<TPayload>
        {
            HubName = hubName,
            DefaultCapacity = defaultCapacity,
        });
        services.AddSingleton<IOverlayHistoryService<TPayload>, OverlayHistoryService<TPayload>>();
        return services;
    }
}

internal sealed class NoOpPlatformChatSender : IPlatformChatSender
{
    public string Platform => "simulation";

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
