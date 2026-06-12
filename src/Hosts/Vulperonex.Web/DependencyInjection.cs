using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TwitchLib.EventSub.Websockets.Extensions;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Adapters.Twitch.EventSub;
using Vulperonex.Adapters.Twitch.Helix;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Application.Auth;
using Vulperonex.Application.Counters;
using Vulperonex.Application.Data;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Modules;
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
using Vulperonex.Application.Workflows.Metadata;
using Vulperonex.Application.Workflows.Filters;
using Vulperonex.Application.Workflows.Filters.Matchers;
using Vulperonex.Infrastructure.Workflows.Metadata;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Auth;
using Vulperonex.Infrastructure.Counters;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Expressions;
using Vulperonex.Infrastructure.Cache;
using Vulperonex.Infrastructure.Logging;
using Vulperonex.Infrastructure.Members;
using Vulperonex.Infrastructure.Modules;
using Vulperonex.Infrastructure.Overlay;
using Vulperonex.Infrastructure.Settings;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Infrastructure.Time;
using Vulperonex.Infrastructure.Workflows;
using Vulperonex.Web.Configuration;
using Vulperonex.Web.Members;
using Vulperonex.Web.Overlay;
using Vulperonex.Web.Ports;
using Vulperonex.Web.Simulation;
using Vulperonex.Web.SignalR;
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
        services.AddHttpClient();
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
        services.AddSingleton<IStreamEventBus>(serviceProvider => new InMemoryStreamEventBus(
            InMemoryStreamEventBus.DefaultCapacity,
            overflowStore: null,
            serviceProvider.GetRequiredService<ILogger<InMemoryStreamEventBus>>()));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SimulationAliasRegistry>();
        services.AddOverlayHistory<OverlayChatPayload>("chat", defaultCapacity: 30);
        services.AddOverlayHistory<OverlayAlertPayload>("alerts", defaultCapacity: 20);
        services.AddOverlayHistory<OverlayMemberPayload>("member", defaultCapacity: 20);
        services.AddOverlayHistory<OverlayWidgetPayload>("widgets", defaultCapacity: 20);
        services.AddSingleton<IOverlayWidgetEmitter, SignalROverlayWidgetEmitter>();
        services.AddSingleton<IOverlayEffectEmitter, SignalROverlayEffectEmitter>();
        services.AddSingleton<IWorkflowChatOverlaySink, OverlayWorkflowChatSink>();
        services.AddSingleton<TwitchOAuthSessionStore>();
        services.AddSingleton<PlatformConnectionNotifier>();
        services.AddSingleton<OverlayPresetStore>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<MachineKeyProvider>(serviceProvider =>
            new MachineKeyProvider(
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetRequiredService<IConfiguration>()["Security:RootPath"]));
        services.AddScoped<DatabaseBootstrapper>();
        services.AddScoped<IOAuthTokenStore, OAuthTokenStore>();
        services.AddScoped<TwitchAccessTokenProvider>();
        services.AddScoped<IHelixClient, TwitchHelixClient>();
        services.AddSingleton<TwitchAdapter>();
        services.AddTwitchLibEventSubWebsockets();
        services.AddSingleton<TwitchIrcChatSource>();
        services.AddSingleton<IPlatformChatSender>(serviceProvider => serviceProvider.GetRequiredService<TwitchIrcChatSource>());
        services.AddSingleton<TwitchEventSubSource>();
        services.AddSingleton<IPlatformBadgeCache, TwitchBadgeCache>();
        services.AddSingleton<TwitchBadgeSyncCoordinator>();
        services.AddSingleton<ITwitchRewardCache, TwitchRewardCache>();
        services.AddSingleton<TwitchTokenEndpoint>();
        services.AddSingleton<ITwitchTokenEndpoint>(serviceProvider => serviceProvider.GetRequiredService<TwitchTokenEndpoint>());
        services.AddScoped<WorkflowRuleValidator>();
        services.AddScoped<IWorkflowRuleQueryService, WorkflowRuleQueryService>();
        // Singleton on purpose: WorkflowEngineDispatcher opens a fresh scope per
        // event, so a scoped snapshot cache would start empty every time and the
        // rules table would be re-queried for every chat message. The cache is
        // thread-safe and reads through a scope-bridging query service on miss.
        services.AddSingleton<IRuleSnapshotCache>(serviceProvider => new InMemoryRuleSnapshotCache(
            new ScopedWorkflowRuleQueryService(serviceProvider.GetRequiredService<IServiceScopeFactory>())));
        services.AddScoped<IWorkflowRuleRepository, WorkflowRuleRepository>();
        services.AddScoped<IWorkflowTimerRepository, WorkflowTimerRepository>();
        services.AddSingleton<SystemSettingsBroker>();
        services.AddSingleton<IObservable<SettingChangedEvent>>(sp => sp.GetRequiredService<SystemSettingsBroker>());
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IMemberQueryService, MemberQueryService>();
        services.AddScoped<IMemberAdminService, MemberAdminService>();
        services.AddScoped<IMemberResolver, MemberResolver>();
        services.AddScoped<IMemberStreamStateRepository, MemberStreamStateRepository>();
        services.AddScoped<IMemberAuditLogRepository, MemberAuditLogRepository>();
        services.AddScoped<ITransactionProvider, EfTransactionProvider>();
        services.AddSingleton<IModuleStateService, ModuleStateService>();
        services.AddScoped<IPlatformUserDisplayInfoProvider, PlatformUserDisplayInfoProvider>();
        services.AddScoped<IPlatformUserResolver, PlatformUserResolver>();
        // Singleton for the same reason as IRuleSnapshotCache: per-event scopes
        // rebuilt the L1 LRU on every message, so every avatar/badge lookup hit
        // the database. Capacity comes from the (previously dead) config key.
        services.AddSingleton<Vulperonex.Adapters.Abstractions.IPlatformUserInfoCache>(serviceProvider =>
        {
            using var scope = serviceProvider.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            var capacity = settings
                .GetAsync(SystemSettingKey.OverlayDisplayCacheL1Capacity, 500, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return new PlatformUserDisplayCache(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Math.Clamp(capacity, 1, 100_000));
        });
        services.AddScoped<ICounterRepository, CounterRepository>();
        services.AddSingleton<ISimulationAdapter, SimulationAdapter>();
        services.AddSingleton<IPlatformChatSender, SimulationPlatformChatSender>();
        services.AddScoped<WorkflowConditionEvaluator>();
        services.AddScoped<ITemplateResolver, TemplateResolver>();
        services.AddScoped<IExpressionEvaluator, NCalcExpressionEvaluator>();
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<IWorkflowActionExecutionStore, InMemoryWorkflowActionExecutionStore>();
        services.AddSingleton<IWorkflowThrottleService, InMemoryWorkflowThrottleService>();
        services.AddSingleton<WorkflowChatEchoTracker>();
        services.AddSingleton<ITriggerMetadataProvider, TriggerMetadataProvider>();
        services.AddSingleton<IActionMetadataProvider, ActionMetadataProvider>();
        services.AddSingleton<ITriggerFilterMatcher, MatchChatMessage>();
        services.AddSingleton<ITriggerFilterMatcher, MatchUserDonated>();
        services.AddSingleton<ITriggerFilterMatcher, MatchUserSubscribed>();
        services.AddSingleton<ITriggerFilterMatcher, MatchUserGiftedSub>();
        services.AddSingleton<ITriggerFilterMatcher, MatchChannelRaided>();
        services.AddSingleton<ITriggerFilterMatcher, MatchRewardRedeemed>();
        services.AddSingleton<ITriggerFilterMatcher, MatchWorkflowTimer>();
        services.AddSingleton<TriggerFilterMatcherRegistry>();
        services.AddSingleton<IChatOutbox, InMemoryChatOutbox>();
        services.AddScoped<IWorkflowActionExecutor>(serviceProvider =>
            new SendChatMessageActionExecutor(
                serviceProvider.GetRequiredService<IChatOutbox>(),
                serviceProvider.GetRequiredService<ITemplateResolver>(),
                serviceProvider.GetRequiredService<TemplateRenderer>(),
                serviceProvider.GetRequiredService<IWorkflowChatOverlaySink>(),
                serviceProvider.GetRequiredService<ISystemSettingsService>(),
                serviceProvider.GetRequiredService<IChatOutboxDispatcher>()));
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
        services.AddScoped<IWorkflowActionExecutor, LookupPlatformUserActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, ShoutoutActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, RefundRewardRedemptionActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, ParseChatCommandActionExecutor>();
        services.AddScoped<WorkflowEngine>();
        services.AddScoped<IWorkflowRuleInvoker>(serviceProvider => serviceProvider.GetRequiredService<WorkflowEngine>());
        services.AddScoped<Func<IWorkflowRuleInvoker>>(serviceProvider =>
            () => serviceProvider.GetRequiredService<IWorkflowRuleInvoker>());
        // Database migration runs in VulperonexWebApplication.Build() before the host
        // starts, so hosted services below may read SystemSettings via EF immediately
        // without depending on registration order.
        // DefaultWorkflowRuleSeedService seeds the `!checkin` chat rule on first boot so
        // out-of-the-box installs immediately have a working chat → checkin pipeline.
        services.AddHostedService<DefaultWorkflowRuleSeedService>();
        services.AddHostedService<SimulationAdapterStartupService>();
        services.AddHostedService<TwitchBadgeSyncHostedService>();
        services.AddHostedService<TwitchConnectionOrchestrator>();
        services.AddHostedService<MemberModuleHostedService>();
        services.AddHostedService<WorkflowEngineDispatcher>();
        services.AddHostedService<OverlayEventForwarder>();
        services.AddHostedService<SystemConfigChangedForwarder>();
        services.AddSingleton<ChatOutboxDispatcher>();
        services.AddSingleton<IChatOutboxDispatcher>(serviceProvider => serviceProvider.GetRequiredService<ChatOutboxDispatcher>());
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<ChatOutboxDispatcher>());
        services.AddHostedService<WorkflowInternalEventTypeBootstrapper>();
        services.AddHostedService<WorkflowTimerHostedService>();
        services.AddSingleton<AppLogsSink>(provider =>
            new AppLogsSink(provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddHostedService<AppLogsCleanupWorker>();
        services.AddHostedService<MemberAuditLogsPruningWorker>();
        services.AddSingleton<SqliteBusyTimeoutInterceptor>(_ => new SqliteBusyTimeoutInterceptor(5000));
        services.AddDbContext<VulperonexDbContext>((serviceProvider, options) =>
        {
            var databasePath = serviceProvider.GetRequiredService<IDatabasePathResolver>().Resolve();
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false,
            }.ToString();
            options.UseSqlite(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<SqliteBusyTimeoutInterceptor>());
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
