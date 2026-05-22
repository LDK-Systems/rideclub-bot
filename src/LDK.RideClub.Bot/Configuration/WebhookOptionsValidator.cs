// ---------------------------------------------------------------------------
// RideClub Bot — WebhookOptionsValidator (Req 5.2)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="WebhookOptions"/>.
/// Ensures the base path is not empty and starts with "/".
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class WebhookOptionsValidator : AbstractValidator<WebhookOptions>
#pragma warning restore CA1812
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookOptionsValidator"/> class.
    /// </summary>
    public WebhookOptionsValidator()
    {
        _ = RuleFor(x => x.BasePath)
            .NotEmpty()
            .Must(path => path.StartsWith('/'))
            .WithMessage("BasePath must start with '/'.");
    }
}
