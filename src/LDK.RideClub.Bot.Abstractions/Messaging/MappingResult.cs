namespace LDK.RideClub.Bot.Abstractions.Messaging;

/// <summary>
/// Represents the result of mapping a platform DTO to a domain model.
/// Uses a discriminated union pattern for explicit error handling without exceptions.
/// </summary>
/// <typeparam name="T">The type of the successfully mapped value.</typeparam>
public abstract record MappingResult<T>;

/// <summary>
/// Represents a successful mapping result containing the mapped value.
/// </summary>
/// <typeparam name="T">The type of the successfully mapped value.</typeparam>
/// <param name="Value">The successfully mapped value.</param>
public sealed record MappingSuccess<T>(T Value) : MappingResult<T>;

/// <summary>
/// Represents a failed mapping result containing the reason for failure.
/// </summary>
/// <typeparam name="T">The type that was expected from the mapping.</typeparam>
/// <param name="Reason">A description of why the mapping failed.</param>
public sealed record MappingFailure<T>(string Reason) : MappingResult<T>;
