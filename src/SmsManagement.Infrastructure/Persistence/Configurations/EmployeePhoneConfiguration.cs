using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmsManagement.Core.Entities;

namespace SmsManagement.Infrastructure.Persistence.Configurations;

internal sealed class EmployeePhoneConfiguration : IEntityTypeConfiguration<EmployeePhone>
{
    public void Configure(EntityTypeBuilder<EmployeePhone> builder)
    {
        builder.ToTable("EmployeePhones");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn();

        builder.Property(p => p.PhoneNumber)
            .HasMaxLength(EmployeePhone.PhoneNumberMaxLength)
            .IsRequired();

        // Recipient lookup by phone number. Deliberately NOT unique: numbers can be shared between employees.
        builder.HasIndex(p => p.PhoneNumber)
            .HasDatabaseName("IX_EmployeePhones_PhoneNumber");

        // A single employee cannot list the same number twice.
        builder.HasIndex(p => new { p.EmployeeId, p.PhoneNumber })
            .IsUnique()
            .HasDatabaseName("UX_EmployeePhones_EmployeeId_PhoneNumber");
    }
}
