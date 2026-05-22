using System.Diagnostics;

namespace LDK.RideClub.Bot.Observability;

/// <summary>
/// Provides a custom <see cref="ActivitySource"/> for messaging adapter operations.
/// Helper methods create child spans with <c>messaging.platform</c> and
/// <c>messaging.operation</c> attributes for observability.
/// </summary>
/// <remarks>
/// The <see cref="SourceName"/> must match the source name registered with
/// <c>AddSource()</c> in the OpenTelemetry tracing configuration.
/// </remarks>
public static class MessagingActivitySource
{
    /// <summary>
    /// The activity source name registered with OpenTelemetry.
    /// </summary>
    public const string SourceName = "LDK.RideClub.Bot.Messaging";

    private static readonly ActivitySource _source = new(SourceName);

    /// <summary>
    /// Starts an activity for a webhook verification operation.
    /// </summary>
    /// <param name="platformId">The messaging platform identifier (e.g., "whatsapp").</param>
    /// <returns>
    /// An <see cref="Activity"/> instance that the caller should dispose when the operation
    /// completes, or <see langword="null"/> if no listener is sampling this activity.
    /// </returns>
    public static Activity? StartVerifyWebhook(string platformId)
    {
        return StartMessagingActivity(platformId, "verify");
    }

    /// <summary>
    /// Starts an activity for a deserialise event operation.
    /// </summary>
    /// <param name="platformId">The messaging platform identifier (e.g., "whatsapp").</param>
    /// <returns>
    /// An <see cref="Activity"/> instance that the caller should dispose when the operation
    /// completes, or <see langword="null"/> if no listener is sampling this activity.
    /// </returns>
    public static Activity? StartDeserialiseEvent(string platformId)
    {
        return StartMessagingActivity(platformId, "deserialise");
    }

    /// <summary>
    /// Starts an activity for a send message operation.
    /// </summary>
    /// <param name="platformId">The messaging platform identifier (e.g., "whatsapp").</param>
    /// <returns>
    /// An <see cref="Activity"/> instance that the caller should dispose when the operation
    /// completes, or <see langword="null"/> if no listener is sampling this activity.
    /// </returns>
    public static Activity? StartSendMessage(string platformId)
    {
        return StartMessagingActivity(platformId, "send");
    }

    /// <summary>
    /// Starts an activity for a verification challenge handling operation.
    /// </summary>
    /// <param name="platformId">The messaging platform identifier (e.g., "whatsapp").</param>
    /// <returns>
    /// An <see cref="Activity"/> instance that the caller should dispose when the operation
    /// completes, or <see langword="null"/> if no listener is sampling this activity.
    /// </returns>
    public static Activity? StartHandleChallenge(string platformId)
    {
        return StartMessagingActivity(platformId, "challenge");
    }

    private static Activity? StartMessagingActivity(string platformId, string operation)
    {
        Activity? activity = _source.StartActivity(operation);

        if (activity is not null)
        {
            _ = activity.SetTag("messaging.platform", platformId);
            _ = activity.SetTag("messaging.operation", operation);
        }

        return activity;
    }
}
