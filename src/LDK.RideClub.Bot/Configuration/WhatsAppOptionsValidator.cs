// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppOptionsValidator (Req 5.2, 5.5)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="WhatsAppOptions"/>.
/// Ensures all credential fields are populated.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class WhatsAppOptionsValidator : AbstractValidator<WhatsAppOptions>
#pragma warning restore CA1812
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppOptionsValidator"/> class.
    /// </summary>
    public WhatsAppOptionsValidator()
    {
        _ = RuleFor(x => x.VerifyToken).NotEmpty();
        _ = RuleFor(x => x.AccessToken).NotEmpty();
        _ = RuleFor(x => x.PhoneNumberId).NotEmpty();
    }
}
