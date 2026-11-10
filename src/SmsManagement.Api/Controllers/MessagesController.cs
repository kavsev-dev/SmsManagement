using Microsoft.AspNetCore.Mvc;
using SmsManagement.Core.Contracts;
using SmsManagement.Core.Entities;
using SmsManagement.Core.Services;

namespace SmsManagement.Api.Controllers;

/// <summary>Send SMS messages and query their delivery status.</summary>
[ApiController]
[Route("messages")]
public sealed class MessagesController(IMessageService messageService) : ControllerBase
{
    /// <summary>Sends an SMS message through the selected provider.</summary>
    /// <remarks>
    /// Validates the request, stores the message as <c>Pending</c>, dispatches it through the provider selected by
    /// <c>provider</c> (<c>Mock</c> or <c>Secondary</c>; <c>Mock</c> when omitted), and returns the stored message with its final status:
    /// <c>Sent</c>, or <c>Failed</c> with an <c>errorMessage</c>. A provider failure is not an HTTP error: the
    /// message is still created (201) and its status records the failure.
    /// </remarks>
    /// <!-- No <param> tags here: the .NET 10 OpenAPI generator attached the cancellationToken description to the
    ///      request body. The body fields are documented on SendMessageRequest. -->
    /// <response code="201">The message was created and a dispatch was attempted.</response>
    /// <response code="400">The request is invalid (RFC 7807 validation problem details).</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<MessageResponse>> SendAsync(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await messageService.SendAsync(request, cancellationToken);

        if (!result.IsCreated)
        {
            foreach (var (field, errors) in result.ValidationErrors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(field, error);
                }
            }

            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetByIdAsync), new { id = result.Message.Id }, result.Message);
    }

    /// <summary>Returns a single message by ID.</summary>
    /// <param name="id">Message ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The message.</response>
    /// <response code="404">No message has this ID.</response>
    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetByIdAsync))]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MessageResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await messageService.GetByIdAsync(id, cancellationToken);

        return message is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Message not found",
                detail: $"No message exists with ID '{id}'.")
            : message;
    }

    /// <summary>Lists messages, newest first, optionally filtered by status.</summary>
    /// <param name="status">Optional status filter: <c>Pending</c>, <c>Sent</c> or <c>Failed</c> (case-insensitive). Omit or leave empty for all messages.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Messages ordered by <c>createdAt</c> descending.</response>
    /// <response code="400">The status filter is not one of the allowed values.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MessageResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> ListAsync(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        MessageStatus? filter = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            // Strict: only the names are accepted. Default enum binding would also accept numbers ("1") and
            // undefined values, which the specification does not allow.
            var name = Enum.GetNames<MessageStatus>()
                .FirstOrDefault(n => string.Equals(n, status.Trim(), StringComparison.OrdinalIgnoreCase));

            if (name is null)
            {
                ModelState.AddModelError(
                    nameof(status),
                    $"Status must be one of: {string.Join(", ", Enum.GetNames<MessageStatus>())}; or empty for all messages.");
                return ValidationProblem(ModelState);
            }

            filter = Enum.Parse<MessageStatus>(name);
        }

        var messages = await messageService.ListAsync(filter, cancellationToken);
        return Ok(messages);
    }
}
