using LDK.RideClub.Bot.Persistence.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LDK.RideClub.Bot.Persistence.Configuration;

/// <summary>
/// Entity Framework Core configuration for the <see cref="ConversationSagaInstance"/> entity.
/// Defines table name, column constraints, indexes, and concurrency token.
/// </summary>
#pragma warning disable CA1812 // Instantiated via ApplyConfigurationsFromAssembly
internal sealed class ConversationSagaInstanceConfiguration
    : IEntityTypeConfiguration<ConversationSagaInstance>
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConversationSagaInstance> builder)
    {
        _ = builder.ToTable("ConversationSagas");

        _ = builder.HasKey(x => x.CorrelationId);

        _ = builder.HasIndex(x => x.CorrelationKey)
            .IsUnique();

        _ = builder.Property(x => x.RowVersion)
            .IsRowVersion();

        _ = builder.Property(x => x.CurrentState)
            .IsRequired()
            .HasMaxLength(64);

        _ = builder.Property(x => x.CorrelationKey)
            .IsRequired()
            .HasMaxLength(256);

        _ = builder.Property(x => x.Platform)
            .IsRequired()
            .HasMaxLength(64);

        _ = builder.Property(x => x.SenderId)
            .IsRequired()
            .HasMaxLength(128);

        _ = builder.Property(x => x.ConversationId)
            .IsRequired()
            .HasMaxLength(128);
    }
}
