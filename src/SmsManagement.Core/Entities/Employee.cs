using SmsManagement.Core.Validation;

namespace SmsManagement.Core.Entities;

/// <summary>
/// An employee who can receive SMS messages. Employees have no business identifier (security restriction);
/// their natural identity is the composite of <see cref="FirstName"/>, <see cref="LastName"/> and <see cref="Address"/>.
/// </summary>
public sealed class Employee
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int AddressMaxLength = 255;

    private readonly List<EmployeePhone> _phones = [];

    // Required by EF Core for materialization.
    private Employee()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Address = string.Empty;
    }

    /// <exception cref="ArgumentException">Thrown when a name part or the address is empty or too long.</exception>
    public Employee(string firstName, string lastName, string address)
    {
        FirstName = Require(firstName, FirstNameMaxLength, nameof(firstName));
        LastName = Require(lastName, LastNameMaxLength, nameof(lastName));
        Address = Require(address, AddressMaxLength, nameof(address));
    }

    /// <summary>Internal database surrogate key. Never exposed as a business identifier.</summary>
    public long Id { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string Address { get; private set; }

    public IReadOnlyCollection<EmployeePhone> Phones => _phones.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Adds a phone number, stored in normalized E.164 form. Adding a number the employee already has is a no-op.
    /// The same number may belong to several employees (e.g. a shared office line).
    /// </summary>
    /// <returns>The new or already existing phone entry.</returns>
    /// <exception cref="ArgumentException">Thrown when the number is not a valid E.164 or Israeli phone number.</exception>
    public EmployeePhone AddPhone(string phoneNumber)
    {
        if (!PhoneNumberValidator.TryNormalizeToE164(phoneNumber, out var normalized))
        {
            throw new ArgumentException($"'{phoneNumber}' is not a valid phone number.", nameof(phoneNumber));
        }

        var existing = _phones.Find(p => p.PhoneNumber == normalized);
        if (existing is not null)
        {
            return existing;
        }

        var phone = new EmployeePhone(normalized);
        _phones.Add(phone);
        return phone;
    }

    private static string Require(string value, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }
}
