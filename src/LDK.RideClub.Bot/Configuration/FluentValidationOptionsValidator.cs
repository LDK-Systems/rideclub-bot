// ---------------------------------------------------------------------------
// RideClub Bot — FluentValidationOptionsValidator (Req 5.2, 5.3)
// ---------------------------------------------------------------------------

using FluentValidation;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Bridges FluentValidation validators with the .NET Options validation system.
/// Implements <see cref="IValidateOptions{TOptions}"/> by delegating to a
/// <see cref="IValidator{T}"/> instance.
/// </summary>
/// <typeparam name="TOptions">The options type to validate.</typeparam>
/// <param name="validator">The FluentValidation validator for the options type.</param>
internal sealed class FluentValidationOptionsValidator<TOptions>(IValidator<TOptions> validator)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        IEnumerable<string> failures = result.Errors
            .Select(e => $"{typeof(TOptions).Name}.{e.PropertyName}: {e.ErrorMessage}");

        return ValidateOptionsResult.Fail(failures);
    }
}
