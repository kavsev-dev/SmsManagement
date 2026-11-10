namespace SmsManagement.Core.Entities;

/// <summary>A phone number belonging to an <see cref="Employee"/>, stored in normalized E.164 form.</summary>
public sealed class EmployeePhone
{
    public const int PhoneNumberMaxLength = 20;

    // Required by EF Core for materialization.
    private EmployeePhone()
    {
        PhoneNumber = string.Empty;
    }

    internal EmployeePhone(string normalizedPhoneNumber)
    {
        PhoneNumber = normalizedPhoneNumber;
    }

    public long Id { get; private set; }

    public long EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    /// <summary>Phone number in E.164 format, e.g. <c>+972501234567</c>.</summary>
    public string PhoneNumber { get; private set; }
}
