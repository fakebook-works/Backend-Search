using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BackEndSearchFakebook.Authentication
{
    public sealed class InternalSearchServiceAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "InternalSearchServiceSecret";
        public const string PolicyName = "InternalSearchServiceOnly";
        public const string HeaderName = SearchHeaders.InternalSearchServiceSecret;

        private readonly byte[] _expectedSecretHash;

        public InternalSearchServiceAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptions<InternalSearchServiceOptions> secretOptions)
            : base(options, logger, encoder)
        {
            _expectedSecretHash = HashSecret(secretOptions.Value.Secret);
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var suppliedValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (suppliedValues.Count != 1 || string.IsNullOrEmpty(suppliedValues[0]))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid internal SearchService credentials."));
            }

            var suppliedSecretHash = HashSecret(suppliedValues[0]!);
            if (!CryptographicOperations.FixedTimeEquals(suppliedSecretHash, _expectedSecretHash))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid internal SearchService credentials."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "InternalSearchServiceClient"),
                new Claim(ClaimTypes.Name, "InternalSearchServiceClient")
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = SchemeName;
            await SecurityProblemWriter.WriteAsync(
                Context,
                StatusCodes.Status401Unauthorized,
                $"Missing or invalid {HeaderName} header.",
                "INVALID_INTERNAL_CREDENTIALS",
                Context.RequestAborted);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            return SecurityProblemWriter.WriteAsync(
                Context,
                StatusCodes.Status403Forbidden,
                "The internal caller is not authorized for this operation.",
                "FORBIDDEN",
                Context.RequestAborted);
        }

        private static byte[] HashSecret(string secret)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }
    }
}
