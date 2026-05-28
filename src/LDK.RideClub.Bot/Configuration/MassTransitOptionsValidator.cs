// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitOptionsValidator (Req 5.2, 5.6)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="MassTransitOptions"/>.
/// Ensures the transport type is provided and is a known value.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class MassTransitOptionsValidator : AbstractValidator<MassTransitOptions>
#pragma warning restore CA1812
{
    private static readonly string[] _knownTransportTypes = ["InMemory", "RabbitMq"];

    /// <summary>
    /// Initializes a new instance of the <see cref="MassTransitOptionsValidator"/> class.
    /// </summary>
    public MassTransitOptionsValidator()
    {
        _ = RuleFor(x => x.TransportType)
            .NotEmpty()
            .Must(t => _knownTransportTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"TransportType must be one of: {string.Join(", ", _knownTransportTypes)}");

        _ = RuleFor(x => x.ConcurrencyLimit)
            .GreaterThan(0);
    }
}
