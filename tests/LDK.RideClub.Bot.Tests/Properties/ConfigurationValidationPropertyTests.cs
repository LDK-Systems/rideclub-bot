// ---------------------------------------------------------------------------
// Feature: rideclub-bot-platform, Property 5: Options Validation Rejects Invalid Configuration
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Adapters.WhatsApp;
using LDK.RideClub.Bot.Configuration;

using Microsoft.Extensions.Options;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Properties;

/// <summary>
/// Property tests verifying that each Options_Model validator correctly rejects
/// invalid values and produces error messages containing the class name and
/// failed property names.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.2, 5.3**
/// </remarks>
public sealed class ConfigurationValidationPropertyTests
{
    // -----------------------------------------------------------------------
    // WebhookOptions validation
    // -----------------------------------------------------------------------

    [Fact]
    public void WebhookOptions_EmptyBasePath_ValidationFails_ErrorContainsClassAndProperty()
    {
        // Arrange
        var validator = new WebhookOptionsValidator();
        var options = new WebhookOptions { BasePath = string.Empty };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "BasePath");
    }

    [Fact]
    public void WebhookOptions_BasePathNotStartingWithSlash_ValidationFails()
    {
        // Arrange
        var validator = new WebhookOptionsValidator();
        var options = new WebhookOptions { BasePath = "webhooks" };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "BasePath");
    }

    // -----------------------------------------------------------------------
    // WhatsAppOptions validation
    // -----------------------------------------------------------------------

    [Fact]
    public void WhatsAppOptions_EmptyVerifyToken_ValidationFails_ErrorContainsClassAndProperty()
    {
        // Arrange
        var validator = new WhatsAppOptionsValidator();
        var options = new WhatsAppOptions
        {
            VerifyToken = string.Empty,
            AccessToken = "valid-token",
            PhoneNumberId = "12345"
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "VerifyToken");
    }

    // -----------------------------------------------------------------------
    // PersistenceOptions validation
    // -----------------------------------------------------------------------

    [Fact]
    public void PersistenceOptions_EmptySqliteConnectionString_ValidationFails()
    {
        // Arrange
        var validator = new PersistenceOptionsValidator();
        var options = new PersistenceOptions { SqliteConnectionString = string.Empty };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "SqliteConnectionString");
    }

    // -----------------------------------------------------------------------
    // OtlpOptions validation
    // -----------------------------------------------------------------------

    [Fact]
    public void OtlpOptions_InvalidEndpoint_ValidationFails()
    {
        // Arrange
        var validator = new OtlpOptionsValidator();
        var options = new OtlpOptions { Endpoint = "not-a-valid-uri", ServiceName = "test" };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "Endpoint");
    }

    // -----------------------------------------------------------------------
    // DeploymentOptions validation
    // -----------------------------------------------------------------------

    [Fact]
    public void DeploymentOptions_InvalidMode_ValidationFails()
    {
        // Arrange
        var validator = new DeploymentOptionsValidator();
        var options = new DeploymentOptions { Mode = "InvalidMode" };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(options);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.PropertyName == "Mode");
    }

    // -----------------------------------------------------------------------
    // FluentValidationOptionsValidator<T> bridge tests
    // -----------------------------------------------------------------------

    [Fact]
    public void FluentValidationOptionsValidatorBridge_InvalidWebhookOptions_ReturnsFailed_WithFormattedMessage()
    {
        // Arrange
        var fluentValidator = new WebhookOptionsValidator();
        var bridge = new FluentValidationOptionsValidator<WebhookOptions>(fluentValidator);
        var options = new WebhookOptions { BasePath = string.Empty };

        // Act
        ValidateOptionsResult result = bridge.Validate(name: null, options);

        // Assert
        _ = result.Failed.Should().BeTrue();
        _ = result.Failures.Should().Contain(f => f.Contains("WebhookOptions") && f.Contains("BasePath"));
    }

    [Fact]
    public void FluentValidationOptionsValidatorBridge_InvalidWhatsAppOptions_ReturnsFailed_WithFormattedMessage()
    {
        // Arrange
        var fluentValidator = new WhatsAppOptionsValidator();
        var bridge = new FluentValidationOptionsValidator<WhatsAppOptions>(fluentValidator);
        var options = new WhatsAppOptions
        {
            VerifyToken = string.Empty,
            AccessToken = "valid",
            PhoneNumberId = "123"
        };

        // Act
        ValidateOptionsResult result = bridge.Validate(name: null, options);

        // Assert
        _ = result.Failed.Should().BeTrue();
        _ = result.Failures.Should().Contain(f => f.Contains("WhatsAppOptions") && f.Contains("VerifyToken"));
    }

    [Fact]
    public void FluentValidationOptionsValidatorBridge_InvalidDeploymentOptions_ReturnsFailed_WithFormattedMessage()
    {
        // Arrange
        var fluentValidator = new DeploymentOptionsValidator();
        var bridge = new FluentValidationOptionsValidator<DeploymentOptions>(fluentValidator);
        var options = new DeploymentOptions { Mode = "Docker" };

        // Act
        ValidateOptionsResult result = bridge.Validate(name: null, options);

        // Assert
        _ = result.Failed.Should().BeTrue();
        _ = result.Failures.Should().Contain(f => f.Contains("DeploymentOptions") && f.Contains("Mode"));
    }

    [Fact]
    public void FluentValidationOptionsValidatorBridge_ValidOptions_ReturnsSuccess()
    {
        // Arrange
        var fluentValidator = new WebhookOptionsValidator();
        var bridge = new FluentValidationOptionsValidator<WebhookOptions>(fluentValidator);
        var options = new WebhookOptions { BasePath = "/webhooks" };

        // Act
        ValidateOptionsResult result = bridge.Validate(name: null, options);

        // Assert
        _ = result.Succeeded.Should().BeTrue();
        _ = result.Failed.Should().BeFalse();
    }
}
