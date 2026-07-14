namespace BackEndSearchFakebook.Infrastructure.Security;

public static class SecurityProblemWriter
{
    public static Task WriteAsync(
        HttpContext context,
        int statusCode,
        string title,
        string code,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.CacheControl = "no-store";

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
