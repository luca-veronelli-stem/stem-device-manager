using ClosedXML.Excel;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Excel;

/// <summary>
/// Implementazione di IDictionaryProvider che legge i dati dal file Excel dei dizionari STEM.
/// Replica la logica di lettura di ExcelHandler (App/) ma produce Core.Models.*
/// senza dipendenze WinForms. Accetta uno Stream (risorsa embedded o file).
/// </summary>
public class ExcelDictionaryProvider : IDictionaryProvider
{
    private readonly MemoryStream _excelBuffer;

    public ExcelDictionaryProvider(Stream excelStream)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        _excelBuffer = new MemoryStream();
        excelStream.CopyTo(_excelBuffer);
        _excelBuffer.Position = 0;
    }

    public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _excelBuffer.Position = 0;
        using var workbook = new XLWorkbook(_excelBuffer);

        var addresses = ReadProtocolAddresses(workbook);
        var commands = ReadCommands(workbook);

        return Task.FromResult(new DictionaryData(addresses, commands));
    }

    public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _excelBuffer.Position = 0;
        using var workbook = new XLWorkbook(_excelBuffer);

        var variables = ReadVariables(workbook, recipientId);

        return Task.FromResult<IReadOnlyList<Variable>>(variables);
    }

    private static List<ProtocolAddress> ReadProtocolAddresses(XLWorkbook workbook)
    {
        var result = new List<ProtocolAddress>();
        var worksheet = workbook.Worksheet("indirizzo protocollo stem");

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var device = row.Cell("A").GetValue<string>();
            var board = row.Cell("C").GetValue<string>();
            var address = row.Cell("G").GetValue<string>();

            if (!string.IsNullOrWhiteSpace(device) &&
                !string.IsNullOrWhiteSpace(board) &&
                !string.IsNullOrWhiteSpace(address))
            {
                result.Add(new ProtocolAddress(device, board, address));
            }
        }

        return result;
    }

    private static List<Command> ReadCommands(XLWorkbook workbook)
    {
        var result = new List<Command>();
        var worksheet = workbook.Worksheet("COMANDI");

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var name = row.Cell("A").GetValue<string>();
            var codeHigh = row.Cell("B").GetValue<string>();
            var codeLow = row.Cell("C").GetValue<string>();

            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(codeHigh) &&
                !string.IsNullOrWhiteSpace(codeLow))
            {
                result.Add(new Command(name, codeHigh, codeLow));
            }
        }

        return result;
    }

    private static List<Variable> ReadVariables(XLWorkbook workbook, uint recipientId)
    {
        var result = new List<Variable>();

        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var cell in worksheet.Row(2).CellsUsed())
            {
                var cellStr = cell.GetString();
                if (cellStr.Length <= 2) continue;

                if (!int.TryParse(
                        cellStr.AsSpan(2),
                        System.Globalization.NumberStyles.HexNumber,
                        null,
                        out int cellValue))
                    continue;

                if (cellValue != recipientId) continue;

                // Match found — extract highlighted variables (skip 4 header rows)
                foreach (var row in worksheet.RowsUsed().Skip(4))
                {
                    var fillColor = row.Cell("A").Style.Fill.BackgroundColor;
                    if (fillColor.ColorType == XLColorType.Theme) continue;
                    if (fillColor.Color.ToArgb() != -7155632) continue;

                    var name = row.Cell("A").GetValue<string>();
                    var addrHigh = row.Cell("B").GetValue<string>();
                    var addrLow = row.Cell("C").GetValue<string>();
                    var dataType = row.Cell("D").GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.IsNullOrWhiteSpace(addrHigh) &&
                        !string.IsNullOrWhiteSpace(addrLow))
                    {
                        result.Add(new Variable(name, addrHigh, addrLow, dataType));
                    }
                }

                return result;
            }
        }

        return result;
    }
}
