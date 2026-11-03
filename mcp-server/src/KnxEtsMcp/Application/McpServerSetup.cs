using KnxEtsMcp.Application.Tools;

namespace KnxEtsMcp.Application;

/// <summary>Registers every KNX tool class (shared by Program.cs and the tests).</summary>
public static class McpServerSetup
{
    /// <summary>Adds all KNX MCP tools. Explicit list (no assembly scanning) so the
    /// single-file exe does not depend on reflection-based discovery.</summary>
    public static IMcpServerBuilder AddKnxTools(this IMcpServerBuilder builder)
        => builder
            .WithTools<ReadTools>()
            .WithTools<MutationTools>()
            .WithTools<GroupAddressEditingTools>()
            .WithTools<GroupRangeTools>()
            .WithTools<DeviceEditingTools>()
            .WithTools<TopologyTools>()
            .WithTools<ComObjectFlagsTools>()
            .WithTools<LabelSetterTools>()
            .WithTools<ProgrammingTools>()
            .WithTools<BuildingStructureTools>()
            .WithTools<BuildingFunctionTools>()
            .WithTools<ProjectManagementTools>()
            .WithTools<CatalogBrowsingTools>()
            .WithTools<BusOnlineOperationTools>()
            .WithTools<ReconciliationTools>()
            .WithTools<DeviceResetTools>()
            .WithTools<MoveTools>()
            .WithTools<AdditionalAddressTools>()
            .WithTools<SegmentTools>()
            .WithTools<TradeTools>()
            .WithTools<BusInterfaceFilterTools>()
            .WithTools<ParameterDefaultTools>()
            .WithTools<KnxSecureCertificateTools>()
            .WithTools<NavigationTools>()
            .WithTools<TagTools>()
            .WithTools<ToDoTools>()
            .WithTools<ChannelModuleTools>()
            .WithTools<ProjectHistoryTools>()
            .WithTools<BatchTools>();
}
