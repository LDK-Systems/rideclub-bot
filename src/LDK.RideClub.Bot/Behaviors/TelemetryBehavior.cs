using System.Diagnostics;

using MediatR;

namespace LDK.RideClub.Bot.Behaviors;

/// <summary>
/// Pipeline behavior that creates OpenTelemetry activity spans for each MediatR request,
/// recording outcome (success/exception) as span attributes.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class TelemetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ActivitySource _source = new("RideClub.Bot.Mediator");

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using Activity? activity = _source.StartActivity(typeof(TRequest).Name);
        try
        {
            TResponse response = await next(cancellationToken).ConfigureAwait(false);
            _ = activity?.SetTag("mediatr.outcome", "success");
            return response;
        }
        catch (Exception ex)
        {
            _ = activity?.SetTag("mediatr.outcome", "exception");
            _ = activity?.SetTag("mediatr.exception_type", ex.GetType().Name);
            throw;
        }
    }
}
