using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmsManagement.Core.Entities;

namespace SmsManagement.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public const string NaturalKeyIndexName = "UX_Employees_FirstName_LastName_Address";

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.FirstName).HasMaxLength(Employee.FirstNameMaxLength).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(Employee.LastNameMaxLength).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(Employee.AddressMaxLength).IsRequired();

        // Natural composite identity: employees have no business ID (security restriction).
        builder.HasIndex(e => new { e.FirstName, e.LastName, e.Address })
            .IsUnique()
            .HasDatabaseName(NaturalKeyIndexName);

        builder.HasMany(e => e.Phones)
            .WithOne(p => p.Employee)
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Phones)
            .HasField("_phones")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.FullName);
    }
}
