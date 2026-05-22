// ---------------------------------------------------------------------------
// RideClub Bot — DeploymentOptionsValidator (Req 5.2, 13.1)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="DeploymentOptions"/>.
/// Ensures the deployment mode is either "Kestrel" or "Lambda" (case-insensitive).
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class DeploymentOptionsValidator : AbstractValidator<DeploymentOptions>
#pragma warning restore CA1812
{
    private static readonly string[] _validModes = ["Kestrel", "Lambda"];

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentOptionsValidator"/> class.
    /// </summary>
    public DeploymentOptionsValidator()
    {
        _ = RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(mode => _validModes.Any(m => m.Equals(mode, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Mode must be 'Kestrel' or 'Lambda'.");
    }
}
