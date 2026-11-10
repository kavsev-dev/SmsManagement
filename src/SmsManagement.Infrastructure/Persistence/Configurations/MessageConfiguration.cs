using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmsManagement.Core.Entities;

namespace SmsManagement.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        var allowedStatuses = string.Join(", ", Enum.GetNames<MessageStatus>().Select(n => $"'{n}'"));

        builder.ToTable("Messages", t =>
            t.HasCheckConstraint("CK_Messages_Status", $"[Status] IN ({allowedStatuses})"));

        builder.HasKey(m => m.Id);

        // Sequential GUIDs generated client-side on Add keep the clustered index from fragmenting.
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.To).HasMaxLength(Message.ToMaxLength).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(Message.BodyMaxLength).IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(Message.StatusMaxLength)
            .IsRequired();

        builder.Property(m => m.ProviderName).HasMaxLength(Message.ProviderNameMaxLength).IsRequired();

        // No max length: maps to nvarchar(max).
        builder.Property(m => m.ErrorMessage);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.SentAt);

        // Supports GET /messages?status=X ordered by CreatedAt DESC.
        builder.HasIndex(m => new { m.Status, m.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Messages_Status_CreatedAt");

        // Supports the unfiltered GET /messages ordered by CreatedAt DESC.
        builder.HasIndex(m => m.CreatedAt)
            .IsDescending()
            .HasDatabaseName("IX_Messages_CreatedAt");
    }
}
