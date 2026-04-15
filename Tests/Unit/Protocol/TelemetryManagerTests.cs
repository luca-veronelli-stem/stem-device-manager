using App.STEMProtocol;
using Core.Models;

namespace Tests.Unit.Protocol;

/// <summary>
/// Test unitari per TelemetryManager — gestione dizionario variabili con Core.Models.Variable.
/// </summary>
public class TelemetryManagerTests
{
    private TelemetryManager CreateManager() =>
        new TelemetryManager(new PacketManager(0xFFFFFFFF));

    private static Variable MakeVar(string name, string addrH = "80", string addrL = "01", string dataType = "uint16_t") =>
        new Variable(name, addrH, addrL, dataType);

    // -----------------------------------------------------------------------
    // AddToDictionary
    // -----------------------------------------------------------------------

    [Fact]
    public void AddToDictionary_SingleVariable_IncreasesDictionaryCount()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("Velocità"));
        Assert.Equal("Velocità", mgr.GetVariableName(0));
    }

    [Fact]
    public void AddToDictionary_MultipleVariables_PreservesOrder()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("A"));
        mgr.AddToDictionary(MakeVar("B"));
        mgr.AddToDictionary(MakeVar("C"));
        Assert.Equal("A", mgr.GetVariableName(0));
        Assert.Equal("B", mgr.GetVariableName(1));
        Assert.Equal("C", mgr.GetVariableName(2));
    }

    // -----------------------------------------------------------------------
    // AddToDictionaryForWrite
    // -----------------------------------------------------------------------

    [Fact]
    public void AddToDictionaryForWrite_Variable_IsAccessibleByIndex()
    {
        var mgr = CreateManager();
        mgr.AddToDictionaryForWrite(MakeVar("Soglia"), "42");
        Assert.Equal("Soglia", mgr.GetVariableName(0));
    }

    // -----------------------------------------------------------------------
    // GetVariableName
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVariableName_IndexOutOfRange_ReturnsErrorString()
    {
        var mgr = CreateManager();
        Assert.Equal("Index out of range", mgr.GetVariableName(0));
        Assert.Equal("Index out of range", mgr.GetVariableName(-1));
    }

    // -----------------------------------------------------------------------
    // GetVariableIndex
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVariableIndex_ExistingName_ReturnsCorrectIndex()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("Alpha"));
        mgr.AddToDictionary(MakeVar("Beta"));
        Assert.Equal(1, mgr.GetVariableIndex("Beta"));
    }

    [Fact]
    public void GetVariableIndex_MissingName_ReturnsMinusOne()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("Alpha"));
        Assert.Equal(-1, mgr.GetVariableIndex("NonEsiste"));
    }

    // -----------------------------------------------------------------------
    // RemoveFromDictionary
    // -----------------------------------------------------------------------

    [Fact]
    public void RemoveFromDictionary_MiddleElement_ShiftsRemainingElements()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("A"));
        mgr.AddToDictionary(MakeVar("B"));
        mgr.AddToDictionary(MakeVar("C"));
        mgr.RemoveFromDictionary(1); // rimuovi "B"
        Assert.Equal("A", mgr.GetVariableName(0));
        Assert.Equal("C", mgr.GetVariableName(1));
        Assert.Equal("Index out of range", mgr.GetVariableName(2));
    }

    // -----------------------------------------------------------------------
    // ResetDictionary
    // -----------------------------------------------------------------------

    [Fact]
    public void ResetDictionary_AfterAdds_DictionaryIsEmpty()
    {
        var mgr = CreateManager();
        mgr.AddToDictionary(MakeVar("X"));
        mgr.AddToDictionary(MakeVar("Y"));
        mgr.ResetDictionary();
        Assert.Equal("Index out of range", mgr.GetVariableName(0));
    }
}
