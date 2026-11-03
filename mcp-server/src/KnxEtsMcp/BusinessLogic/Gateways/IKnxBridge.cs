using System.Text.Json.Nodes;

namespace KnxEtsMcp.BusinessLogic.Gateways;

/// <summary>
/// The ETS AddIn as seen by the MCP tools: one call per protocol method
/// (docs/protocol.md). Implementations handle the envelope, the session token,
/// idempotency keys, retries and serialization of concurrent calls.
/// </summary>
public interface IKnxBridge
{
    /// <summary>
    /// Calls <paramref name="method"/> with <paramref name="parameters"/> and returns
    /// the protocol <c>result</c>, or the protocol error.
    /// </summary>
    /// <param name="expectedRevision">Optional <c>expectedProjectRevision</c> guard.</param>
    Task<Result<JsonNode?>> RequestAsync(
        string method,
        JsonObject? parameters = null,
        string? expectedRevision = null,
        CancellationToken cancellationToken = default);
}
