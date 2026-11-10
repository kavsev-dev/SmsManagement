using Microsoft.Extensions.Logging;
using SmsManagement.Core.Contracts;
using SmsManagement.Core.Entities;
using SmsManagement.Core.Providers;
using SmsManagement.Core.Repositories;
using SmsManagement.Core.Validation;

namespace SmsManagement.Core.Services;

/// <summary>Application service implementing the message send / query use cases.</summary>
public sealed partial class MessageService(
    IMessageRepository messages,
    IEmployeeRepository employees,
    ISmsProviderFactory providerFactory,
    ISendMessageRequestValidator validator,
    TimeProvider timeProvider,
    ILogger<MessageService> logger) : IMessageService
{
    internal const string CancelledErrorMessage =
        "Dispatch was cancelled before the provider confirmed delivery.";

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return SendMessageResult.Invalid(validation.Errors);
        }

        // Validation guarantees To and Body are present and well-formed, and Provider is empty or supported.
        var to = PhoneNumberValidator.Compact(request.To!);
        var provider = providerFactory.GetProvider(
            string.IsNullOrWhiteSpace(request.Provider) ? SendMessageRequest.DefaultProvider : request.Provider);

        var employeeNames = await ResolveRecipientAsync(to, cancellationToken).ConfigureAwait(false);

        var message = Message.Create(to, request.Body!, provider.ProviderName, timeProvider.GetUtcNow());
        messages.Add(message);

        // Persist as Pending before contacting the provider so every dispatch attempt is auditable,
        // even if the process stops mid-flight.
        await messages.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await DispatchAsync(provider, message, cancellationToken).ConfigureAwait(false);

        return SendMessageResult.Created(MessageResponse.FromEntity(message, employeeNames));
    }

    public async Task<MessageResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await messages.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return null;
        }

        var names = await FindEmployeeNamesAsync([message], cancellationToken).ConfigureAwait(false);
        return MessageResponse.FromEntity(message, NamesFor(message, names));
    }

    public async Task<IReadOnlyList<MessageResponse>> ListAsync(MessageStatus? status, CancellationToken cancellationToken)
    {
        var list = await messages.ListAsync(status, cancellationToken).ConfigureAwait(false);
        var names = await FindEmployeeNamesAsync(list, cancellationToken).ConfigureAwait(false);
        return list.Select(m => MessageResponse.FromEntity(m, NamesFor(m, names))).ToList();
    }

    /// <summary>Matches recipients to employees on the fly, in one query: messages have no stored employee link.</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> FindEmployeeNamesAsync(
        IReadOnlyCollection<Message> list, CancellationToken cancellationToken)
    {
        var numbers = list
            .Select(m => PhoneNumberValidator.TryNormalizeToE164(m.To, out var e164) ? e164 : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return await employees.FindNamesByPhoneNumbersAsync(numbers, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> NamesFor(Message message, IReadOnlyDictionary<string, IReadOnlyList<string>> names) =>
        PhoneNumberValidator.TryNormalizeToE164(message.To, out var e164) && names.TryGetValue(e164, out var found)
            ? found
            : [];

    private async Task<IReadOnlyList<string>> ResolveRecipientAsync(string to, CancellationToken cancellationToken)
    {
        var e164 = PhoneNumberValidator.NormalizeToE164(to);
        var recipients = await employees.FindByPhoneNumberAsync(e164, cancellationToken).ConfigureAwait(false);

        if (recipients.Count == 0)
        {
            LogRecipientNotResolved(e164);
        }
        else
        {
            // Only the match count is logged: employee identity data is sensitive and must not reach the logs.
            LogRecipientResolved(e164, recipients.Count);
        }

        return recipients.Select(e => e.FullName).ToList();
    }

    private async Task DispatchAsync(ISmsProvider provider, Message message, CancellationToken cancellationToken)
    {
        SmsDeliveryResult result;
        try
        {
            result = await provider.SendSmsAsync(message.To, message.Body, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            message.MarkFailed(CancelledErrorMessage);
            // The caller has gone away; still record the outcome so the message does not stay Pending forever.
            await messages.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
#pragma warning disable CA1031 // Provider boundary: any unexpected provider fault must fail the message, not the request.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogProviderFault(ex, message.Id, provider.ProviderName);
            result = SmsDeliveryResult.Failure($"Unexpected error while dispatching through provider '{provider.ProviderName}'.");
        }

        if (result.IsSuccess)
        {
            message.MarkSent(timeProvider.GetUtcNow());
            LogMessageSent(message.Id, provider.ProviderName, result.ProviderMessageId);
        }
        else
        {
            message.MarkFailed(result.ErrorMessage);
            LogMessageFailed(message.Id, provider.ProviderName, result.ErrorMessage);
        }

        // The provider call has already happened; persist its outcome even if the client disconnects now.
        await messages.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {PhoneNumber} resolved to {Count} employee(s)")]
    private partial void LogRecipientResolved(string phoneNumber, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recipient {PhoneNumber} does not match any known employee")]
    private partial void LogRecipientNotResolved(string phoneNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message {MessageId} sent via {Provider} (provider id: {ProviderMessageId})")]
    private partial void LogMessageSent(Guid messageId, string provider, string? providerMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} failed via {Provider}: {Error}")]
    private partial void LogMessageFailed(Guid messageId, string provider, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Provider {Provider} threw while dispatching message {MessageId}")]
    private partial void LogProviderFault(Exception exception, Guid messageId, string provider);
}
