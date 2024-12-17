using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using StemPC;
using Stem_Protocol;
using Stem_Protocol.PacketManager;
using System.Runtime.InteropServices;

// Classe per l'interfaccia grafica del bootloader
public class Boot_Interface_Tab : TabPage
{
    private TextBox textBoxFilePath;
    private Button buttonOpenFile;
    private RichTextBox richTextBoxHex;
    private RichTextBox richTextBoxAscii;
    private SplitContainer splitContainer;

    public Boot_Interface_Tab()
    {
        // Inizializzazione controlli (migliorata per robustezza)
        textBoxFilePath = new TextBox { Dock = DockStyle.Top, ReadOnly = true };
        buttonOpenFile = new Button { Dock = DockStyle.Top, Text = "Seleziona File Binario" };

        splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        richTextBoxHex = new RichTextBox { Dock = DockStyle.Fill, Font = new Font("Courier New", 10), WordWrap = false, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical }; // Forza le scrollbar verticali
        richTextBoxAscii = new RichTextBox { Dock = DockStyle.Fill, Font = new Font("Courier New", 10), WordWrap = false, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical }; // Forza le scrollbar verticali


        splitContainer.Panel1.Controls.Add(richTextBoxHex);
        splitContainer.Panel2.Controls.Add(richTextBoxAscii);

        splitContainer.SplitterDistance = splitContainer.Width * 70 / 100;

        // Gestione eventi di Scroll (CORRETTA E ROBUSTA)
        richTextBoxHex.VScroll += RichTextBox_VScroll;
        richTextBoxAscii.VScroll += RichTextBox_VScroll;


        this.Controls.AddRange(new Control[] { splitContainer, buttonOpenFile, textBoxFilePath });

        buttonOpenFile.Click += ButtonOpenFile_Click;

        this.Text = "Boot Interface";
    }

    private void ButtonOpenFile_Click(object sender, EventArgs e)
    {
        // ... (codice per aprire il file)
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "File Binari (*.bin)|*.bin|Tutti i File (*.*)|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                textBoxFilePath.Text = filePath;
                VisualizzaEsadecimale(filePath);
            }
        }
    }

    private void VisualizzaEsadecimale(string filePath)
    {
        // ... (codice per visualizzare l'esadecimale)
        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            StringBuilder hexStringBuilder = new StringBuilder();
            StringBuilder asciiStringBuilder = new StringBuilder();

            for (int i = 0; i < fileBytes.Length; i++)
            {
                hexStringBuilder.Append(fileBytes[i].ToString("X2") + " ");
                char asciiChar = (fileBytes[i] >= 32 && fileBytes[i] <= 126) ? (char)fileBytes[i] : '.';
                asciiStringBuilder.Append(asciiChar);

                if ((i + 1) % 16 == 0)
                {
                    hexStringBuilder.AppendLine();
                    asciiStringBuilder.AppendLine();
                }
            }

            richTextBoxHex.Text = hexStringBuilder.ToString();
            richTextBoxAscii.Text = asciiStringBuilder.ToString();

        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore durante la lettura del file: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RichTextBox_VScroll(object sender, EventArgs e)
    {
        if (sender is RichTextBox source && source.Visible)
        {
            RichTextBox target = (source == richTextBoxHex) ? richTextBoxAscii : richTextBoxHex;

            if (target.Visible)
            {
                // Ottieni la posizione del primo carattere visibile nel RichTextBox sorgente
                int firstVisibleCharIndex = source.GetCharIndexFromPosition(new Point(0, 0)); // L'origine (0,0) rappresenta l'angolo superiore sinistro dell'area client visibile.

                // Calcola la posizione corrispondente nel RichTextBox target
                if (firstVisibleCharIndex >= 0 && firstVisibleCharIndex < target.TextLength)
                {
                    try
                    {
                        Point targetPoint = target.GetPositionFromCharIndex(firstVisibleCharIndex);
                        target.Select(firstVisibleCharIndex, 0);
                        target.ScrollToCaret();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Eccezione durante la sincronizzazione: {ex.Message}");
                    }

                }
                else
                {
                    target.ScrollToCaret();
                }
            }
        }
    }
}