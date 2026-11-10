using Microsoft.EntityFrameworkCore;
using SmsManagement.Core.Entities;

namespace SmsManagement.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotently upserts <see cref="EmployeeSeedData"/>: missing employees are inserted, and missing phone numbers
/// are added to existing employees. Existing data is never modified or removed, so it is safe to run on every startup.
/// </summary>
internal static class EmployeeSeeder
{
    /// <returns>The number of employees and phone numbers that were added.</returns>
    public static async Task<(int EmployeesAdded, int PhonesAdded)> SeedAsync(
        ApplicationDbContext dbContext,
        IEnumerable<EmployeeSeed> seeds,
        CancellationToken cancellationToken)
    {
        var employeesAdded = 0;
        var phonesAdded = 0;

        foreach (var seed in seeds)
        {
            var employee = await dbContext.Employees
                .Include(e => e.Phones)
                .FirstOrDefaultAsync(
                    e => e.FirstName == seed.FirstName && e.LastName == seed.LastName && e.Address == seed.Address,
                    cancellationToken)
                .ConfigureAwait(false);

            if (employee is null)
            {
                employee = new Employee(seed.FirstName, seed.LastName, seed.Address);
                dbContext.Employees.Add(employee);
                employeesAdded++;
            }

            foreach (var phoneNumber in seed.PhoneNumbers)
            {
                var countBefore = employee.Phones.Count;
                employee.AddPhone(phoneNumber);
                phonesAdded += employee.Phones.Count - countBefore;
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (employeesAdded, phonesAdded);
    }
}
