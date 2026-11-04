using System.Text.Json.Nodes;
using KnxEtsMcp.Adapters.Bridge;
using KnxEtsMcp.Adapters.Transport;
using KnxEtsMcp.Tests.Support;

namespace KnxEtsMcp.Tests;

/// <summary>
/// project.undo / project.redo behaviour against the mock AddIn. An undo must actually
/// revert the last project mutation (not just bump the revision), a single undo reverts a
/// whole batch, and undoing/redoing with an empty stack is an explicit error that leaves the
/// revision untouched. Regression cover for the v0.4.0 "undo reports success without undoing"
/// defect.
/// </summary>
public sealed class UndoRedoTests
{
    private static async Task<List<string>> NamesAsync(KnxBridgeClient bridge)
    {
        var result = await bridge.RequestAsync("ga.list");
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ArrB().Select(g => g.ObjB()["name"].StrB()).ToList();
    }

    private static async Task CreateGaAsync(KnxBridgeClient bridge, string name, string address)
    {
        var result = await bridge.RequestAsync("ga.create", new JsonObject { ["name"] = name, ["address"] = address });
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    private static JsonObject CreateOp(string name, string address)
        => new() { ["method"] = "ga.create", ["params"] = new JsonObject { ["name"] = name, ["address"] = address } };

    [Fact]
    public async Task UndoRemovesCreatedGa_RedoRestoresIt()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        Assert.DoesNotContain("Essai", await NamesAsync(bridge));
        await CreateGaAsync(bridge, "Essai", "1/0/9");
        Assert.Contains("Essai", await NamesAsync(bridge));

        var undo = await bridge.RequestAsync("project.undo");
        Assert.True(undo.IsSuccess, undo.Error?.ToString());
        Assert.DoesNotContain("Essai", await NamesAsync(bridge));

        var redo = await bridge.RequestAsync("project.redo");
        Assert.True(redo.IsSuccess, redo.Error?.ToString());
        Assert.Contains("Essai", await NamesAsync(bridge));
    }

    [Fact]
    public async Task UndoWithEmptyStackErrorsAndDoesNotChangeRevision()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        var revisionBefore = mock.Revision;
        var result = await bridge.RequestAsync("project.undo");

        Assert.False(result.IsSuccess, "undo with an empty stack must fail, not return ok");
        Assert.Contains("nothing to undo", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(revisionBefore, mock.Revision);
    }

    [Fact]
    public async Task RedoWithEmptyStackErrorsAndDoesNotChangeRevision()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        var revisionBefore = mock.Revision;
        var result = await bridge.RequestAsync("project.redo");

        Assert.False(result.IsSuccess, "redo with an empty stack must fail, not return ok");
        Assert.Contains("nothing to redo", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(revisionBefore, mock.Revision);
    }

    [Fact]
    public async Task UndoAdvancesTheRevisionOnlyWhenItRevertsSomething()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        await CreateGaAsync(bridge, "Essai", "1/0/9");
        var revisionAfterCreate = mock.Revision;

        var undo = await bridge.RequestAsync("project.undo");
        Assert.True(undo.IsSuccess, undo.Error?.ToString());
        Assert.NotEqual(revisionAfterCreate, mock.Revision); // a real undo is itself a change
    }

    [Fact]
    public async Task SingleUndoRevertsAWholeBatch()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        var before = (await NamesAsync(bridge)).Count;
        var operations = new JsonArray(CreateOp("U1", "8/0/1"), CreateOp("U2", "8/0/2"));
        var batch = await bridge.RequestAsync("batch.apply", new JsonObject
        {
            ["operations"] = operations,
            ["atomic"] = true,
        });
        Assert.True(batch.IsSuccess, batch.Error?.ToString());
        Assert.Equal(before + 2, (await NamesAsync(bridge)).Count);

        var undo = await bridge.RequestAsync("project.undo");
        Assert.True(undo.IsSuccess, undo.Error?.ToString());

        var names = await NamesAsync(bridge);
        Assert.Equal(before, names.Count); // one undo removed both
        Assert.DoesNotContain("U1", names);
        Assert.DoesNotContain("U2", names);
    }

    [Fact]
    public async Task RolledBackBatchLeavesNothingToUndo()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        var before = (await NamesAsync(bridge)).Count;
        // Second op duplicates seed address 1/0/1 -> atomic rollback.
        var operations = new JsonArray(CreateOp("R1", "8/1/1"), CreateOp("Dup", "1/0/1"));
        var batch = await bridge.RequestAsync("batch.apply", new JsonObject
        {
            ["operations"] = operations,
            ["atomic"] = true,
        });
        Assert.True(batch.IsSuccess, batch.Error?.ToString());
        batch.Value.ObjB()["applied"].IsFalseB();
        batch.Value.ObjB()["rolledBack"].IsTrueB();
        Assert.Equal(before, (await NamesAsync(bridge)).Count);

        var undo = await bridge.RequestAsync("project.undo");
        Assert.False(undo.IsSuccess, "a fully rolled-back batch must not leave an undo entry");
    }

    [Fact]
    public async Task UndoReturnsToExactlyTheSeedStateAndStopsAtTheStackBottom()
    {
        var mock = new MockTransport();
        await using var bridge = new KnxBridgeClient(mock);

        var seed = (await NamesAsync(bridge)).OrderBy(n => n).ToList();
        await CreateGaAsync(bridge, "Temp", "8/2/1");

        var undo = await bridge.RequestAsync("project.undo");
        Assert.True(undo.IsSuccess, undo.Error?.ToString());
        Assert.Equal(seed, (await NamesAsync(bridge)).OrderBy(n => n).ToList());

        // Only the one user mutation was checkpointed; the seed itself is not undoable.
        var again = await bridge.RequestAsync("project.undo");
        Assert.False(again.IsSuccess, "nothing left to undo once the user's change is reverted");
    }
}
