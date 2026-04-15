using ClosedXML.Excel;
using Core.Models;
using System.Globalization;

/// <summary>
/// Adattatore Excel legacy — legge il file Dizionari STEM.xlsx e restituisce Core.Models.
/// Sarà rimosso nella Fase 4 quando tutti i consumer useranno IDictionaryProvider.
/// </summary>
public class ExcelHandler
{
    private readonly MemoryStream _excelBuffer;

    public ExcelHandler() { }

    /// <summary>
    /// Costruttore per risorsa embedded via Stream.
    /// Copia lo stream in un MemoryStream per permettere letture multiple.
    /// </summary>
    public ExcelHandler(Stream excelStream)
    {
        if (excelStream == null)
            throw new ArgumentNullException(nameof(excelStream));

        _excelBuffer = new MemoryStream();
        excelStream.CopyTo(_excelBuffer);
        _excelBuffer.Position = 0;
    }

    /// <summary>
    /// Estrae indirizzi protocollo e comandi da file esterno.
    /// </summary>
    public void EstraiDatiProtocollo(List<ProtocolAddress> indirizzi, List<Command> comandi, string filePath)
    {
        indirizzi.Clear();
        comandi.Clear();
        try
        {
            using var workbook = new XLWorkbook(filePath);
            ExtractAddresses(indirizzi, workbook);
            ExtractCommands(comandi, workbook);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura file excel: {ex.Message}", "Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// Estrae indirizzi protocollo e comandi da risorsa embedded (stream).
    /// </summary>
    public void EstraiDatiProtocollo(List<ProtocolAddress> indirizzi, List<Command> comandi, List<Variable> dizionario)
    {
        if (_excelBuffer == null)
            throw new InvalidOperationException("Stream non inizializzato: utilizzare il costruttore ExcelHandler(Stream)");

        indirizzi.Clear();
        comandi.Clear();
        dizionario.Clear();

        try
        {
            if (_excelBuffer.CanSeek)
                _excelBuffer.Seek(0, SeekOrigin.Begin);

            using var workbook = new XLWorkbook(_excelBuffer);
            ExtractAddresses(indirizzi, workbook);
            ExtractCommands(comandi, workbook);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura stream excel: {ex.Message}", "Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// Estrae il dizionario variabili per RecipientId da file esterno.
    /// </summary>
    public void EstraiDizionario(uint recipientId, List<Variable> variabili, string filePath)
    {
        variabili.Clear();
        try
        {
            using var workbook = new XLWorkbook(filePath);
            ProcessWorksheetsForDictionary(recipientId, variabili, workbook);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura file excel: {ex.Message}", "Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Estrae il dizionario variabili per RecipientId da risorsa embedded.
    /// </summary>
    public void EstraiDizionario(uint recipientId, List<Variable> variabili)
    {
        if (_excelBuffer == null)
            throw new InvalidOperationException("Stream non inizializzato: utilizzare il costruttore ExcelHandler(Stream)");

        variabili.Clear();
        try
        {
            _excelBuffer.Position = 0;
            using var workbook = new XLWorkbook(_excelBuffer);
            ProcessWorksheetsForDictionary(recipientId, variabili, workbook);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura stream excel dizionario: {ex.Message}", "Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractAddresses(List<ProtocolAddress> indirizzi, XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet("indirizzo protocollo stem");
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var deviceName = row.Cell("A").GetValue<string>();
            var boardName  = row.Cell("C").GetValue<string>();
            var address    = row.Cell("G").GetValue<string>();
            if (!string.IsNullOrWhiteSpace(deviceName) && !string.IsNullOrWhiteSpace(boardName) && !string.IsNullOrWhiteSpace(address))
                indirizzi.Add(new ProtocolAddress(deviceName, boardName, address));
        }
    }

    private static void ExtractCommands(List<Command> comandi, XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet("COMANDI");
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var name  = row.Cell("A").GetValue<string>();
            var cmdH  = row.Cell("B").GetValue<string>();
            var cmdL  = row.Cell("C").GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(cmdH) && !string.IsNullOrWhiteSpace(cmdL))
                comandi.Add(new Command(name, cmdH, cmdL));
        }
    }

    private static void ProcessWorksheetsForDictionary(uint recipientId, List<Variable> variabili, XLWorkbook workbook)
    {
        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var cell in worksheet.Row(2).CellsUsed())
            {
                var cellStr = cell.GetString();
                if (cellStr.Length <= 2) continue;
                if (!int.TryParse(cellStr.Substring(2), NumberStyles.HexNumber, null, out int cellValue)) continue;
                if (cellValue != recipientId) continue;

                foreach (var rowTemp in worksheet.RowsUsed().Skip(4))
                {
                    var fillColor = rowTemp.Cell("A").Style.Fill.BackgroundColor;
                    if (fillColor.ColorType != XLColorType.Theme && fillColor.Color.ToArgb() == -7155632)
                    {
                        var name     = rowTemp.Cell("A").GetValue<string>();
                        var addrH    = rowTemp.Cell("B").GetValue<string>();
                        var addrL    = rowTemp.Cell("C").GetValue<string>();
                        var dataType = rowTemp.Cell("D").GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(addrH) && !string.IsNullOrWhiteSpace(addrL))
                            variabili.Add(new Variable(name, addrH, addrL, dataType));
                    }
                }
                return;
            }
        }
    }
}
