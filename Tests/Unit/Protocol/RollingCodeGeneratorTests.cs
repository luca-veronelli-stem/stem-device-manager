using App.STEMProtocol;

namespace Tests.Unit.Protocol;

/// <summary>
/// Test per RollingCodeGenerator.
/// Verifica range [1..7], ciclicità e thread-safety.
/// NOTA: classe statica con stato condiviso — i test sono serializzati
/// via [Collection] per evitare interferenze.
/// </summary>
[Collection("RollingCode")]
public class RollingCodeGeneratorTests
{
    [Fact]
    public void GetIndex_ReturnsValueBetween1And7()
    {
        byte index = RollingCodeGenerator.GetIndex();

        Assert.InRange(index, 1, 7);
    }

    [Fact]
    public void GetIndex_14Calls_AllValuesInRange()
    {
        // 14 chiamate = 2 cicli completi, tutti devono restare in range
        for (int i = 0; i < 14; i++)
        {
            byte index = RollingCodeGenerator.GetIndex();
            Assert.InRange(index, 1, 7);
        }
    }

    [Fact]
    public void GetIndex_7Calls_ProducesAllValuesFrom1To7()
    {
        var values = new HashSet<byte>();

        for (int i = 0; i < 7; i++)
            values.Add(RollingCodeGenerator.GetIndex());

        // Dopo 7 chiamate, tutti i valori 1-7 devono essere apparsi
        Assert.Equal(7, values.Count);
        for (byte expected = 1; expected <= 7; expected++)
            Assert.Contains(expected, values);
    }

    [Fact]
    public void GetIndex_ConcurrentCalls_AllValuesInRange()
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<byte>();

        Parallel.For(0, 100, _ =>
        {
            results.Add(RollingCodeGenerator.GetIndex());
        });

        Assert.Equal(100, results.Count);
        Assert.All(results, index => Assert.InRange(index, 1, 7));
    }
}
