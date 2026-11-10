using SmsManagement.Core.Entities;

namespace SmsManagement.Core.Contracts;

/// <summary>
/// Public representation of a <see cref="Message"/>. <c>EmployeeNames</c> holds the full names of the employees who
/// own the recipient number, matched when the message is read (messages have no stored employee link). It is empty
/// when the number belongs to no employee, and has several names for a shared number.
/// </summary>
public sealed record MessageResponse(
    Guid Id,
    string To,
    IReadOnlyList<string> EmployeeNames,
    string Body,
    MessageStatus Status,
    string ProviderName,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt)
{
    public static MessageResponse FromEntity(Message message, IReadOnlyList<string> employeeNames)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(employeeNames);
        return new MessageResponse(
            message.Id,
            message.To,
            employeeNames,
            message.Body,
            message.Status,
            message.ProviderName,
            message.ErrorMessage,
            message.CreatedAt,
            message.SentAt);
    }
}
