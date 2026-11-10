using Microsoft.EntityFrameworkCore;
using SmsManagement.Core.Entities;
using SmsManagement.Core.Repositories;

namespace SmsManagement.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(ApplicationDbContext dbContext) : IEmployeeRepository
{
    public async Task<IReadOnlyList<Employee>> FindByPhoneNumberAsync(string e164PhoneNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(e164PhoneNumber);

        return await dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Phones.Any(p => p.PhoneNumber == e164PhoneNumber))
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> FindNamesByPhoneNumbersAsync(
        IReadOnlyCollection<string> e164PhoneNumbers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e164PhoneNumbers);
        if (e164PhoneNumbers.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var owners = await dbContext.EmployeePhones
            .AsNoTracking()
            .Where(p => e164PhoneNumbers.Contains(p.PhoneNumber))
            .OrderBy(p => p.Employee!.LastName)
            .ThenBy(p => p.Employee!.FirstName)
            .Select(p => new { p.PhoneNumber, p.Employee!.FirstName, p.Employee.LastName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return owners
            .GroupBy(o => o.PhoneNumber, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(o => $"{o.FirstName} {o.LastName}").ToList(),
                StringComparer.Ordinal);
    }
}
