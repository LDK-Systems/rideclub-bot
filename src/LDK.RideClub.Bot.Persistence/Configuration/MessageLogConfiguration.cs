using LDK.RideClub.Bot.Persistence.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LDK.RideClub.Bot.Persistence.Configuration;

/// <summary>
/// Entity Framework Core configuration for the <see cref="MessageLog"/> entity.
/// Defines table name, column constraints, and indexes.
/// </summary>
#pragma warning disable CA1812 // Instantiated via ApplyConfigurationsFromAssembly
internal sealed class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageLog> builder)
    {
        _ = builder.ToTable("MessageLogs");

        _ = builder.HasKey(m => m.Id);
        _ = builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        _ = builder.Property(m => m.Platform)
            .IsRequired()
            .HasMaxLength(32);

        _ = builder.Property(m => m.EventId)
            .IsRequired()
            .HasMaxLength(256);

        _ = builder.Property(m => m.SenderId)
            .IsRequired()
            .HasMaxLength(256);

        _ = builder.Property(m => m.ConversationId)
            .IsRequired()
            .HasMaxLength(256);

        _ = builder.Property(m => m.ReceivedAt)
            .IsRequired();

        _ = builder.Property(m => m.PayloadType)
            .IsRequired()
            .HasMaxLength(64);

        _ = builder.Property(m => m.RawPayload)
            .IsRequired(false);

        // Unique index on EventId to prevent duplicate event processing
        _ = builder.HasIndex(m => m.EventId)
            .IsUnique();

        // Index on ConversationId for conversation history queries
        _ = builder.HasIndex(m => m.ConversationId);

        // Composite index on (Platform, ReceivedAt) for common query patterns
        _ = builder.HasIndex(m => new { m.Platform, m.ReceivedAt });
    }
}
