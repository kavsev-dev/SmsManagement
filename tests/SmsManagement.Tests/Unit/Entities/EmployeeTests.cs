using SmsManagement.Core.Entities;

namespace SmsManagement.Tests.Unit.Entities;

public sealed class EmployeeTests
{
    [Fact]
    public void Constructor_trims_natural_key_parts()
    {
        var employee = new Employee("  Israel ", " Israeli", "68 HaMaccabim Road ");

        employee.FirstName.Should().Be("Israel");
        employee.LastName.Should().Be("Israeli");
        employee.Address.Should().Be("68 HaMaccabim Road");
        employee.FullName.Should().Be("Israel Israeli");
    }

    [Fact]
    public void Hebrew_natural_key_is_preserved()
    {
        var employee = new Employee(" ישראל ", "ישראלי", "דרך המכבים 68, ראשון לציון");

        employee.FirstName.Should().Be("ישראל");
        employee.FullName.Should().Be("ישראל ישראלי");
        employee.Address.Should().Be("דרך המכבים 68, ראשון לציון");
    }

    [Theory]
    [InlineData("", "Israeli", "Address")]
    [InlineData("Israel", " ", "Address")]
    [InlineData("Israel", "Israeli", "")]
    public void Constructor_requires_every_natural_key_part(string first, string last, string address) =>
        FluentActions.Invoking(() => new Employee(first, last, address)).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_enforces_column_lengths() =>
        FluentActions.Invoking(() => new Employee(new string('a', 101), "Israeli", "Address"))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Employee_can_have_multiple_phone_numbers_stored_in_e164()
    {
        var employee = new Employee("Israel", "Israeli", "68 HaMaccabim Road");

        employee.AddPhone("0501234567");
        employee.AddPhone("+972521234567");

        employee.Phones.Select(p => p.PhoneNumber).Should().Equal("+972501234567", "+972521234567");
    }

    [Fact]
    public void Adding_the_same_number_in_another_format_is_a_no_op()
    {
        var employee = new Employee("Israel", "Israeli", "68 HaMaccabim Road");
        var first = employee.AddPhone("050-123-4567");

        var second = employee.AddPhone("+972501234567");

        second.Should().BeSameAs(first);
        employee.Phones.Should().ContainSingle();
    }

    [Fact]
    public void AddPhone_rejects_invalid_numbers() =>
        FluentActions.Invoking(() => new Employee("Israel", "Israeli", "Address").AddPhone("123"))
            .Should().Throw<ArgumentException>();
}
