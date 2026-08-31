namespace DataFirst;

/// The outcome of validating data against a schema.
///
/// A union rather than a bool so the failure detail cannot be dropped by accident,
/// and so callers must decide what to do about it.
public union ValidationResult(Valid, Invalid);

public sealed record Valid
{
    public static readonly Valid Instance = new();
    public override string ToString() => "valid";
}

public sealed record Invalid(IReadOnlyList<ValidationError> Errors)
{
    public override string ToString() => string.Join("; ", Errors);
}

/// Where the data failed, and why. The path is what makes an error actionable in
/// a deeply nested structure.
public sealed record ValidationError(DataPath Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}

/// Raised at a boundary when data that must be valid is not.
public sealed class SchemaViolationException(Invalid invalid)
    : Exception($"Data does not match schema -- {invalid}")
{
    public IReadOnlyList<ValidationError> Errors { get; } = invalid.Errors;
}

public static class ValidationResults
{
    public static bool IsValid(this ValidationResult result) => result is Valid;

    public static IReadOnlyList<ValidationError> Errors(this ValidationResult result) =>
        result switch
        {
            Valid => [],
            Invalid(var errors) => errors
        };
}
