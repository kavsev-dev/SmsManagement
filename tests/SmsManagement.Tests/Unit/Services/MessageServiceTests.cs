using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;
using SmsManagement.Core.Contracts;
using SmsManagement.Core.Entities;
using SmsManagement.Core.Providers;
using SmsManagement.Core.Repositories;
using SmsManagement.Core.Services;
using SmsManagement.Core.Validation;
using SmsManagement.Infrastructure.Providers;

namespace SmsManagement.Tests.Unit.Services;

public sealed class MessageServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 19, 0, 0, TimeSpan.Zero);

    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly ISmsProvider _mockProvider = Substitute.For<ISmsProvider>();
    private readonly ISmsProvider _secondaryProvider = Substitute.For<ISmsProvider>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly List<MessageStatus> _statusAtEachSave = [];
    private readonly MessageService _service;
    private Message? _added;

    public MessageServiceTests()
    {
        _mockProvider.ProviderName.Returns(SmsProviderNames.Mock);
        _secondaryProvider.ProviderName.Returns(SmsProviderNames.Secondary);

        _messages.When(r => r.Add(Arg.Any<Message>())).Do(c => _added = c.Arg<Message>());
        _messages.When(r => r.SaveChangesAsync(Arg.Any<CancellationToken>())).Do(_ => _statusAtEachSave.Add(_added!.Status));
        _employees.FindByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _employees.FindNamesByPhoneNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<string>>());

        var factory = new SmsProviderFactory([_mockProvider, _secondaryProvider]);
        _service = new MessageService(
            _messages,
            _employees,
            factory,
            new SendMessageRequestValidator(factory),
            _time,
            NullLogger<MessageService>.Instance);
    }

    [Fact]
    public async Task Successful_dispatch_persists_pending_then_sent()
    {
        _mockProvider.SendSmsAsync("0501234567", "Hello", Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("mock-123"));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "Mock"), CancellationToken.None);

        result.IsCreated.Should().BeTrue();
        result.Message!.Status.Should().Be(MessageStatus.Sent);
        result.Message.ProviderName.Should().Be("Mock");
        result.Message.To.Should().Be("0501234567");
        result.Message.CreatedAt.Should().Be(Now);
        result.Message.SentAt.Should().Be(Now);
        _statusAtEachSave.Should().Equal(MessageStatus.Pending, MessageStatus.Sent);
        await _secondaryProvider.DidNotReceiveWithAnyArgs().SendSmsAsync(default!, default!, default);
    }

    [Fact]
    public async Task Provider_is_selected_by_the_requested_key_case_insensitively()
    {
        _secondaryProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("SM1"));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "secondary"), CancellationToken.None);

        result.Message!.ProviderName.Should().Be("Secondary");
        await _secondaryProvider.Received(1).SendSmsAsync("0501234567", "Hello", Arg.Any<CancellationToken>());
        await _mockProvider.DidNotReceiveWithAnyArgs().SendSmsAsync(default!, default!, default);
    }

    [Fact]
    public async Task Recipient_is_compacted_before_storage_and_looked_up_in_e164()
    {
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("mock-1"));

        var result = await _service.SendAsync(new SendMessageRequest(" 050-123-4567 ", "Hello", "Mock"), CancellationToken.None);

        result.Message!.To.Should().Be("0501234567");
        await _employees.Received(1).FindByPhoneNumberAsync("+972501234567", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provider_failure_marks_message_failed_with_the_provider_error()
    {
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Failure("Mock provider is unreachable."));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "Mock"), CancellationToken.None);

        result.Message!.Status.Should().Be(MessageStatus.Failed);
        result.Message.ErrorMessage.Should().Be("Mock provider is unreachable.");
        result.Message.SentAt.Should().BeNull();
        _statusAtEachSave.Should().Equal(MessageStatus.Pending, MessageStatus.Failed);
    }

    [Fact]
    public async Task Unexpected_provider_exception_marks_message_failed_without_leaking_details()
    {
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("secret internal detail"));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "Mock"), CancellationToken.None);

        result.Message!.Status.Should().Be(MessageStatus.Failed);
        result.Message.ErrorMessage.Should().Contain("Mock").And.NotContain("secret");
    }

    [Fact]
    public async Task Cancellation_during_dispatch_records_failure_and_rethrows()
    {
        using var cts = new CancellationTokenSource();
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<SmsDeliveryResult>>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var act = () => _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "Mock"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _added!.Status.Should().Be(MessageStatus.Failed);
        _added.ErrorMessage.Should().Be(MessageService.CancelledErrorMessage);
        await _messages.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_without_persistence_or_dispatch()
    {
        var result = await _service.SendAsync(new SendMessageRequest("abc", "", "Unknown"), CancellationToken.None);

        result.IsCreated.Should().BeFalse();
        result.ValidationErrors.Keys.Should().BeEquivalentTo("to", "body", "provider");
        _messages.DidNotReceiveWithAnyArgs().Add(default!);
        await _messages.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await _mockProvider.DidNotReceiveWithAnyArgs().SendSmsAsync(default!, default!, default);
    }

    [Fact]
    public async Task Request_without_provider_is_dispatched_through_mock()
    {
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("mock-1"));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello"), CancellationToken.None);

        result.Message!.ProviderName.Should().Be(SmsProviderNames.Mock);
        await _mockProvider.Received(1).SendSmsAsync("0501234567", "Hello", Arg.Any<CancellationToken>());
        await _secondaryProvider.DidNotReceiveWithAnyArgs().SendSmsAsync(default!, default!, default);
    }

    [Fact]
    public async Task Unknown_recipient_is_still_dispatched()
    {
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("mock-1"));

        var result = await _service.SendAsync(new SendMessageRequest("+14155550123", "Hello", "Mock"), CancellationToken.None);

        result.Message!.Status.Should().Be(MessageStatus.Sent);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        (await _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_passes_the_status_filter_and_maps_results()
    {
        var sent = Message.Create("0501234567", "Hi", "Mock", Now);
        sent.MarkSent(Now);
        _messages.ListAsync(MessageStatus.Sent, Arg.Any<CancellationToken>()).Returns([sent]);

        var list = await _service.ListAsync(MessageStatus.Sent, CancellationToken.None);

        list.Should().ContainSingle().Which.Status.Should().Be(MessageStatus.Sent);
    }

    [Fact]
    public async Task Send_response_names_the_employees_who_own_the_recipient_number()
    {
        _employees.FindByPhoneNumberAsync("+972501234567", Arg.Any<CancellationToken>())
            .Returns([new Employee("ישראל", "ישראלי", "דרך המכבים 68, ראשון לציון")]);
        _mockProvider.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SmsDeliveryResult.Success("mock-1"));

        var result = await _service.SendAsync(new SendMessageRequest("0501234567", "Hello", "Mock"), CancellationToken.None);

        result.Message!.EmployeeNames.Should().Equal("ישראל ישראלי");
    }

    [Fact]
    public async Task ListAsync_matches_employee_names_in_one_lookup_by_e164_number()
    {
        var known = Message.Create("0501234567", "Hi", "Mock", Now);
        var sameNumberInternational = Message.Create("+972501234567", "Hi again", "Mock", Now);
        var unknown = Message.Create("+14155550123", "Hello", "Mock", Now);
        _messages.ListAsync(null, Arg.Any<CancellationToken>()).Returns([known, sameNumberInternational, unknown]);
        _employees.FindNamesByPhoneNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<string>> { ["+972501234567"] = ["ישראל ישראלי"] });

        var list = await _service.ListAsync(null, CancellationToken.None);

        list[0].EmployeeNames.Should().Equal("ישראל ישראלי");
        list[1].EmployeeNames.Should().Equal("ישראל ישראלי");
        list[2].EmployeeNames.Should().BeEmpty();
        await _employees.Received(1).FindNamesByPhoneNumbersAsync(
            Arg.Is<IReadOnlyCollection<string>>(n => n.Count == 2 && n.Contains("+972501234567") && n.Contains("+14155550123")),
            Arg.Any<CancellationToken>());
    }
}
