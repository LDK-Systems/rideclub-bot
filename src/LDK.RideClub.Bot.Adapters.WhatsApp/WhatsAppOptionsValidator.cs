// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppOptionsValidator (Req 5.2, 5.5)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// FluentValidation validator for <see cref="WhatsAppOptions"/>.
/// Ensures all credential fields are populated.
/// </summary>
public sealed class WhatsAppOptionsValidator : AbstractValidator<WhatsAppOptions>
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
