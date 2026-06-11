using System.Reflection;
using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Infrastructure.Workflows.Metadata;

public sealed class ActionMetadataProvider : IActionMetadataProvider
{
    private readonly IReadOnlyList<ActionMetadataDto> _actions;

    public ActionMetadataProvider()
    {
        var actionMetadataList = new List<ActionMetadataDto>();
        var derivedTypeAttrs = typeof(WorkflowAction).GetCustomAttributes<JsonDerivedTypeAttribute>();

        foreach (var attr in derivedTypeAttrs)
        {
            var actionClass = attr.DerivedType;
            var actionTypeKey = attr.TypeDiscriminator as string;
            if (actionTypeKey == null) continue;

            var metaAttr = actionClass.GetCustomAttribute<ActionMetadataAttribute>();
            if (metaAttr == null)
            {
                throw new InvalidOperationException($"Action type '{actionClass.Name}' is missing [ActionMetadata] attribute.");
            }

            var parameters = new List<ActionParamMetadataDto>();
            var properties = actionClass.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var paramAttr = prop.GetCustomAttribute<ActionParamAttribute>();
                if (paramAttr != null)
                {
                    parameters.Add(new ActionParamMetadataDto(
                        prop.Name,
                        paramAttr.Label,
                        paramAttr.Type,
                        paramAttr.Required,
                        paramAttr.Help,
                        paramAttr.Advanced,
                        paramAttr.Options is { Length: > 0 } options ? options : null));
                }
            }

            actionMetadataList.Add(new ActionMetadataDto(
                actionTypeKey,
                metaAttr.DisplayName,
                metaAttr.Description,
                parameters));
        }

        _actions = actionMetadataList.AsReadOnly();
    }

    public IReadOnlyList<ActionMetadataDto> GetAvailableActions()
    {
        return _actions;
    }
}
