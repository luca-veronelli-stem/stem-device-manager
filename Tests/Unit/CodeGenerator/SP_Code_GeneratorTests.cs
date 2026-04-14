namespace Tests.Unit.CodeGenerator;

/// <summary>
/// Test per SP_Code_Generator.
/// Verifica generazione file header C con #define, header guard e typedef.
/// Usa file temporanei con cleanup.
/// </summary>
public class SP_Code_GeneratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SP_Code_Generator _generator;

    public SP_Code_GeneratorTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "SPCodeGenTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _generator = new SP_Code_Generator();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string GetTempFile() =>
        Path.Combine(_tempDir, "sp_config.h");

    [Fact]
    public void GeneraFileDiTesto_CreatesFile()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto([], path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void GeneraFileDiTesto_ContainsHeaderGuard()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto([], path);

        var content = File.ReadAllText(path);
        Assert.Contains("#ifndef SP_CONFIG_H_", content);
        Assert.Contains("#define SP_CONFIG_H_", content);
        Assert.Contains("#endif /* SP_CONFIG_H_ */", content);
    }

    [Fact]
    public void GeneraFileDiTesto_ConfigWithEquals_WritesDefineWithValue()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(["MAX_NODES=16"], path);

        var content = File.ReadAllText(path);
        Assert.Contains("#define MAX_NODES 16", content);
    }

    [Fact]
    public void GeneraFileDiTesto_ConfigWithoutEquals_WritesDefineFlag()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(["ENABLE_CAN"], path);

        var content = File.ReadAllText(path);
        Assert.Contains("#define ENABLE_CAN", content);
    }

    [Fact]
    public void GeneraFileDiTesto_OverwritesExistingFile()
    {
        var path = GetTempFile();
        File.WriteAllText(path, "vecchio contenuto");

        _generator.GeneraFileDiTesto(["NEW_FLAG"], path);

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("vecchio contenuto", content);
        Assert.Contains("#define NEW_FLAG", content);
    }

    [Fact]
    public void GeneraFileDiTesto_EmptyList_WritesStructureOnly()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto([], path);

        var content = File.ReadAllText(path);
        Assert.Contains("#ifndef SP_CONFIG_H_", content);
        Assert.Contains("RouterChannel_t", content);
        Assert.DoesNotContain("#define \r\n", content);
    }

    [Fact]
    public void GeneraFileDiTesto_ContainsTypedef()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto([], path);

        var content = File.ReadAllText(path);
        Assert.Contains("typedef enum", content);
        Assert.Contains("BLE_SERIAL = 0", content);
        Assert.Contains("CAN_BUS_1", content);
        Assert.Contains("CAN_BUS_2", content);
        Assert.Contains("RouterChannel_t", content);
    }
}
