using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using static ExcelHandler;

public class ExcelHandler
{

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

        public string ToTerminal()
        {
            return $"Macchina: {Macchina}, Scheda: {Scheda}, Indirizzo: {Indirizzo}";
        }
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

        public string ToTerminal()
        {
            return $"Comando: {Name}, codeH: {CmdH}, codeL: {CmdL}";
        }
    }

    public void EstraiDatiProtocollo(List<RowData> IndirizziProtocollo, List<CommandData> Commandi, string filePath)
    {
        IndirizziProtocollo.Clear();
        Commandi.Clear();
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("indirizzo protocollo stem");

                // Scorri le righe del foglio, iniziando da 2 (se la prima riga contiene intestazioni)
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    // Leggi i dati delle colonne A, C e G
                    var macchina = row.Cell("A").GetValue<string>();
                    var scheda = row.Cell("C").GetValue<string>();
                    var indirizzo = row.Cell("G").GetValue<string>();

                    // Aggiungi alla lista solo se tutti i campi hanno un valore
                    if (!string.IsNullOrWhiteSpace(macchina) &&
                        !string.IsNullOrWhiteSpace(scheda) &&
                        !string.IsNullOrWhiteSpace(indirizzo))
                    {
                        // Aggiungi un oggetto RowData alla lista
                        IndirizziProtocollo.Add(new RowData(macchina, scheda, indirizzo));
                    }
                }

                worksheet = workbook.Worksheet("COMANDI");

                // Scorri le righe del foglio, iniziando da 2 (se la prima riga contiene intestazioni)
                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    // Leggi i dati delle colonne A, B e C
                    var name = row.Cell("A").GetValue<string>();
                    var cmdH = row.Cell("B").GetValue<string>();
                    var cmdL = row.Cell("C").GetValue<string>();

                    // Aggiungi alla lista solo se tutti i campi hanno un valore
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.IsNullOrWhiteSpace(cmdH) &&
                        !string.IsNullOrWhiteSpace(cmdL))
                    {
                        // Aggiungi un oggetto RowData alla lista
                        Commandi.Add(new CommandData(name, cmdH, cmdL));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nell'apertura file excel: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
       
            Application.Exit(); // Chiude l'applicazione
            Environment.Exit(0); // Termina il processo
        }   
    }
}
