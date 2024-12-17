using System;
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
    private TextBox txtFilePath;
    private Button btnSelectFile;
    private Button btnStartProcedure;
    private DataGridView dgvHexView;
    private ProgressBar progressBar;
    private OpenFileDialog openFileDialog;
    private TableLayoutPanel mainLayout;

    public Boot_Interface_Tab()
    {
        this.Text = "Boot Interface";

        // Layout principale
        mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // TextBox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Pulsanti
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // ProgressBar

        // TextBox per mostrare il percorso del file selezionato
        txtFilePath = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            PlaceholderText = "No file selected"
        };

        // Pulsante per selezionare il file
        btnSelectFile = new Button
        {
            Text = "Select .bin File",
            Width = 100
        };
        btnSelectFile.Click += BtnSelectFile_Click;

        // Pulsante per avviare la procedura
        btnStartProcedure = new Button
        {
            Text = "Download",
            Width = 120
        };
        btnStartProcedure.Click += BtnStartProcedure_Click;

        // Contenitore per i pulsanti
        FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill
        };
        buttonPanel.Controls.Add(btnSelectFile);
        buttonPanel.Controls.Add(btnStartProcedure);

        // DataGridView per visualizzare il contenuto in esadecimale e ASCII
        dgvHexView = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };

        // Colonna Offset (larghezza fissa)
        DataGridViewColumn offsetColumn = new DataGridViewTextBoxColumn
        {
            Name = "Offset",
            HeaderText = "Offset",
            Width = 100,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };
        dgvHexView.Columns.Add(offsetColumn);

        // Colonna HexValues (si espande dinamicamente)
        DataGridViewColumn hexColumn = new DataGridViewTextBoxColumn
        {
            Name = "HexValues",
            HeaderText = "Hex Values",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgvHexView.Columns.Add(hexColumn);

        // Colonna AsciiValues (si espande dinamicamente)
        DataGridViewColumn asciiColumn = new DataGridViewTextBoxColumn
        {
            Name = "AsciiValues",
            HeaderText = "ASCII",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgvHexView.Columns.Add(asciiColumn);

        // Barra di progresso
        progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill
        };

        // OpenFileDialog per selezionare i file
        openFileDialog = new OpenFileDialog
        {
            Filter = "Binary Files|*.bin|All Files|*.*",
            Title = "Select a Binary File"
        };

        // Aggiunta dei controlli al layout
        mainLayout.Controls.Add(txtFilePath, 0, 0); // TextBox
        mainLayout.Controls.Add(buttonPanel, 0, 1); // Pulsanti
        mainLayout.Controls.Add(dgvHexView, 0, 2); // DataGridView
        mainLayout.Controls.Add(progressBar, 0, 3); // ProgressBar

        // Aggiunta del layout alla TabPage
        this.Controls.Add(mainLayout);
    }

    private void BtnSelectFile_Click(object sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            string filePath = openFileDialog.FileName;
            txtFilePath.Text = filePath; // Mostra il percorso nel TextBox
            DisplayFileContent(filePath);
        }
    }

    private void BtnStartProcedure_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Start Procedure button clicked. Implement your procedure here.");
    }

    private void DisplayFileContent(string filePath)
    {
        try
        {
            byte[] fileContent = File.ReadAllBytes(filePath);
            dgvHexView.Rows.Clear();

            int bytesPerRow = 16;
            for (int i = 0; i < fileContent.Length; i += bytesPerRow)
            {
                int rowLength = Math.Min(bytesPerRow, fileContent.Length - i);
                byte[] rowBytes = new byte[rowLength];
                Array.Copy(fileContent, i, rowBytes, 0, rowLength);

                string offset = i.ToString("X8");
                string hexValues = BitConverter.ToString(rowBytes).Replace("-", " ");
                string asciiValues = Encoding.ASCII.GetString(rowBytes)
                    .Replace('\0', '.')
                    .Replace("\r", ".")
                    .Replace("\n", ".");

                dgvHexView.Rows.Add(offset, hexValues, asciiValues);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

