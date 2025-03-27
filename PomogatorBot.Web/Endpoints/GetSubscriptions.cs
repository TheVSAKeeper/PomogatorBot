using FastEndpoints;

namespace PomogatorBot.Web.Endpoints;

public static class GetSubscriptions
{
    public record Response(IEnumerable<SubscriptionMeta> SubscriptionMetas);

    public class Endpoint : EndpointWithoutRequest<Response>
    {
        public override void Configure()
        {
            Get("/subscriptions/meta");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken cancellationToken)
        {
            var meta = SubscriptionExtensions.GetSubscriptionMetadata()
                .Values
                .Where(x => x.Subscription is not Subscribes.None and not Subscribes.All);

            await SendOkAsync(new(meta), cancellationToken);
        }
    }
}
