// ---------------------------------------------------------------------------
// RideClub Bot — OptionsChangeMonitorService (Req 5.6, 5.7)
// ---------------------------------------------------------------------------

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// A hosted service that monitors IOptionsMonitor change callbacks for all
/// registered Options_Model classes. When a configuration change produces
/// invalid values, it logs a warning and retains the last valid configuration.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class OptionsChangeMonitorService(
    ILogger<OptionsChangeMonitorService> logger,
    IOptionsMonitor<WebhookOptions> webhookMonitor,
    IOptionsMonitor<WhatsAppOptions> whatsAppMonitor,
    IOptionsMonitor<PersistenceOptions> persistenceMonitor,
    IOptionsMonitor<OtlpOptions> otlpMonitor,
    IOptionsMonitor<DeploymentOptions> deploymentMonitor,
    IValidator<WebhookOptions> webhookValidator,
    IValidator<WhatsAppOptions> whatsAppValidator,
    IValidator<PersistenceOptions> persistenceValidator,
    IValidator<OtlpOptions> otlpValidator,
    IValidator<DeploymentOptions> deploymentValidator) : IHostedService, IDisposable
#pragma warning restore CA1812
{
    private readonly List<IDisposable> _changeListeners = [];

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        AddChangeListener(webhookMonitor, webhookValidator);
        AddChangeListener(whatsAppMonitor, whatsAppValidator);
        AddChangeListener(persistenceMonitor, persistenceValidator);
        AddChangeListener(otlpMonitor, otlpValidator);
        AddChangeListener(deploymentMonitor, deploymentValidator);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (IDisposable listener in _changeListeners)
        {
            listener.Dispose();
        }

        _changeListeners.Clear();
    }

    private void AddChangeListener<TOptions>(
        IOptionsMonitor<TOptions> monitor,
        IValidator<TOptions> validator)
    {
        IDisposable? listener = monitor.OnChange(options => ValidateOnChange(options, validator));

        if (listener is not null)
        {
            _changeListeners.Add(listener);
        }
    }

    private void ValidateOnChange<TOptions>(TOptions options, IValidator<TOptions> validator)
    {
        ValidationResult result = validator.Validate(options);

        if (!result.IsValid)
        {
            string optionsName = typeof(TOptions).Name;
            string failedProperties = string.Join(", ", result.Errors.Select(e => e.PropertyName));

            LogInvalidConfigurationChange(logger, optionsName, failedProperties);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Configuration change for {OptionsType} produced invalid values. " +
                  "Retaining last valid configuration. Failed properties: {FailedProperties}")]
    private static partial void LogInvalidConfigurationChange(
        ILogger logger,
        string optionsType,
        string failedProperties);
}
