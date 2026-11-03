namespace KnxEtsMcp.BusinessLogic;

/// <summary>Protocol facts shared by the bridge client and the mock AddIn.</summary>
public static class ProtocolMethods
{
    /// <summary>
    /// Methods that mutate the project or the bus and therefore carry a fresh
    /// idempotency key per operation (reused only when the client itself retries).
    /// </summary>
    public static readonly IReadOnlySet<string> Mutating = new HashSet<string>(StringComparer.Ordinal)
    {
        "ga.create", "ga.delete", "ga.rename", "ga.setDescription", "ga.setDatapointType",
        "groupRange.create", "groupRange.delete",
        "device.addFromCatalog", "device.delete", "device.rename",
        "device.setAddress", "device.move",
        "link.create", "link.delete",
        "param.set", "device.program", "firmware.update",
        "area.create", "area.delete",
        "line.create", "line.delete",
        "comObject.setFlags",
        "device.setDescription", "device.setComment",
        "comObject.setDescription", "comObject.setFunctionText",
        "catalog.import", "catalog.internalize",
        "building.create", "building.delete", "building.rename",
        "building.assignDevice", "building.unassignDevice",
        "buildingFunction.create", "buildingFunction.delete",
        "buildingFunction.linkGroupAddress", "buildingFunction.unlinkGroupAddress",
        "project.undo", "project.redo",
        "group.write", "device.unload",
        "bus.setIndividualAddress", "device.reset",
        "device.unassign",
        "groupRange.moveGroupAddress", "groupRange.moveGroupRange",
        "line.move", "buildingPart.move",
        "device.addAdditionalAddress", "device.removeAdditionalAddress",
        "line.addAdditionalGroupAddress", "line.removeAdditionalGroupAddress",
        "segment.create", "segment.delete",
        "trade.create", "trade.delete", "trade.assignDevice", "trade.unassignDevice",
        "busInterface.link", "busInterface.unlink",
        "param.setDefault",
        "certificate.add", "certificate.delete",
        "tag.create", "tag.delete",
        "todo.create", "todo.delete",
        "projectHistory.add", "projectHistory.delete",
        "batch.apply",
    };

    /// <summary>
    /// Methods that write to the KNX bus / physical devices. The AddIn refuses them with
    /// approval_required while its "Bus read-only" preference is on (Host/BusWritePolicy.cs).
    /// </summary>
    public static readonly IReadOnlySet<string> BusWrites = new HashSet<string>(StringComparer.Ordinal)
    {
        "device.program", "firmware.update", "group.write",
        "device.unload", "device.reset", "bus.setIndividualAddress",
    };
}
