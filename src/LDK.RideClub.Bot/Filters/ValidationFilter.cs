using FluentValidation;
using FluentValidation.Results;

using MassTransit;

namespace LDK.RideClub.Bot.Filters;

/// <summary>
/// MassTransit send filter that resolves all registered <see cref="IValidator{T}"/> instances
/// for the message type, runs validation, and throws <see cref="ValidationException"/> on failure.
/// Short-circuits the pipeline before reaching the consumer if validation fails.
/// </summary>
/// <typeparam name="T">The message type being sent through the mediator pipeline.</typeparam>
/// <param name="validators">The collection of validators for the message type.</param>
internal sealed class ValidationFilter<T>(
    IEnumerable<IValidator<T>> validators)
    : IFilter<SendContext<T>>
    where T : class
{
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        if (validators.Any())
        {
            ValidationContext<T> validationContext = new(context.Message);

            FluentValidation.Results.ValidationResult[] results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(validationContext))).ConfigureAwait(false);

            List<ValidationFailure> failures = [.. results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)];

            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }
        }

        await next.Send(context).ConfigureAwait(false);
    }

    public void Probe(ProbeContext context)
    {
        _ = context.CreateFilterScope("validation");
    }
}
