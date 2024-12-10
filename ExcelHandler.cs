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

    public void EstraiIndirizziProtocollo(List<RowData> IndirizziProtocollo, string filePath)
    {
        string sheetName = "indirizzo protocollo stem";
           
        IndirizziProtocollo.Clear();

        using (var workbook = new XLWorkbook(filePath))
        {
            var worksheet = workbook.Worksheet(sheetName);

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
        }
    }
}
