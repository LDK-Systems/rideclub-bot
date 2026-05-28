using FluentValidation;
using FluentValidation.Results;

using MediatR;

namespace LDK.RideClub.Bot.Behaviors;

/// <summary>
/// Pipeline behavior that resolves all registered validators for a request type,
/// runs validation, and throws <see cref="ValidationException"/> on failure.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            ValidationResult[] results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
