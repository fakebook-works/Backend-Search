namespace BackEndSearchFakebook.Infrastructure.Security;

public sealed class UnhandledExceptionMiddleware(
    RequestDelegate next,
    ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled SearchService request failure.");
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await SecurityProblemWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "SearchService could not complete the request.",
                "INTERNAL_ERROR",
                context.RequestAborted);
        }
    }
}
