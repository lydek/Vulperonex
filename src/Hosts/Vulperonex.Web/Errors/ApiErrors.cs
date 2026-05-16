namespace Vulperonex.Web.Errors;

public static class ApiErrors
{
    public static IResult ToResult(string errorCode, int statusCode, IReadOnlyDictionary<string, object?>? meta = null)
    {
        return Results.Json(new ApiError(errorCode, meta), statusCode: statusCode);
    }
}
