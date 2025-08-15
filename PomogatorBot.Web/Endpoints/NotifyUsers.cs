using FastEndpoints;
using PomogatorBot.Web.Middlewares;
using PomogatorBot.Web.Services.ExternalClients;

namespace PomogatorBot.Web.Endpoints;

public static class NotifyUsers
{
    public class Endpoint(UserService userService, ExternalClientService externalClientService) : Endpoint<Request, Response>
    {
        public override void Configure()
        {
            Post("/notify");
            AuthSchemes(ApiKeyAuthenticationHandler.SchemeName);
        }

        public override async Task HandleAsync(Request request, CancellationToken ct)
        {
            var clientId = User.Claims.FirstOrDefault(c => c.Type == "external_client_id")?.Value;

            if (string.IsNullOrEmpty(clientId))
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            if (Guid.TryParse(clientId, out var clientGuid) == false)
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            var client = await externalClientService.TryGetByIdAsync(clientGuid, ct);

            if (client == null)
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            var response = await userService.NotifyAsync(request.Message, request.Subscribes, cancellationToken: ct);
            await SendOkAsync(new(response.TotalUsers, response.SuccessfulSends, response.FailedSends, DateTime.UtcNow), ct);
        }
    }

    public record Request(string Message, Subscribes Subscribes);

    public record Response(int TotalUsers, int SuccessfulSends, int FailedSends, DateTime Timestamp);
}
