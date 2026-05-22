// ---------------------------------------------------------------------------
// RideClub Bot — MappingPropertyTests (Properties 6, 8)
// Feature: rideclub-bot-platform, Property 6: DTO-to-Domain Mapping Purity
// Feature: rideclub-bot-platform, Property 8: Invalid DTO Rejection
// ---------------------------------------------------------------------------

using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

using FluentAssertions;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Adapters.WhatsApp;
using LDK.RideClub.Bot.Domain.Events;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Properties;

/// <summary>
/// Property-based tests verifying DTO-to-Domain mapping correctness.
/// </summary>
/// <remarks>
/// <para><b>Validates: Requirements 6.3, 6.5</b></para>
/// </remarks>
public sealed class MappingPropertyTests
{
    private const string _dtosNamespace = "LDK.RideClub.Bot.Adapters.WhatsApp.DTOs";

    private static WhatsAppMessagingAdapter CreateAdapter()
    {
        var options = new WhatsAppAdapterOptions(
            VerifyToken: "test-token",
            AccessToken: "test-access",
            PhoneNumberId: "123456");

        return new WhatsAppMessagingAdapter(options);
    }

    private static HttpRequest CreateHttpRequestWithBody(string body)
    {
        var context = new DefaultHttpContext();
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static string CreateValidWhatsAppPayload(
        string messageId = "wamid.test123",
        string from = "447700900000",
        string timestamp = "1700000000",
        string messageType = "text",
        string textBody = "Hello, world!")
    {
        return $$"""
            {
                "object": "whatsapp_business_account",
                "entry": [
                    {
                        "id": "ENTRY_ID",
                        "changes": [
                            {
                                "value": {
                                    "messaging_product": "whatsapp",
                                    "messages": [
                                        {
                                            "from": "{{from}}",
                                            "id": "{{messageId}}",
                                            "timestamp": "{{timestamp}}",
                                            "type": "{{messageType}}",
                                            "text": {
                                                "body": "{{textBody}}"
                                            }
                                        }
                                    ]
                                },
                                "field": "messages"
                            }
                        ]
                    }
                ]
            }
            """;
    }

    // -----------------------------------------------------------------------
    // Property 6: DTO-to-Domain Mapping Purity
    // -----------------------------------------------------------------------

    /// <summary>
    /// After mapping a valid WhatsApp payload to InboundEvent, the InboundEvent type
    /// hierarchy contains no references to platform-specific types, serialization
    /// attributes (JsonPropertyName), or WhatsApp-specific DTO types.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.3</b></para>
    /// </remarks>
    [Fact]
    public async Task Property6_MappedDomainModel_ContainsNoPlatformSpecificTypes()
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        string payload = CreateValidWhatsAppPayload();
        HttpRequest request = CreateHttpRequestWithBody(payload);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert — mapping succeeded
        _ = result.Should().BeOfType<MappingSuccess<InboundEvent>>();
        var success = (MappingSuccess<InboundEvent>)result;
        InboundEvent inboundEvent = success.Value;

        // Verify no properties on InboundEvent or its payload have types from the DTOs namespace
        AssertNoDtoNamespaceTypes(inboundEvent.GetType());
        AssertNoDtoNamespaceTypes(inboundEvent.Payload.GetType());

        // Verify no JsonPropertyName attributes on InboundEvent or its payload properties
        AssertNoJsonPropertyNameAttributes(inboundEvent.GetType());
        AssertNoJsonPropertyNameAttributes(inboundEvent.Payload.GetType());
    }

    /// <summary>
    /// Verifies mapping purity across multiple valid payloads with varying content.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.3</b></para>
    /// </remarks>
    [Theory]
    [InlineData("wamid.abc", "441234567890", "1700000001", "text", "Short")]
    [InlineData("wamid.xyz", "12025551234", "1700099999", "text", "A longer message with special chars: @#$%")]
    [InlineData("wamid.empty", "491234567890", "1700050000", "text", "")]
    public async Task Property6_MappedDomainModel_ContainsNoPlatformSpecificTypes_AcrossPayloads(
        string messageId,
        string from,
        string timestamp,
        string messageType,
        string textBody)
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        string payload = CreateValidWhatsAppPayload(messageId, from, timestamp, messageType, textBody);
        HttpRequest request = CreateHttpRequestWithBody(payload);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingSuccess<InboundEvent>>();
        var success = (MappingSuccess<InboundEvent>)result;
        InboundEvent inboundEvent = success.Value;

        AssertNoDtoNamespaceTypes(inboundEvent.GetType());
        AssertNoDtoNamespaceTypes(inboundEvent.Payload.GetType());
        AssertNoJsonPropertyNameAttributes(inboundEvent.GetType());
        AssertNoJsonPropertyNameAttributes(inboundEvent.Payload.GetType());
    }

    // -----------------------------------------------------------------------
    // Property 8: Invalid DTO Rejection
    // -----------------------------------------------------------------------

    /// <summary>
    /// An empty body produces a MappingFailure with a non-empty reason.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.5</b></para>
    /// </remarks>
    [Fact]
    public async Task Property8_EmptyBody_ReturnsMappingFailureWithReason()
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        HttpRequest request = CreateHttpRequestWithBody(string.Empty);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingFailure<InboundEvent>>();
        var failure = (MappingFailure<InboundEvent>)result;
        _ = failure.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Valid JSON but with no messages array produces a MappingFailure with a descriptive reason.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.5</b></para>
    /// </remarks>
    [Fact]
    public async Task Property8_ValidJsonNoMessages_ReturnsMappingFailureWithReason()
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        string payload = """
            {
                "object": "whatsapp_business_account",
                "entry": [
                    {
                        "id": "ENTRY_ID",
                        "changes": [
                            {
                                "value": {
                                    "messaging_product": "whatsapp"
                                },
                                "field": "messages"
                            }
                        ]
                    }
                ]
            }
            """;
        HttpRequest request = CreateHttpRequestWithBody(payload);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingFailure<InboundEvent>>();
        var failure = (MappingFailure<InboundEvent>)result;
        _ = failure.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Malformed JSON produces a MappingFailure with a descriptive reason.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.5</b></para>
    /// </remarks>
    [Fact]
    public async Task Property8_MalformedJson_ReturnsMappingFailureWithReason()
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        HttpRequest request = CreateHttpRequestWithBody("{not valid json at all");

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingFailure<InboundEvent>>();
        var failure = (MappingFailure<InboundEvent>)result;
        _ = failure.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Missing required fields (empty entry array) produces a MappingFailure.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.5</b></para>
    /// </remarks>
    [Fact]
    public async Task Property8_MissingRequiredFields_ReturnsMappingFailureWithReason()
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        string payload = """
            {
                "object": "whatsapp_business_account",
                "entry": []
            }
            """;
        HttpRequest request = CreateHttpRequestWithBody(payload);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingFailure<InboundEvent>>();
        var failure = (MappingFailure<InboundEvent>)result;
        _ = failure.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Various invalid payloads all produce MappingFailure with non-empty reasons.
    /// </summary>
    /// <remarks>
    /// <para><b>Validates: Requirements 6.5</b></para>
    /// </remarks>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"just a string\"")]
    [InlineData("12345")]
    public async Task Property8_VariousInvalidPayloads_ReturnMappingFailureWithReason(string body)
    {
        // Arrange
        WhatsAppMessagingAdapter adapter = CreateAdapter();
        HttpRequest request = CreateHttpRequestWithBody(body);

        // Act
        MappingResult<InboundEvent> result = await adapter.DeserialiseEventAsync(request);

        // Assert
        _ = result.Should().BeOfType<MappingFailure<InboundEvent>>();
        var failure = (MappingFailure<InboundEvent>)result;
        _ = failure.Reason.Should().NotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertNoDtoNamespaceTypes(Type type)
    {
        // Check the type itself
        _ = type.Namespace.Should().NotBe(_dtosNamespace,
            because: $"domain model type {type.Name} should not be from the DTOs namespace");

        // Check all public instance properties
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            Type propertyType = property.PropertyType;

            // Unwrap generic types (e.g., IReadOnlyDictionary<string, string>)
            if (propertyType.IsGenericType)
            {
                foreach (Type genericArg in propertyType.GetGenericArguments())
                {
                    _ = genericArg.Namespace.Should().NotBe(_dtosNamespace,
                        because: $"property {type.Name}.{property.Name} generic argument {genericArg.Name} should not be from the DTOs namespace");
                }
            }

            _ = propertyType.Namespace.Should().NotBe(_dtosNamespace,
                because: $"property {type.Name}.{property.Name} of type {propertyType.Name} should not be from the DTOs namespace");
        }
    }

    private static void AssertNoJsonPropertyNameAttributes(Type type)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            bool hasJsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>() is not null;
            _ = hasJsonPropertyName.Should().BeFalse(
                because: $"domain model property {type.Name}.{property.Name} should not have JsonPropertyName attribute");
        }
    }
}
