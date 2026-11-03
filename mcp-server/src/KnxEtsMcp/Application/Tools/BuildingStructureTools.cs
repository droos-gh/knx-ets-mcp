// Generated from the former Python FastMCP server (server.py) during the .NET migration;
// maintained by hand from here on.
using System.ComponentModel;
using System.Text.Json.Nodes;
using KnxEtsMcp.BusinessLogic.Gateways;
using ModelContextProtocol.Server;

namespace KnxEtsMcp.Application.Tools;

/// <summary>MCP tools: Phase B: Building structure tools.</summary>
[McpServerToolType]
public sealed class BuildingStructureTools(IKnxBridge bridge)
{
    [McpServerTool(Name = "knx_list_building", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("""
        List the building structure: building parts, devices, and functions as a tree.
        """)]
    public async Task<JsonNode?> ListBuilding(
        CancellationToken cancellationToken = default)
    {
        var result = await bridge.CallAsync("building.list", null, null, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_create_building_part", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Create a building part (building, floor, room, etc.).

        Args:
            name: Name for the building part.
            type: Type: Building, BuildingPart, Floor, Room, DistributionBoard, Corridor, Stairway.
            parent_building_part_ref: Parent building part ref. Omit for top-level.
            space_usage: Optional space usage from KNX master data (e.g. "Office").
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> CreateBuildingPart(
        [Description("Name for the building part.")] string name,
        [Description("Type: Building, BuildingPart, Floor, Room, DistributionBoard, Corridor, Stairway.")] string type,
        [Description("Parent building part ref. Omit for top-level.")] string? parent_building_part_ref = null,
        [Description("Optional space usage from KNX master data (e.g. \"Office\").")] string? space_usage = null,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("name", name)
            .Set("type", type)
            .SetOptional("parentBuildingPartRef", parent_building_part_ref)
            .SetOptional("spaceUsage", space_usage);
        var result = await bridge.CallAsync("building.create", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_delete_building_part", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Delete a building part from the building structure.

        Args:
            building_part_ref: The ref of the building part to delete.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> DeleteBuildingPart(
        [Description("The ref of the building part to delete.")] string building_part_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingPartRef", building_part_ref);
        var result = await bridge.CallAsync("building.delete", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_rename_building_part", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Rename a building part.

        Args:
            building_part_ref: The ref of the building part.
            name: The new name.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> RenameBuildingPart(
        [Description("The ref of the building part.")] string building_part_ref,
        [Description("The new name.")] string name,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingPartRef", building_part_ref)
            .Set("name", name);
        var result = await bridge.CallAsync("building.rename", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_assign_device", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("""
        Assign a device to a building part (room, distribution board, etc.).

        Args:
            building_part_ref: The ref of the building part (must accept devices).
            device_ref: The ref of the device to assign.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> AssignDevice(
        [Description("The ref of the building part (must accept devices).")] string building_part_ref,
        [Description("The ref of the device to assign.")] string device_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingPartRef", building_part_ref)
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("building.assignDevice", parameters, expected_project_revision, cancellationToken);
        return result;
    }

    [McpServerTool(Name = "knx_unassign_device", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("""
        Unassign a device from a building part.

        Args:
            building_part_ref: The ref of the building part.
            device_ref: The ref of the device to unassign.
            expected_project_revision: Optional revision guard against concurrent edits.
        """)]
    public async Task<JsonNode?> UnassignDevice(
        [Description("The ref of the building part.")] string building_part_ref,
        [Description("The ref of the device to unassign.")] string device_ref,
        [Description("Optional revision guard against concurrent edits.")] string? expected_project_revision = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new JsonObject()
            .Set("buildingPartRef", building_part_ref)
            .Set("deviceRef", device_ref);
        var result = await bridge.CallAsync("building.unassignDevice", parameters, expected_project_revision, cancellationToken);
        return result;
    }
}
