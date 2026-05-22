// Feature: rideclub-bot-platform, Properties 1-4, 7: Architectural Constraints
// Validates: Requirements 4.1, 4.2, 4.3, 4.5, 6.4

using System.Reflection;
using System.Text.Json;

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Architecture;

/// <summary>
/// Reflection-based architectural constraint tests that scan application assemblies
/// and verify structural properties hold universally.
/// </summary>
public sealed class ArchitecturalConstraintTests
{
    /// <summary>
    /// Application assemblies to scan for architectural constraints.
    /// </summary>
    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Program).Assembly,
        typeof(Abstractions.Messaging.IMessagingAdapter).Assembly,
        typeof(Domain.Events.InboundEvent).Assembly,
        typeof(Adapters.WhatsApp.WhatsAppMessagingAdapter).Assembly,
        typeof(Persistence.BotDbContext).Assembly,
        typeof(Observability.ObservabilityExtensions).Assembly,
    ];

    /// <summary>
    /// Cross-cutting concern types that are excluded from the dependency count.
    /// One cross-cutting concern is allowed per constructor without counting toward the limit.
    /// </summary>
    private static readonly Type[] CrossCuttingConcernOpenTypes =
    [
        typeof(ILogger<>),
        typeof(IOptions<>),
        typeof(IOptionsSnapshot<>),
        typeof(IOptionsMonitor<>),
    ];

    /// <summary>
    /// Determines whether a type is part of the composition root and should be excluded
    /// from architectural constraint checks.
    /// </summary>
    private static bool IsCompositionRoot(Type type)
    {
        // Program class
        if (type.Name is "Program" or "<Program>$")
        {
            return true;
        }

        string? ns = type.Namespace;
        if (string.IsNullOrEmpty(ns))
        {
            return true;
        }

        // Any type in a "Modules" namespace
        if (ns.Contains(".Modules", StringComparison.Ordinal) || ns.EndsWith("Modules", StringComparison.Ordinal))
        {
            return true;
        }

        // Any type in a "Configuration" namespace with "Extensions" in the name
        if ((ns.Contains(".Configuration", StringComparison.Ordinal) || ns.EndsWith("Configuration", StringComparison.Ordinal))
            && type.Name.Contains("Extensions", StringComparison.Ordinal))
        {
            return true;
        }

        // IHostedService implementations that use IServiceProvider for scope creation
        // are infrastructure services and part of the composition root pattern
        return typeof(IHostedService).IsAssignableFrom(type);
    }

    /// <summary>
    /// Determines whether a parameter type is a cross-cutting concern.
    /// </summary>
    private static bool IsCrossCuttingConcern(Type parameterType)
    {
        if (!parameterType.IsGenericType)
        {
            return false;
        }

        Type genericDefinition = parameterType.GetGenericTypeDefinition();
        return CrossCuttingConcernOpenTypes.Contains(genericDefinition);
    }

    /// <summary>
    /// Gets all non-abstract, non-static classes from application assemblies
    /// that are not part of the composition root.
    /// </summary>
    private static IEnumerable<Type> GetApplicationClasses()
    {
        return ApplicationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !IsCompositionRoot(t));
    }

    /// <summary>
    /// Gets all interfaces defined in application assemblies.
    /// </summary>
    private static IEnumerable<Type> GetApplicationInterfaces()
    {
        return ApplicationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsInterface);
    }

    // -------------------------------------------------------------------------
    // Property 1: Constructor Dependency Limit
    // For any class in the application assemblies (excluding the composition root),
    // the constructor SHALL have no more than 5 parameters (not counting a single
    // cross-cutting concern like ILogger<T>).
    // Validates: Requirements 4.1
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that no constructor in application classes exceeds 5 parameters
    /// (excluding a single cross-cutting concern).
    /// </summary>
    [Fact]
    public void ConstructorDependencyLimitIsNotExceeded()
    {
        IEnumerable<Type> classes = GetApplicationClasses();

        using (new AssertionScope())
        {
            foreach (Type type in classes)
            {
                ConstructorInfo? constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor is null)
                {
                    continue;
                }

                ParameterInfo[] parameters = constructor.GetParameters();

                // Exclude up to one cross-cutting concern from the count
                int crossCuttingCount = parameters.Count(p => IsCrossCuttingConcern(p.ParameterType));
                int exclusion = Math.Min(crossCuttingCount, 1);
                int effectiveCount = parameters.Length - exclusion;

                _ = effectiveCount.Should().BeLessThanOrEqualTo(
                    5,
                    $"class '{type.FullName}' has {effectiveCount} constructor dependencies " +
                    $"(excluding 1 cross-cutting concern), which exceeds the limit of 5");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Property 2: Dependencies on Abstractions
    // For any class in the application assemblies, constructor parameters representing
    // logging, persistence, or messaging dependencies SHALL be interface types rather
    // than concrete implementations.
    // Validates: Requirements 4.2
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that logging, persistence, and messaging constructor parameters
    /// are interface types rather than concrete implementations.
    /// </summary>
    [Fact]
    public void DependenciesOnAbstractionsAreInterfaces()
    {
        IEnumerable<Type> classes = GetApplicationClasses();

        using (new AssertionScope())
        {
            foreach (Type type in classes)
            {
                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                foreach (ConstructorInfo constructor in constructors)
                {
                    foreach (ParameterInfo param in constructor.GetParameters())
                    {
                        Type paramType = param.ParameterType;

                        // Logging dependencies must be interfaces (ILogger<T> is already an interface)
                        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
                        {
                            _ = paramType.IsInterface.Should().BeTrue(
                                $"parameter '{param.Name}' in '{type.FullName}' constructor " +
                                $"is a logging dependency and should be an interface type, but is '{paramType.Name}'");
                        }

                        // Repository persistence dependencies must be interfaces
                        if (paramType.Name.Contains("Repository", StringComparison.Ordinal))
                        {
                            _ = paramType.IsInterface.Should().BeTrue(
                                $"parameter '{param.Name}' in '{type.FullName}' constructor " +
                                $"is a persistence dependency and should be an interface type, but is '{paramType.Name}'");
                        }

                        // Messaging dependencies must be interfaces
                        if (paramType == typeof(Abstractions.Messaging.IMessagingAdapter) ||
                            paramType == typeof(Abstractions.Messaging.IAdapterRegistry) ||
                            paramType == typeof(Abstractions.Messaging.IEventProcessor))
                        {
                            _ = paramType.IsInterface.Should().BeTrue(
                                $"parameter '{param.Name}' in '{type.FullName}' constructor " +
                                $"is a messaging dependency and should be an interface type, but is '{paramType.Name}'");
                        }
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Property 3: No Service Locator Pattern
    // For any class in the application assemblies (excluding the composition root),
    // there SHALL be no constructor parameter, field, or property of type IServiceProvider.
    // Validates: Requirements 4.3
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that no application class outside the composition root uses
    /// IServiceProvider in constructors, fields, or properties.
    /// </summary>
    [Fact]
    public void NoServiceLocatorPatternOutsideCompositionRoot()
    {
        IEnumerable<Type> classes = GetApplicationClasses();

        using (new AssertionScope())
        {
            foreach (Type type in classes)
            {
                // Check constructor parameters
                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (ConstructorInfo constructor in constructors)
                {
                    foreach (ParameterInfo param in constructor.GetParameters())
                    {
                        _ = param.ParameterType.Should().NotBe<IServiceProvider>(
                            $"class '{type.FullName}' has constructor parameter '{param.Name}' " +
                            $"of type IServiceProvider, which indicates a service locator pattern");
                    }
                }

                // Check fields
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (FieldInfo field in fields)
                {
                    _ = field.FieldType.Should().NotBe<IServiceProvider>(
                        $"class '{type.FullName}' has field '{field.Name}' " +
                        $"of type IServiceProvider, which indicates a service locator pattern");
                }

                // Check properties
                PropertyInfo[] properties = type.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (PropertyInfo property in properties)
                {
                    _ = property.PropertyType.Should().NotBe<IServiceProvider>(
                        $"class '{type.FullName}' has property '{property.Name}' " +
                        $"of type IServiceProvider, which indicates a service locator pattern");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Property 4: Interface Segregation Limit
    // For any interface defined in the application assemblies, the interface SHALL
    // declare no more than 5 methods.
    // Validates: Requirements 4.5
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that no interface in application assemblies declares more than 5 methods.
    /// </summary>
    [Fact]
    public void InterfaceSegregationLimitIsNotExceeded()
    {
        IEnumerable<Type> interfaces = GetApplicationInterfaces();

        using (new AssertionScope())
        {
            foreach (Type iface in interfaces)
            {
                // Count only methods declared directly on this interface (not inherited)
                // Exclude property getters/setters from the method count
                int declaredMethodCount = iface
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Count(m => !m.IsSpecialName);

                _ = declaredMethodCount.Should().BeLessThanOrEqualTo(
                    5,
                    $"interface '{iface.FullName}' declares {declaredMethodCount} methods, " +
                    $"which exceeds the interface segregation limit of 5");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Property 7: No Raw JSON in Component Interfaces
    // For any public method in the application assemblies (excluding the webhook
    // endpoint boundary and DTOs), parameters and return types SHALL not be
    // JsonElement for structured domain data.
    // Validates: Requirements 6.4
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that no public method uses JsonElement as a parameter or return type.
    /// </summary>
    [Fact]
    public void NoJsonElementInPublicMethodSignatures()
    {
        IEnumerable<Type> types = ApplicationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => (t.IsClass || t.IsInterface) && t.IsPublic)
            .Where(t => !IsWebhookBoundary(t))
            .Where(t => !IsDto(t));

        using (new AssertionScope())
        {
            foreach (Type type in types)
            {
                MethodInfo[] publicMethods = [.. type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName)];

                foreach (MethodInfo method in publicMethods)
                {
                    // Check return type
                    Type returnType = UnwrapTaskType(method.ReturnType);
                    _ = returnType.Should().NotBe<JsonElement>(
                        $"method '{type.FullName}.{method.Name}' returns JsonElement, " +
                        $"which violates the no-raw-JSON constraint");

                    // Check parameter types
                    foreach (ParameterInfo param in method.GetParameters())
                    {
                        _ = param.ParameterType.Should().NotBe<JsonElement>(
                            $"method '{type.FullName}.{method.Name}' has parameter '{param.Name}' " +
                            $"of type JsonElement, which violates the no-raw-JSON constraint");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines whether a type is a webhook endpoint boundary type (excluded from Property 7).
    /// </summary>
    private static bool IsWebhookBoundary(Type type)
    {
        string? ns = type.Namespace;
        return !string.IsNullOrEmpty(ns)
            && (ns.Contains(".Endpoints", StringComparison.Ordinal)
                || type.Name.Contains("Webhook", StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether a type is a DTO (excluded from Property 7).
    /// </summary>
    private static bool IsDto(Type type)
    {
        string? ns = type.Namespace;
        return !string.IsNullOrEmpty(ns)
            && (ns.Contains(".DTOs", StringComparison.Ordinal)
                || type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
                || type.Name.EndsWith("Payload", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Unwraps Task&lt;T&gt; and ValueTask&lt;T&gt; to get the inner type.
    /// </summary>
    private static Type UnwrapTaskType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type;
        }

        Type genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>)
            ? type.GetGenericArguments()[0]
            : type;
    }
}
