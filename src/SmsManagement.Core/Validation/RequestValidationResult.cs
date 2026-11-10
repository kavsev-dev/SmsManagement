namespace SmsManagement.Core.Validation;

/// <summary>
/// Outcome of validating a request. Errors are keyed by the (camelCase) JSON field name so they map
/// directly onto RFC 7807 validation problem details.
/// </summary>
public sealed class RequestValidationResult
{
    public static readonly RequestValidationResult Success = new(new Dictionary<string, string[]>());

    private RequestValidationResult(IReadOnlyDictionary<string, string[]> errors)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    /// <summary>Accumulates field errors and produces an immutable <see cref="RequestValidationResult"/>.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

        public Builder Add(string field, string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            if (!_errors.TryGetValue(field, out var list))
            {
                list = [];
                _errors[field] = list;
            }

            list.Add(error);
            return this;
        }

        public RequestValidationResult Build() =>
            _errors.Count == 0
                ? Success
                : new RequestValidationResult(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal));
    }
}
