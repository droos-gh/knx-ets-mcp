// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase G: KNX Secure certificate tools.</summary>
[McpServerToolType]
public sealed class KnxSecureCertificateTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_certificates", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List KNX Secure device certificates in the project.
        """)]
    public async Task<JsonNode?> ListCertificates(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("certificates.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_add_certificate", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Import a KNX Secure device certificate (25-char string from QR code).
        """)]
    public async Task<JsonNode?> AddCertificate(
        string value,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("value", value);
        var result = await bridge.CallAsync("certificate.add", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_certificate", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a KNX Secure device certificate from the project.
        """)]
    public async Task<JsonNode?> DeleteCertificate(
        string certificate_ref,
        string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("certificateRef", certificate_ref);
        var result = await bridge.CallAsync("certificate.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
