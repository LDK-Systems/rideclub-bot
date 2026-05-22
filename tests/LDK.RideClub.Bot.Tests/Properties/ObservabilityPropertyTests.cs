// ---------------------------------------------------------------------------
// RideClub Bot — Observability Property Tests
// Feature: rideclub-bot-platform, Property 14: Adapter Span Attributes
// ---------------------------------------------------------------------------

using System.Diagnostics;

using FluentAssertions;

using LDK.RideClub.Bot.Observability;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Properties;

/// <summary>
/// Property-based tests verifying that the <see cref="MessagingActivitySource"/>
/// creates spans with the correct <c>messaging.platform</c> and
/// <c>messaging.operation</c> attributes for all adapter operations.
/// </summary>
/// <remarks>
/// <para><b>Validates: Requirements 11.5</b></para>
/// </remarks>
public sealed class ObservabilityPropertyTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _capturedActivities = [];

    public ObservabilityPropertyTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == MessagingActivitySource.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivities.Add(activity),
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();

        foreach (Activity activity in _capturedActivities)
        {
            activity.Dispose();
        }
    }

    // Feature: rideclub-bot-platform, Property 14: Adapter Span Attributes

    /// <summary>
    /// StartVerifyWebhook creates a span with messaging.platform and messaging.operation attributes.
    /// Verified across multiple platform identifiers.
    /// </summary>
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("discord")]
    [InlineData("signal")]
    [InlineData("slack")]
    public void VerifyWebhookCreatesSpanWithPlatformAndOperationAttributes(string platformId)
    {
        // Act
        using Activity? activity = MessagingActivitySource.StartVerifyWebhook(platformId);

        // Assert
        _ = activity.Should().NotBeNull("ActivityListener is registered");
        _ = activity!.GetTagItem("messaging.platform").Should().Be(platformId);
        _ = activity.GetTagItem("messaging.operation").Should().Be("verify");
    }

    /// <summary>
    /// StartDeserialiseEvent creates a span with messaging.platform and messaging.operation attributes.
    /// Verified across multiple platform identifiers.
    /// </summary>
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("discord")]
    [InlineData("signal")]
    [InlineData("slack")]
    public void DeserialiseEventCreatesSpanWithPlatformAndOperationAttributes(string platformId)
    {
        // Act
        using Activity? activity = MessagingActivitySource.StartDeserialiseEvent(platformId);

        // Assert
        _ = activity.Should().NotBeNull("ActivityListener is registered");
        _ = activity!.GetTagItem("messaging.platform").Should().Be(platformId);
        _ = activity.GetTagItem("messaging.operation").Should().Be("deserialise");
    }

    /// <summary>
    /// StartSendMessage creates a span with messaging.platform and messaging.operation attributes.
    /// Verified across multiple platform identifiers.
    /// </summary>
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("discord")]
    [InlineData("signal")]
    [InlineData("slack")]
    public void SendMessageCreatesSpanWithPlatformAndOperationAttributes(string platformId)
    {
        // Act
        using Activity? activity = MessagingActivitySource.StartSendMessage(platformId);

        // Assert
        _ = activity.Should().NotBeNull("ActivityListener is registered");
        _ = activity!.GetTagItem("messaging.platform").Should().Be(platformId);
        _ = activity.GetTagItem("messaging.operation").Should().Be("send");
    }

    /// <summary>
    /// StartHandleChallenge creates a span with messaging.platform and messaging.operation attributes.
    /// Verified across multiple platform identifiers.
    /// </summary>
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("discord")]
    [InlineData("signal")]
    [InlineData("slack")]
    public void HandleChallengeCreatesSpanWithPlatformAndOperationAttributes(string platformId)
    {
        // Act
        using Activity? activity = MessagingActivitySource.StartHandleChallenge(platformId);

        // Assert
        _ = activity.Should().NotBeNull("ActivityListener is registered");
        _ = activity!.GetTagItem("messaging.platform").Should().Be(platformId);
        _ = activity.GetTagItem("messaging.operation").Should().Be("challenge");
    }

    /// <summary>
    /// All messaging activity source methods produce spans with both required attributes
    /// for any non-empty platform identifier string.
    /// </summary>
    [Fact]
    public void AllOperationsProduceSpansWithRequiredAttributes()
    {
        // Arrange — test across a range of platform identifiers
        string[] platforms = ["whatsapp", "telegram", "discord", "x", "a1b2c3", "longplatformname123"];

        foreach (string platform in platforms)
        {
            _capturedActivities.Clear();

            // Act — call each operation
            using Activity? verify = MessagingActivitySource.StartVerifyWebhook(platform);
            using Activity? deserialise = MessagingActivitySource.StartDeserialiseEvent(platform);
            using Activity? send = MessagingActivitySource.StartSendMessage(platform);
            using Activity? challenge = MessagingActivitySource.StartHandleChallenge(platform);

            // Assert — all activities have the correct attributes
            Activity?[] activities = [verify, deserialise, send, challenge];
            string[] expectedOps = ["verify", "deserialise", "send", "challenge"];

            for (int i = 0; i < activities.Length; i++)
            {
                _ = activities[i].Should().NotBeNull("ActivityListener is registered for platform '{0}'", platform);
                _ = activities[i]!.GetTagItem("messaging.platform").Should().Be(platform);
                _ = activities[i]!.GetTagItem("messaging.operation").Should().Be(expectedOps[i]);
            }
        }
    }
}
