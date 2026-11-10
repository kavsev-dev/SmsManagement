using System.Text.RegularExpressions;
using SmsManagement.Infrastructure.Persistence.Seeding;

namespace SmsManagement.Tests.Persistence;

/// <summary>Keeps <c>/db/scripts/002_seed_employees.sql</c> in sync with <see cref="EmployeeSeedData"/>.</summary>
public sealed partial class SeedScriptConsistencyTests
{
    [Fact]
    public void Sql_seed_script_contains_exactly_the_application_seed_data()
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "db", "002_seed_employees.sql"));

        var scriptRows = SeedRow().Matches(script)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value))
            .ToList();

        var expectedRows = EmployeeSeedData.Employees
            .SelectMany(e => e.PhoneNumbers.Select(p => (e.FirstName, e.LastName, e.Address, p)))
            .ToList();

        scriptRows.Should().BeEquivalentTo(expectedRows);
    }

    [Fact]
    public void Seed_data_meets_the_specification()
    {
        var employees = EmployeeSeedData.Employees;

        employees.Should().HaveCountGreaterThanOrEqualTo(5).And.HaveCountLessThanOrEqualTo(10);
        employees.Should().Contain(e => e.FirstName == "ישראל" && e.LastName == "ישראלי" && e.PhoneNumbers.Count == 2);
        employees.Select(e => (e.FirstName, e.LastName, e.Address)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Seed_names_and_addresses_are_in_hebrew()
    {
        foreach (var employee in EmployeeSeedData.Employees)
        {
            HebrewText().IsMatch(employee.FirstName).Should().BeTrue(employee.FirstName);
            HebrewText().IsMatch(employee.LastName).Should().BeTrue(employee.LastName);
            HebrewText().IsMatch(employee.Address).Should().BeTrue(employee.Address);
        }
    }

    [Fact]
    public void Sql_seed_script_is_utf8_with_bom_so_sqlcmd_reads_hebrew_correctly()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "db", "002_seed_employees.sql"));

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }

    // Must contain Hebrew letters; digits, spaces and punctuation are allowed alongside them.
    [GeneratedRegex(@"^(?=.*\p{IsHebrew})[\p{IsHebrew}\d\s,\-]+$")]
    private static partial Regex HebrewText();

    // (N'First', N'Last', N'Address', N'+Phone')
    [GeneratedRegex(@"\(\s*N'([^']*)',\s*N'([^']*)',\s*N'([^']*)',\s*N'(\+\d+)'\s*\)")]
    private static partial Regex SeedRow();
}
