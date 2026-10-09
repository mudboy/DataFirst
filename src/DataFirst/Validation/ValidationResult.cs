namespace DataFirst;

/// <summary>
/// The outcome of validating data against a schema.
/// </summary>
/// <remarks>
/// A union rather than a bool so the failure detail cannot be dropped by accident,
/// and so callers must decide what to do about it.
/// </remarks>
/// <example>
/// <code>
/// var message = Validation.Validate(schema, data) switch
/// {
///     Valid => "ok",
///     Invalid(var errors) => $"{errors.Count} problems"
/// };
/// </code>
/// </example>
public union ValidationResult(Valid, Invalid);

/// <summary>
/// The data conformed to the schema.
/// </summary>
public sealed record Valid
{
    /// <summary>The single shared instance.</summary>
    public static readonly Valid Instance = new();

    /// <inheritdoc/>
    public override string ToString() => "valid";
}

/// <summary>
/// The data did not conform to the schema.
/// </summary>
/// <param name="Errors">Every problem found, not just the first.</param>
public sealed record Invalid(IReadOnlyList<ValidationError> Errors)
{
    /// <summary>The errors, separated by semicolons.</summary>
    public override string ToString() => string.Join("; ", Errors);
}

/// <summary>
/// Where the data failed, and why. The path is what makes an error actionable in
/// a deeply nested structure.
/// </summary>
/// <param name="Path">The location of the offending value, from the root of the data.</param>
/// <param name="Message">What is wrong with it.</param>
/// <example>
/// <code>
/// new ValidationError(DataPath.Of("authorIds", 0), "must be at least 1 characters, but was 0")
///     // authorIds.[0]: must be at least 1 characters, but was 0
/// </code>
/// </example>
public sealed record ValidationError(DataPath Path, string Message)
{
    /// <summary>The path and message, as <c>path: message</c>.</summary>
    public override string ToString() => $"{Path}: {Message}";
}

/// <summary>
/// Raised at a boundary when data that must be valid is not.
/// </summary>
/// <param name="invalid">The failed validation, whose errors the exception carries.</param>
public sealed class SchemaViolationException(Invalid invalid)
    : Exception($"Data does not match schema -- {invalid}")
{
    /// <summary>Every problem found in the data.</summary>
    public IReadOnlyList<ValidationError> Errors { get; } = invalid.Errors;
}

/// <summary>
/// Convenience readers for <see cref="ValidationResult"/>.
/// </summary>
public static class ValidationResults
{
    /// <summary>
    /// Tests whether validation succeeded.
    /// </summary>
    /// <param name="result">The result to test.</param>
    /// <returns>True for <see cref="Valid"/>.</returns>
    /// <example>
    /// <code>
    /// Validation.Validate(Map.Of(("type", "string")), "text").IsValid()   // true
    /// </code>
    /// </example>
    public static bool IsValid(this ValidationResult result) => result is Valid;

    /// <summary>
    /// Lists the problems found.
    /// </summary>
    /// <param name="result">The result to read.</param>
    /// <returns>The errors; empty for <see cref="Valid"/>.</returns>
    /// <example>
    /// <code>
    /// Validation.Validate(Map.Of(("type", "string")), 5).Errors().Single().ToString()
    ///     // (root): expected string, but found integer
    /// </code>
    /// </example>
    public static IReadOnlyList<ValidationError> Errors(this ValidationResult result) =>
        result switch
        {
            Valid => [],
            Invalid(var errors) => errors
        };
}
