using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Members;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Members;
using Vulperonex.Infrastructure.Settings;
using Vulperonex.Infrastructure.Time;
using Vulperonex.Infrastructure.Workflows;
using Vulperonex.Web.Configuration;
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
        services.AddSingleton<SimulationAliasRegistry>();
        services.AddScoped<WorkflowRuleValidator>();
        services.AddScoped<IWorkflowRuleQueryService, WorkflowRuleQueryService>();
        services.AddScoped<IWorkflowRuleRepository, WorkflowRuleRepository>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IMemberQueryService, MemberQueryService>();
        services.AddScoped<ISimulationAdapter, SimulationAdapter>();
        services.AddScoped<WorkflowConditionEvaluator>();
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<IWorkflowActionExecutionStore, InMemoryWorkflowActionExecutionStore>();
        services.AddScoped<IWorkflowActionExecutor, SendChatMessageActionExecutor>();
        services.AddScoped<IWorkflowActionExecutor, InvokeSubWorkflowActionExecutor>();
        if (!services.Any(service => service.ServiceType == typeof(IPlatformChatSender)))
        {
            services.AddSingleton<IPlatformChatSender, NoOpPlatformChatSender>();
        }
        services.AddScoped<WorkflowEngine>();
        services.AddScoped<IWorkflowRuleInvoker>(serviceProvider => serviceProvider.GetRequiredService<WorkflowEngine>());
        services.AddHostedService<SimulationAdapterStartupService>();
        services.AddHostedService<OverlayEventForwarder>();
        services.AddDbContext<VulperonexDbContext>((serviceProvider, options) =>
        {
            var databasePath = serviceProvider.GetRequiredService<IDatabasePathResolver>().Resolve();
            options.UseSqlite($"Data Source={databasePath}");
        });

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
