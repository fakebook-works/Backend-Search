using System.Diagnostics;

namespace BackEndSearchFakebook.Infrastructure.Security;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Resolve(context);
        context.TraceIdentifier = correlationId;
        context.Request.Headers[SearchHeaders.CorrelationId] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[SearchHeaders.CorrelationId] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }

    internal static string Resolve(HttpContext context)
    {
        if (TrustedHeaderReader.TryReadSingle(
                context.Request.Headers,
                SearchHeaders.CorrelationId,
                out var supplied) &&
            IsValid(supplied))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsValid(string value)
    {
        if (value.Length is 0 or > MaximumLength)
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':' or '/');
    }
}
