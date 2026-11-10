using SmsManagement.Core.Entities;

namespace SmsManagement.Core.Repositories;

public interface IEmployeeRepository
{
    /// <summary>
    /// Returns every employee who owns <paramref name="e164PhoneNumber"/>. Phone numbers are not globally unique,
    /// so the result can contain several employees (e.g. a shared office line), or none.
    /// </summary>
    Task<IReadOnlyList<Employee>> FindByPhoneNumberAsync(string e164PhoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the full names of the employees who own each of <paramref name="e164PhoneNumbers"/>, in one query.
    /// Numbers that belong to no employee are absent from the result. Names are ordered by last, then first name.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> FindNamesByPhoneNumbersAsync(
        IReadOnlyCollection<string> e164PhoneNumbers, CancellationToken cancellationToken);
}
