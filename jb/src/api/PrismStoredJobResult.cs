namespace Prism.Api;

/// <summary>
/// Stored result projection for result endpoint callers.
/// </summary>
internal sealed record PrismStoredJobResult(string Status, PrismJobResult? Result, bool IsTerminal);
