using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PomogatorBot.Web.Services.ExternalClients;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PomogatorBot.Web.Middlewares;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ExternalClientService externalClientService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKeyScheme";
    private const string ApiKeyHeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues) == false)
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key header is missing"));
        }

        var providedKey = headerValues.ToString();

        var client = externalClientService.TryValidateAsync(providedKey, CancellationToken.None).GetAwaiter().GetResult();

        if (client == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var clientClaims = new List<Claim>
        {
            new(ClaimTypes.Name, client!.Name),
            new("external_client_id", client.Id.ToString()),
        };

        var clientIdentity = new ClaimsIdentity(clientClaims, Scheme.Name);
        var principal = new ClaimsPrincipal(clientIdentity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
