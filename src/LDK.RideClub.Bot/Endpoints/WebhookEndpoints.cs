// ---------------------------------------------------------------------------
// RideClub Bot — WebhookEndpoints (Req 7.1–7.9)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Configuration;
using LDK.RideClub.Bot.Domain.Events;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Endpoints;

/// <summary>
/// Registers webhook HTTP endpoints using ASP.NET Core Minimal APIs.
/// Routes inbound platform webhook requests to the appropriate messaging adapter.
/// </summary>
internal static class WebhookEndpoints
{
    /// <summary>
    /// Maps the webhook route group and registers POST, GET, and catch-all handlers.
    /// </summary>
    /// <param name="app">The web application to register routes on.</param>
    public static void Map(WebApplication app)
    {
        IOptions<WebhookOptions> webhookOptions = app.Services.GetRequiredService<IOptions<WebhookOptions>>();
        string basePath = webhookOptions.Value.BasePath.TrimEnd('/');

        RouteGroupBuilder group = app.MapGroup($"{basePath}/{{platformId}}");

        _ = group.MapPost("/", HandlePostAsync);
        _ = group.MapGet("/", HandleGetAsync);
        _ = group.MapMethods("/", ["PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"], HandleMethodNotAllowed);
    }

    /// <summary>
    /// Handles inbound POST webhook requests from messaging platforms.
    /// Flow: resolve adapter → verify signature → deserialise → process → 200.
    /// </summary>
    private static async Task<IResult> HandlePostAsync(
        string platformId,
        HttpContext context,
        IAdapterRegistry adapterRegistry,
        IEventProcessor eventProcessor,
        CancellationToken ct)
    {
        // Step 1: Resolve adapter by platform identifier (Req 7.2, 8.3)
        IMessagingAdapter? adapter = adapterRegistry.GetAdapter(platformId);

        if (adapter is null)
        {
            return Results.NotFound();
        }

        // Step 2: Verify webhook signature (Req 7.3)
        bool isValid = await adapter.VerifyWebhookAsync(context.Request, ct).ConfigureAwait(false);

        if (!isValid)
        {
            return Results.Unauthorized();
        }

        // Step 3: Deserialise the event (Req 6.5)
        MappingResult<InboundEvent> mappingResult = await adapter.DeserialiseEventAsync(context.Request, ct).ConfigureAwait(false);

        if (mappingResult is MappingFailure<InboundEvent> failure)
        {
            return Results.BadRequest(new { error = failure.Reason });
        }

        // Step 4: Process the event (Req 7.4)
        InboundEvent inboundEvent = ((MappingSuccess<InboundEvent>)mappingResult).Value;
        await eventProcessor.ProcessAsync(inboundEvent, ct).ConfigureAwait(false);

        // Step 5: Return 200 OK
        return Results.Ok();
    }

    /// <summary>
    /// Handles GET requests for platform verification challenges (Req 7.6).
    /// Delegates entirely to the adapter's verification challenge handler.
    /// </summary>
    private static async Task<IResult> HandleGetAsync(
        string platformId,
        HttpContext context,
        IAdapterRegistry adapterRegistry,
        CancellationToken ct)
    {
        // Resolve adapter by platform identifier
        IMessagingAdapter? adapter = adapterRegistry.GetAdapter(platformId);

        if (adapter is null)
        {
            return Results.NotFound();
        }

        // Delegate to adapter's verification challenge handler
        return await adapter.HandleVerificationChallengeAsync(context.Request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns 405 Method Not Allowed for unsupported HTTP methods (Req 7.7).
    /// Includes an Allow header listing the supported methods.
    /// </summary>
    private static IResult HandleMethodNotAllowed(HttpContext context)
    {
        context.Response.Headers["Allow"] = "GET, POST";
        return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
    }
}
