// ---------------------------------------------------------------------------
// RideClub Bot — Health Check Smoke Test (Req 12.5)
// ---------------------------------------------------------------------------

using System.Net;

using FluentAssertions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Integration smoke test that verifies the Bot_Host health-check endpoint
/// returns HTTP 200 OK when the application is running in-memory.
/// </summary>
public sealed class HealthCheckTests(BotWebApplicationFactory factory) : IClassFixture<BotWebApplicationFactory>
{
    [Fact]
    public async Task Should_Return_200_When_Healthy()
    {
        // Arrange
        using HttpClient client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative), cts.Token);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
