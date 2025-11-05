namespace BuildingBlocks.Logging;

/// <summary>
/// Provides access to the current request's correlation ID
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the correlation ID for the current request/operation
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Sets the correlation ID for the current request/operation
    /// </summary>
    void SetCorrelationId(string correlationId);
}
