using System.Globalization;
using BackEndSearchFakebook.Configuration;
using Microsoft.Extensions.Options;

namespace BackEndSearchFakebook.Infrastructure.Security;

public sealed class GatewayTrustMiddleware(
    RequestDelegate next,
    IOptions<GatewayOptions> options,
    ILogger<GatewayTrustMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/graphql"))
        {
            await next(context);
            return;
        }

        var configuredSecret = options.Value.InternalSharedSecret;
        if (!FixedTimeSecretComparer.IsStrongEnough(configuredSecret))
        {
            logger.LogCritical("The Gateway shared secret is missing or too short.");
            await SecurityProblemWriter.WriteAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Search Gateway trust configuration is unavailable.",
                "SECURITY_MISCONFIGURED",
                context.RequestAborted);
            return;
        }

        if (!TrustedHeaderReader.TryReadSingle(
                context.Request.Headers,
                SearchHeaders.GatewaySecret,
                out var suppliedSecret) ||
            !FixedTimeSecretComparer.Matches(suppliedSecret, configuredSecret))
        {
            await SecurityProblemWriter.WriteAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "A trusted Gateway is required.",
                "INVALID_GATEWAY_CREDENTIALS",
                context.RequestAborted);
            return;
        }

        if (context.Request.Headers.ContainsKey(SearchHeaders.UserId))
        {
            if (!TrustedHeaderReader.TryReadSingle(
                    context.Request.Headers,
                    SearchHeaders.UserId,
                    out var rawUserId) ||
                !long.TryParse(
                    rawUserId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var userId) ||
                userId <= 0)
            {
                await SecurityProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "The trusted user identity is invalid.",
                    "INVALID_TRUSTED_USER",
                    context.RequestAborted);
                return;
            }

            context.Items[TrustedGatewayUserAccessor.HttpContextItemKey] =
                new TrustedGatewayUser(userId);
        }

        await next(context);
    }
}
