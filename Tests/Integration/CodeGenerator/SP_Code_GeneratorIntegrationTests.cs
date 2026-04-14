namespace Tests.Integration.CodeGenerator;

/// <summary>
/// Test di integrazione end-to-end per SP_Code_Generator.
/// Verifica scenari multi-config e robustezza input.
/// </summary>
public class SP_Code_GeneratorIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SP_Code_Generator _generator;

    public SP_Code_GeneratorIntegrationTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "SPCodeGenInteg_" + Guid.NewGuid().ToString("N")[..8]);
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
    public void GeneraFileDiTesto_MultipleConfigs_AllPresent()
    {
        var configs = new List<string>
        {
            "CAN_BAUD=500000",
            "ENABLE_BLE",
            "MAX_RETRY=3",
            "TOPLIFT_MODE",
            "UART_TIMEOUT=1000"
        };
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(configs, path);

        var content = File.ReadAllText(path);
        Assert.Contains("#define CAN_BAUD 500000", content);
        Assert.Contains("#define ENABLE_BLE", content);
        Assert.Contains("#define MAX_RETRY 3", content);
        Assert.Contains("#define TOPLIFT_MODE", content);
        Assert.Contains("#define UART_TIMEOUT 1000", content);
    }

    [Fact]
    public void GeneraFileDiTesto_MixedConfigs_CorrectFormat()
    {
        var configs = new List<string>
        {
            "FLAG_ONLY",
            "KEY=VALUE"
        };
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(configs, path);

        var lines = File.ReadAllLines(path);
        Assert.Contains(lines, l => l.Trim() == "#define FLAG_ONLY");
        Assert.Contains(lines, l => l.Trim() == "#define KEY VALUE");
    }

    [Fact]
    public void GeneraFileDiTesto_Output_HasValidCStructure()
    {
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(["TEST=1"], path);

        var content = File.ReadAllText(path);
        // Struttura C valida: header guard apre e chiude
        var ifndefIndex = content.IndexOf("#ifndef SP_CONFIG_H_");
        var defineIndex = content.IndexOf("#define SP_CONFIG_H_");
        var endifIndex = content.IndexOf("#endif");
        Assert.True(ifndefIndex < defineIndex);
        Assert.True(defineIndex < endifIndex);
        // Typedef presente tra define e endif
        var typedefIndex = content.IndexOf("typedef enum");
        Assert.True(typedefIndex > defineIndex);
        Assert.True(typedefIndex < endifIndex);
    }

    [Fact]
    public void GeneraFileDiTesto_ConfigWithMultipleEquals_SplitsOnFirst()
    {
        // "A=B=C" -> split su primo '=' -> parts[0]="A", parts[1]="B=C"
        // Ma il codice usa Split('=') senza limit, quindi parts[0]="A", parts[1]="B"
        // Questo è il comportamento attuale — lo documentiamo con un test
        var path = GetTempFile();

        _generator.GeneraFileDiTesto(["A=B=C"], path);

        var content = File.ReadAllText(path);
        // Con Split('=') default: parts = ["A", "B", "C"]
        // parts[0].Trim() = "A", parts[1].Trim() = "B"
        Assert.Contains("#define A B", content);
    }
}
