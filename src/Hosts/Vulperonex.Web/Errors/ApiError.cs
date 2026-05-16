namespace Vulperonex.Web.Errors;

public sealed record ApiError(string Error, IReadOnlyDictionary<string, object?>? Meta = null);
