using System.Diagnostics;

using MassTransit;

namespace LDK.RideClub.Bot.Filters;

/// <summary>
/// MassTransit send filter that creates an OpenTelemetry activity span for each mediator
/// command, recording outcome (success/exception) as span attributes.
/// </summary>
/// <typeparam name="T">The message type being sent through the mediator pipeline.</typeparam>
internal sealed class TelemetryFilter<T> : IFilter<SendContext<T>>
    where T : class
{
    private static readonly ActivitySource _source = new("RideClub.Bot.Mediator");

    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        using Activity? activity = _source.StartActivity(typeof(T).Name);
        try
        {
            await next.Send(context).ConfigureAwait(false);
            _ = activity?.SetTag("mediator.outcome", "success");
        }
        catch (Exception ex)
        {
            _ = activity?.SetTag("mediator.outcome", "exception");
            _ = activity?.SetTag("mediator.exception_type", ex.GetType().Name);
            throw;
        }
    }

    public void Probe(ProbeContext context)
    {
        _ = context.CreateFilterScope("telemetry");
    }
}
