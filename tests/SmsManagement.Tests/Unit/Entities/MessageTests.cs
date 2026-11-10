using SmsManagement.Core.Entities;

namespace SmsManagement.Tests.Unit.Entities;

public sealed class MessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_in_pending_with_utc_timestamp()
    {
        var local = new DateTimeOffset(2026, 11, 8, 21, 0, 0, TimeSpan.FromHours(2));

        var message = Message.Create("0501234567", "Hello", "Mock", local);

        message.Status.Should().Be(MessageStatus.Pending);
        message.CreatedAt.Should().Be(Now);
        message.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
        message.SentAt.Should().BeNull();
        message.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkSent_sets_status_and_sent_time()
    {
        var message = Message.Create("0501234567", "Hello", "Mock", Now);

        message.MarkSent(Now.AddSeconds(1));

        message.Status.Should().Be(MessageStatus.Sent);
        message.SentAt.Should().Be(Now.AddSeconds(1));
    }

    [Fact]
    public void MarkFailed_sets_status_and_error()
    {
        var message = Message.Create("0501234567", "Hello", "Mock", Now);

        message.MarkFailed("Provider unreachable");

        message.Status.Should().Be(MessageStatus.Failed);
        message.ErrorMessage.Should().Be("Provider unreachable");
        message.SentAt.Should().BeNull();
    }

    [Fact]
    public void Status_can_change_only_once()
    {
        var message = Message.Create("0501234567", "Hello", "Mock", Now);
        message.MarkSent(Now);

        FluentActions.Invoking(() => message.MarkFailed("late failure")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => message.MarkSent(Now)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "Hello", "Mock")]
    [InlineData("0501234567", " ", "Mock")]
    [InlineData("0501234567", "Hello", "")]
    [InlineData("012345678901234567890", "Hello", "Mock")] // To > 20 chars
    public void Create_rejects_values_violating_column_constraints(string to, string body, string provider) =>
        FluentActions.Invoking(() => Message.Create(to, body, provider, Now)).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_rejects_body_over_160_characters() =>
        FluentActions.Invoking(() => Message.Create("0501234567", new string('a', 161), "Mock", Now))
            .Should().Throw<ArgumentException>();
}
