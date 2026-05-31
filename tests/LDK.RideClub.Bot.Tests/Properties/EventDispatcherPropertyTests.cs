// ---------------------------------------------------------------------------
// RideClub Bot — EventDispatcher Property-Based Tests
// Feature: mediatr-masstransit-pipeline, Property 1: Dispatch Exclusivity
// Feature: mediatr-masstransit-pipeline, Property 2: Command Field Mapping Preservation
// ---------------------------------------------------------------------------

using FluentAssertions;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Domain.Responses;
using LDK.RideClub.Bot.Services;

using MassTransit;

using MassTransit.Mediator;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Properties;

/// <summary>
/// FsCheck property-based tests verifying EventDispatcher dispatch exclusivity
/// and command field mapping preservation across all valid InboundEvent instances.
/// </summary>
/// <remarks>
/// <para><b>Validates: Requirements 10.5</b></para>
/// </remarks>
[Properties(MaxTest = 100, Arbitrary = [typeof(InboundEventArbitraries)])]
public sealed class EventDispatcherPropertyTests
{
    // -----------------------------------------------------------------------
    // Property 1: Dispatch Exclusivity
    // For any valid InboundEvent, EventDispatcher produces exactly one
    // Command send or one Notification publish through the Mediator — never
    // both, never neither.
    // -----------------------------------------------------------------------

    /// <summary>
    /// For all valid InboundEvent instances with a known payload type
    /// (TextMessagePayload or CommandPayload), the EventDispatcher sends
    /// exactly one command via IMediator.Send and does not publish any notification.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 2.3, 10.5</b></para>
    /// </remarks>
    [Property]
    public async Task Property1_KnownPayload_ProducesExactlyOneCommandSend(KnownPayloadInboundEvent wrapper)
    {
        // Arrange
        InboundEvent evt = wrapper.Event;
        IMediator mediator = Substitute.For<IScopedMediator>();
        IBus bus = Substitute.For<IBus>();
        NullLogger<EventDispatcher> logger = NullLogger<EventDispatcher>.Instance;
        EventDispatcher sut = new(mediator, bus, logger);

        mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new MessageProcessingResult { Success = true });

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — exactly one Send call
        await mediator.Received(1).Send(
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());

        // Assert — no Publish of UnrecognisedEventNotification
        await mediator.DidNotReceive().Publish(
            Arg.Any<UnrecognisedEventNotification>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// For all valid InboundEvent instances with an unknown payload type,
    /// the EventDispatcher publishes exactly one notification via IMediator.Publish
    /// and does not send any command.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 2.4, 10.5</b></para>
    /// </remarks>
    [Property]
    public async Task Property1_UnknownPayload_ProducesExactlyOneNotificationPublish(UnknownPayloadInboundEvent wrapper)
    {
        // Arrange
        InboundEvent evt = wrapper.Event;
        IMediator mediator = Substitute.For<IScopedMediator>();
        IBus bus = Substitute.For<IBus>();
        NullLogger<EventDispatcher> logger = NullLogger<EventDispatcher>.Instance;
        EventDispatcher sut = new(mediator, bus, logger);

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — exactly one Publish of UnrecognisedEventNotification
        await mediator.Received(1).Publish(
            Arg.Any<UnrecognisedEventNotification>(),
            Arg.Any<CancellationToken>());

        // Assert — no Send call
        await mediator.DidNotReceive().Send(
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Property 2: Command Field Mapping Preservation
    // For any valid InboundEvent with a known EventPayload subtype, the
    // MediatR command carries the same SenderId, ConversationId, Platform,
    // Timestamp, and payload-specific fields as the source InboundEvent.
    // -----------------------------------------------------------------------

    /// <summary>
    /// For all valid InboundEvent instances with a TextMessagePayload, the
    /// produced ProcessTextMessageCommand preserves SenderId, ConversationId,
    /// Platform, Timestamp, and MessageText from the source event.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 2.2, 4.1</b></para>
    /// </remarks>
    [Property]
    public async Task Property2_TextMessagePayload_PreservesAllFields(TextMessageInboundEvent wrapper)
    {
        // Arrange
        InboundEvent evt = wrapper.Event;
        IMediator mediator = Substitute.For<IScopedMediator>();
        IBus bus = Substitute.For<IBus>();
        NullLogger<EventDispatcher> logger = NullLogger<EventDispatcher>.Instance;
        EventDispatcher sut = new(mediator, bus, logger);

        ProcessTextMessageCommand? capturedCommand = null;
        mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new MessageProcessingResult { Success = true })
            .AndDoes(callInfo =>
            {
                if (callInfo.Arg<object>() is ProcessTextMessageCommand cmd)
                {
                    capturedCommand = cmd;
                }
            });

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — command was captured
        _ = capturedCommand.Should().NotBeNull("a ProcessTextMessageCommand should have been sent");

        // Assert — common fields preserved
        _ = capturedCommand!.SenderId.Should().Be(evt.SenderId);
        _ = capturedCommand.ConversationId.Should().Be(evt.ConversationId);
        _ = capturedCommand.Platform.Should().Be(evt.Platform);
        _ = capturedCommand.Timestamp.Should().Be(evt.Timestamp);

        // Assert — payload-specific field preserved
        TextMessagePayload textPayload = (TextMessagePayload)evt.Payload;
        _ = capturedCommand.MessageText.Should().Be(textPayload.Text);
    }

    /// <summary>
    /// For all valid InboundEvent instances with a CommandPayload, the
    /// produced ProcessBotCommandCommand preserves SenderId, ConversationId,
    /// Platform, Timestamp, CommandName, and Arguments from the source event.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 2.2, 4.2</b></para>
    /// </remarks>
    [Property]
    public async Task Property2_CommandPayload_PreservesAllFields(CommandPayloadInboundEvent wrapper)
    {
        // Arrange
        InboundEvent evt = wrapper.Event;
        IMediator mediator = Substitute.For<IScopedMediator>();
        IBus bus = Substitute.For<IBus>();
        NullLogger<EventDispatcher> logger = NullLogger<EventDispatcher>.Instance;
        EventDispatcher sut = new(mediator, bus, logger);

        ProcessBotCommandCommand? capturedCommand = null;
        mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new MessageProcessingResult { Success = true })
            .AndDoes(callInfo =>
            {
                if (callInfo.Arg<object>() is ProcessBotCommandCommand cmd)
                {
                    capturedCommand = cmd;
                }
            });

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — command was captured
        _ = capturedCommand.Should().NotBeNull("a ProcessBotCommandCommand should have been sent");

        // Assert — common fields preserved
        _ = capturedCommand!.SenderId.Should().Be(evt.SenderId);
        _ = capturedCommand.ConversationId.Should().Be(evt.ConversationId);
        _ = capturedCommand.Platform.Should().Be(evt.Platform);
        _ = capturedCommand.Timestamp.Should().Be(evt.Timestamp);

        // Assert — payload-specific fields preserved
        CommandPayload cmdPayload = (CommandPayload)evt.Payload;
        _ = capturedCommand.CommandName.Should().Be(cmdPayload.CommandName);
        _ = capturedCommand.Arguments.Should().BeEquivalentTo(cmdPayload.Arguments);
    }
}

// ---------------------------------------------------------------------------
// Wrapper types for FsCheck to generate specific InboundEvent variants.
// These must be public for FsCheck/xUnit reflection-based discovery.
// ---------------------------------------------------------------------------

/// <summary>Wrapper for InboundEvent with a known payload (TextMessage or Command).</summary>
#pragma warning disable CA1515 // Public types must be internal — required by FsCheck reflection
public sealed record KnownPayloadInboundEvent(InboundEvent Event)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"KnownPayload({Event.Payload.GetType().Name}, EventId={Event.EventId})";
    }
}

/// <summary>Wrapper for InboundEvent with an unknown/unrecognised payload.</summary>
public sealed record UnknownPayloadInboundEvent(InboundEvent Event)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"UnknownPayload({Event.Payload.GetType().Name}, EventId={Event.EventId})";
    }
}

/// <summary>Wrapper for InboundEvent specifically with TextMessagePayload.</summary>
public sealed record TextMessageInboundEvent(InboundEvent Event)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"TextMessage(Text={((TextMessagePayload)Event.Payload).Text}, EventId={Event.EventId})";
    }
}

/// <summary>Wrapper for InboundEvent specifically with CommandPayload.</summary>
public sealed record CommandPayloadInboundEvent(InboundEvent Event)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"CommandPayload(Cmd={((CommandPayload)Event.Payload).CommandName}, EventId={Event.EventId})";
    }
}

/// <summary>An unknown payload type for testing the unrecognised event path.</summary>
public sealed record UnknownTestPayload : EventPayload;
#pragma warning restore CA1515

// ---------------------------------------------------------------------------
// Custom Arbitrary generators for InboundEvent variants
// ---------------------------------------------------------------------------

/// <summary>
/// Provides FsCheck Arbitrary instances for generating valid InboundEvent
/// instances with various payload types.
/// </summary>
#pragma warning disable CA1515 // Public types must be internal — required by FsCheck Arbitrary attribute
public static class InboundEventArbitraries
#pragma warning restore CA1515
{
    // Non-empty, non-null string generator (alphanumeric, reasonable length)
    private static Gen<string> NonEmptyStringGen()
    {
        return Gen.Elements(
            "user-1", "user-2", "user-abc", "sender-42", "447700900000",
            "conv-1", "conv-99", "conv-xyz", "chat-123",
            "whatsapp", "telegram", "discord", "signal", "slack",
            "evt-001", "evt-002", "evt-abc", "msg-xyz",
            "Hello", "Hi there", "Test message", "/start", "/help",
            "arg1", "arg2", "value1", "value2", "key1", "key2",
            "platform-x", "id-123", "some-text", "another-value");
    }

    private static Gen<string> PlatformGen()
    {
        return Gen.Elements("whatsapp", "telegram", "discord", "signal", "slack", "sms");
    }

    private static Gen<string> MessageTextGen()
    {
        return Gen.Elements(
            "Hello", "Hi there!", "How are you?", "Test message",
            "A longer message with some content", "Short",
            "Special chars: @#$%^&*()", "Unicode: 你好世界",
            "Numbers 12345", "Mixed content 123 abc !@#");
    }

    private static Gen<string> CommandNameGen()
    {
        return Gen.Elements(
            "/start", "/help", "/stop", "/status", "/ride",
            "/join", "/leave", "/info", "/settings", "/cancel");
    }

    private static Gen<IReadOnlyDictionary<string, string>> ArgumentsGen()
    {
        Gen<IReadOnlyDictionary<string, string>> emptyDict = Gen.Constant<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>());

        Gen<IReadOnlyDictionary<string, string>> singleEntry = Gen.Elements("key1", "key2", "arg", "param", "option")
            .SelectMany(key => Gen.Elements("value1", "value2", "true", "42", "hello")
                .Select(value => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { [key] = value }));

        Gen<IReadOnlyDictionary<string, string>> twoEntries = Gen.Constant<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" });

        return Gen.OneOf(emptyDict, singleEntry, twoEntries);
    }

    private static Gen<DateTimeOffset> TimestampGen()
    {
        return Gen.Choose(1_600_000_000, 1_800_000_000)
            .Select(unixSeconds => DateTimeOffset.FromUnixTimeSeconds(unixSeconds));
    }

    private static Gen<InboundEvent> TextMessageEventGen()
    {
        return from eventId in NonEmptyStringGen()
               from platform in PlatformGen()
               from senderId in NonEmptyStringGen()
               from conversationId in NonEmptyStringGen()
               from timestamp in TimestampGen()
               from text in MessageTextGen()
               select new InboundEvent
               {
                   EventId = eventId,
                   Platform = platform,
                   SenderId = senderId,
                   ConversationId = conversationId,
                   Timestamp = timestamp,
                   Payload = new TextMessagePayload { Text = text },
               };
    }

    private static Gen<InboundEvent> CommandEventGen()
    {
        return from eventId in NonEmptyStringGen()
               from platform in PlatformGen()
               from senderId in NonEmptyStringGen()
               from conversationId in NonEmptyStringGen()
               from timestamp in TimestampGen()
               from commandName in CommandNameGen()
               from arguments in ArgumentsGen()
               select new InboundEvent
               {
                   EventId = eventId,
                   Platform = platform,
                   SenderId = senderId,
                   ConversationId = conversationId,
                   Timestamp = timestamp,
                   Payload = new CommandPayload
                   {
                       CommandName = commandName,
                       Arguments = arguments,
                   },
               };
    }

    private static Gen<InboundEvent> UnknownPayloadEventGen()
    {
        return from eventId in NonEmptyStringGen()
               from platform in PlatformGen()
               from senderId in NonEmptyStringGen()
               from conversationId in NonEmptyStringGen()
               from timestamp in TimestampGen()
               select new InboundEvent
               {
                   EventId = eventId,
                   Platform = platform,
                   SenderId = senderId,
                   ConversationId = conversationId,
                   Timestamp = timestamp,
                   Payload = new UnknownTestPayload(),
               };
    }

    /// <summary>Generates InboundEvent with known payload types (TextMessage or Command).</summary>
    public static Arbitrary<KnownPayloadInboundEvent> KnownPayloadInboundEventArbitrary()
    {
        Gen<KnownPayloadInboundEvent> gen = Gen.OneOf(TextMessageEventGen(), CommandEventGen())
            .Select(evt => new KnownPayloadInboundEvent(evt));
        return Arb.From(gen);
    }

    /// <summary>Generates InboundEvent with unknown/unrecognised payload types.</summary>
    public static Arbitrary<UnknownPayloadInboundEvent> UnknownPayloadInboundEventArbitrary()
    {
        Gen<UnknownPayloadInboundEvent> gen = UnknownPayloadEventGen()
            .Select(evt => new UnknownPayloadInboundEvent(evt));
        return Arb.From(gen);
    }

    /// <summary>Generates InboundEvent specifically with TextMessagePayload.</summary>
    public static Arbitrary<TextMessageInboundEvent> TextMessageInboundEventArbitrary()
    {
        Gen<TextMessageInboundEvent> gen = TextMessageEventGen()
            .Select(evt => new TextMessageInboundEvent(evt));
        return Arb.From(gen);
    }

    /// <summary>Generates InboundEvent specifically with CommandPayload.</summary>
    public static Arbitrary<CommandPayloadInboundEvent> CommandPayloadInboundEventArbitrary()
    {
        Gen<CommandPayloadInboundEvent> gen = CommandEventGen()
            .Select(evt => new CommandPayloadInboundEvent(evt));
        return Arb.From(gen);
    }
}
