using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using static ExcelHandler;

public class ExcelHandler
{
    // Internal stream for embedded resources
    private readonly Stream _excelStream;

    // Default constructor for file-based usage
    public ExcelHandler() { }

    // Overload constructor for stream-based usage
    public ExcelHandler(Stream excelStream)
    {
        _excelStream = excelStream ?? throw new ArgumentNullException(nameof(excelStream));
    }

    // Definizione del tipo di dato personalizzato
    public class RowData
    {
        public string Macchina { get; set; }
        public string Scheda { get; set; }
        public string Indirizzo { get; set; }

        public RowData(string macchina, string scheda, string indirizzo)
        {
            Macchina = macchina;
            Scheda = scheda;
            Indirizzo = indirizzo;
        }

        public string ToTerminal() => $"Macchina: {Macchina}, Scheda: {Scheda}, Indirizzo: {Indirizzo}";
    }

    public class CommandData
    {
        public string Name { get; set; }
        public string CmdH { get; set; }
        public string CmdL { get; set; }

        public CommandData(string name, string cmdH, string cmdL)
        {
            Name = name;
            CmdH = cmdH;
            CmdL = cmdL;
        }

        public string ToTerminal() => $"Comando: {Name}, codeH: {CmdH}, codeL: {CmdL}";
    }

    public class VariableData
    {
        public string Name { get; set; }
        public string AddrH { get; set; }
        public string AddrL { get; set; }
        public string DataType { get; set; }

        public VariableData(string name, string addrH, string addrL, string dataType)
        {
            Name = name;
            AddrH = addrH;
            AddrL = addrL;
            DataType = dataType;
        }

        public string ToTerminal() => $"Variabile logica: {Name}, addrH: {AddrH}, addrL: {AddrL}";
    }

    /// <summary>
    /// Estrae dati protocollo (indirizzi e comandi) da un file esterno
    /// </summary>
    public void EstraiDatiProtocollo(List<RowData> IndirizziProtocollo, List<CommandData> Comandi, string filePath)
    {
        IndirizziProtocollo.Clear();
        Comandi.Clear();
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                // Estrazione indirizzi protocollo
                var worksheet = workbook.Worksheet("indirizzo protocollo stem");
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    var macchina = row.Cell("A").GetValue<string>();
                    var scheda = row.Cell("C").GetValue<string>();
                    var indirizzo = row.Cell("G").GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(macchina) && !string.IsNullOrWhiteSpace(scheda) && !string.IsNullOrWhiteSpace(indirizzo))
                        IndirizziProtocollo.Add(new RowData(macchina, scheda, indirizzo));
                }

                // Estrazione comandi protocollo
                worksheet = workbook.Worksheet("COMANDI");
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    var name = row.Cell("A").GetValue<string>();
                    var cmdH = row.Cell("B").GetValue<string>();
                    var cmdL = row.Cell("C").GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(cmdH) && !string.IsNullOrWhiteSpace(cmdL))
                        Comandi.Add(new CommandData(name, cmdH, cmdL));
                }
            }
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
    /// Estrae dati protocollo (indirizzi, comandi) direttamente da uno stream (risorsa embedded)
    /// </summary>
    public void EstraiDatiProtocollo(List<RowData> IndirizziProtocollo, List<CommandData> Comandi, List<VariableData> Dizionario)
    {
        if (_excelStream == null)
            throw new InvalidOperationException("Stream non inizializzato: utilizzare il costruttore ExcelHandler(Stream)");

        IndirizziProtocollo.Clear();
        Comandi.Clear();
        Dizionario.Clear(); // al momento non popolato, estendere se necessario

        try
        {
            // Assicura la posizione all'inizio dello stream
            if (_excelStream.CanSeek)
                _excelStream.Seek(0, SeekOrigin.Begin);

            using (var workbook = new XLWorkbook(_excelStream))
            {
                // Estrarre indirizzi e comandi come sopra
                var worksheet = workbook.Worksheet("indirizzo protocollo stem");
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    var macchina = row.Cell("A").GetValue<string>();
                    var scheda = row.Cell("C").GetValue<string>();
                    var indirizzo = row.Cell("G").GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(macchina) && !string.IsNullOrWhiteSpace(scheda) && !string.IsNullOrWhiteSpace(indirizzo))
                        IndirizziProtocollo.Add(new RowData(macchina, scheda, indirizzo));
                }
                worksheet = workbook.Worksheet("COMANDI");
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    var name = row.Cell("A").GetValue<string>();
                    var cmdH = row.Cell("B").GetValue<string>();
                    var cmdL = row.Cell("C").GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(cmdH) && !string.IsNullOrWhiteSpace(cmdL))
                        Comandi.Add(new CommandData(name, cmdH, cmdL));
                }

                // TODO: se serve, implementare l'estrazione delle variabili nel Dizionario usando logic simile a EstraiDizionario
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura stream excel: {ex.Message}", "Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }
    }


    public void EstraiDizionario(uint RecipientId, List<VariableData> Variabili, string filePath)
    {
        Variabili.Clear();

        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
                // Estrazione dizionario macchine
                //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
                foreach (var worksheet in workbook.Worksheets)
                {
                    var row = worksheet.Row(2); // Cerca nella seconda riga se trovi un indirizzo corrispondente a RecipientId

                    foreach (var cell in row.CellsUsed())
                    {
                        int CellValue;
                        string CellValueString = cell.GetString();

                        if (CellValueString.Length > 2){ 

                            int.TryParse(CellValueString.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out CellValue);

                            if (CellValue == RecipientId)
                            {
                                //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
                                // Estrazione dizionario
                                //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
                                // Scorri le righe del foglio, iniziando da 5 (esclude tutte le intestazioni)
                                foreach (var rowtemp in worksheet.RowsUsed().Skip(4))
                                {
                                    // Ottieni il colore di sfondo della cella
                                    XLColor fillColor = rowtemp.Cell("A").Style.Fill.BackgroundColor;
                                    if (fillColor.ColorType!= XLColorType.Theme)
                                    {
                                        var rgb = fillColor.Color.ToArgb();
                                        //if (rgb == System.Drawing.Color.LightGreen.ToArgb())
                                        if (rgb == -7155632)
                                        {
                                            // Leggi i dati delle colonne A, B e C
                                            var name = rowtemp.Cell("A").GetValue<string>();
                                            var addrH = rowtemp.Cell("B").GetValue<string>();
                                            var addrL = rowtemp.Cell("C").GetValue<string>();
                                            var dataType = rowtemp.Cell("D").GetValue<string>();

                                            // Aggiungi alla lista solo se tutti i campi hanno un valore
                                            if (!string.IsNullOrWhiteSpace(name) &&
                                                !string.IsNullOrWhiteSpace(addrH) &&
                                                !string.IsNullOrWhiteSpace(addrL)) 
                                            {
                                                // Aggiungi un oggetto RowData alla lista
                                                Variabili.Add(new VariableData(name, addrH, addrL, dataType));
                                            }
                                        }
                                    }
                                }
                            //    MessageBox.Show($"Indirizzo trovato nel foglio: {worksheet.Name}, cella: {cell.Address}", "", MessageBoxButtons.OK);
                            }
                            else
                            {
                                //MessageBox.Show("Indirizzo non trovato", "", MessageBoxButtons.OK);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura file excel: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);

            //Application.Exit(); // Chiude l'applicazione
            //Environment.Exit(0); // Termina il processo
        }


    }
}


