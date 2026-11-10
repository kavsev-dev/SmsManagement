namespace SmsManagement.Infrastructure.Persistence.Seeding;

/// <summary>A seed employee, identified by its natural key, with phone numbers in E.164 form.</summary>
internal sealed record EmployeeSeed(string FirstName, string LastName, string Address, IReadOnlyList<string> PhoneNumbers);

/// <summary>
/// Mock employees (Hebrew names and addresses) seeded on startup so that recipients of <c>POST /messages</c>
/// resolve to known employees. Mirrored by <c>/db/scripts/002_seed_employees.sql</c> for manual provisioning;
/// a unit test keeps them in sync.
/// </summary>
internal static class EmployeeSeedData
{
    /// <summary>Office landline shared by two employees: phone numbers are not globally unique.</summary>
    public const string SharedOfficeLine = "+97239683400";

    public static IReadOnlyList<EmployeeSeed> Employees { get; } =
    [
        // "Israel Israeli" from the specification, with two phone numbers.
        new("ישראל", "ישראלי", "דרך המכבים 68, ראשון לציון", ["+972501234567", "+972521234567"]),

        // Same name, different address: a distinct employee under the composite natural key.
        new("ישראל", "ישראלי", "ויצמן 40, כפר סבא", ["+972509876543"]),

        new("שרה", "כהן", "הרצל 12, רחובות", ["+972541112233"]),
        new("דוד", "לוי", "השקמה 5, בית דגן", ["+972502223344", SharedOfficeLine]),
        new("מרים", "מזרחי", "שדרות רוטשילד 8, תל אביב-יפו", ["+972533334455"]),
        new("יוסף", "פרץ", "בן גוריון 22, באר שבע", ["+972584445566"]),
        new("רחל", "ביטון", "הנשיא 3, חיפה", ["+972525556677", SharedOfficeLine]),
        new("אברהם", "פרידמן", "יפו 17, ירושלים", ["+972546667788"]),
        new("נועה", "שפירא", "הדקל 9, גילת", ["+972507778899"]),
    ];
}
