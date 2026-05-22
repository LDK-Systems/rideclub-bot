// ---------------------------------------------------------------------------
// RideClub Bot — AdapterRegistry Unit Tests (Req 8.3, 8.5)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Services;

using NSubstitute;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AdapterRegistry"/> verifying duplicate detection,
/// case-insensitive lookup, and platform enumeration.
/// </summary>
public sealed class AdapterRegistryTests
{
    [Fact]
    public void Duplicate_PlatformId_Throws_InvalidOperationException()
    {
        // Arrange
        IMessagingAdapter adapter1 = Substitute.For<IMessagingAdapter>();
        adapter1.PlatformId.Returns("whatsapp");

        IMessagingAdapter adapter2 = Substitute.For<IMessagingAdapter>();
        adapter2.PlatformId.Returns("whatsapp");

        // Act
        Action act = () => _ = new AdapterRegistry([adapter1, adapter2]);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate*whatsapp*");
    }

    [Fact]
    public void GetAdapter_Returns_Correct_Adapter_CaseInsensitive()
    {
        // Arrange
        IMessagingAdapter adapter = Substitute.For<IMessagingAdapter>();
        adapter.PlatformId.Returns("whatsapp");

        var registry = new AdapterRegistry([adapter]);

        // Act
        IMessagingAdapter? result = registry.GetAdapter("WhatsApp");

        // Assert
        result.Should().BeSameAs(adapter);
    }

    [Fact]
    public void GetAdapter_Returns_Null_For_Unknown_Platform()
    {
        // Arrange
        IMessagingAdapter adapter = Substitute.For<IMessagingAdapter>();
        adapter.PlatformId.Returns("whatsapp");

        var registry = new AdapterRegistry([adapter]);

        // Act
        IMessagingAdapter? result = registry.GetAdapter("telegram");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RegisteredPlatforms_Returns_All_Registered_Ids()
    {
        // Arrange
        IMessagingAdapter whatsApp = Substitute.For<IMessagingAdapter>();
        whatsApp.PlatformId.Returns("whatsapp");

        IMessagingAdapter telegram = Substitute.For<IMessagingAdapter>();
        telegram.PlatformId.Returns("telegram");

        var registry = new AdapterRegistry([whatsApp, telegram]);

        // Act
        IEnumerable<string> platforms = registry.RegisteredPlatforms;

        // Assert
        platforms.Should().BeEquivalentTo(["whatsapp", "telegram"]);
    }
}
