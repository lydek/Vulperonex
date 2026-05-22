using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Members;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Auth;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
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
        services.AddSingleton<TwitchOAuthSessionStore>();
        services.AddSingleton<PlatformConnectionNotifier>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<MachineKeyProvider>(serviceProvider =>
            new MachineKeyProvider(
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetRequiredService<IConfiguration>()["Security:RootPath"]));
        services.AddScoped<DatabaseBootstrapper>();
        services.AddScoped<IOAuthTokenStore, OAuthTokenStore>();
        services.AddSingleton<TwitchTokenEndpoint>();
        services.AddSingleton<ITwitchTokenEndpoint>(serviceProvider => serviceProvider.GetRequiredService<TwitchTokenEndpoint>());
        services.AddScoped<WorkflowRuleValidator>();
        services.AddScoped<IWorkflowRuleQueryService, WorkflowRuleQueryService>();
        services.AddScoped<IWorkflowRuleRepository, WorkflowRuleRepository>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IMemberQueryService, MemberQueryService>();
        services.AddScoped<IMemberAdminService, MemberAdminService>();
        services.AddScoped<IMemberResolver, MemberResolver>();
        services.AddScoped<IMemberStreamStateRepository, MemberStreamStateRepository>();
        services.AddSingleton<ISimulationAdapter, SimulationAdapter>();
        services.AddScoped<WorkflowConditionEvaluator>();
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<IWorkflowActionExecutionStore, InMemoryWorkflowActionExecutionStore>();
        services.AddScoped<IWorkflowActionExecutor, SendChatMessageActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, InvokeSubWorkflowActionExecutor>();
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
