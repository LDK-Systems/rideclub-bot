// ---------------------------------------------------------------------------
// RideClub Bot — OtlpOptionsValidator (Req 5.2, 11.2)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="OtlpOptions"/>.
/// Ensures the endpoint is a valid URI and the service name is provided.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class OtlpOptionsValidator : AbstractValidator<OtlpOptions>
#pragma warning restore CA1812
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpOptionsValidator"/> class.
    /// </summary>
    public OtlpOptionsValidator()
    {
        _ = RuleFor(x => x.Endpoint)
            .NotEmpty()
            .Must(BeAValidUri)
            .WithMessage("Endpoint must be a valid URI.");

        _ = RuleFor(x => x.ServiceName).NotEmpty();
    }

    private static bool BeAValidUri(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out _);
    }
}
