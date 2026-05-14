namespace Vulperonex.Application.EventTypes;

public interface IStreamEventTypeRegistry
{
    void Register(StreamEventTypeMetadata metadata);

    bool IsKnown(string key);

    bool IsKnownForWorkflow(string key);

    IReadOnlyCollection<StreamEventTypeMetadata> GetAll();
}
