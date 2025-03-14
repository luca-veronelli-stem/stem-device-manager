using Stem_Protocol.BootManager;
using StemPC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class DeviceInfo
{
    public int Address { get; set; }
    public string DisplayName { get; set; }

    public DeviceInfo(int address, string displayName)
    {
        Address = address;
        DisplayName = displayName;
    }

}

public class Boot_Smart_Tab : TabPage
{ 
// Pannello che ospiterà i controlli dinamici
private TableLayoutPanel selectionPanel; 
// Bottone per avviare la procedura
private Button btnStartProcedure; 
// Lista dei controlli associati a ciascun dispositivo
private List<BinSelectionControl> binSelections = new List<BinSelectionControl>(); 
public BootManager BootHndlr;

    public Boot_Smart_Tab()
    {
        Name = "tabPageBootSmart";
        Text = "Boot Smart";

        // Layout principale con due righe:
        // - La prima (AutoSize) contiene il pannello di selezione
        // - La seconda (Absolute) contiene il bottone con altezza fissa
        TableLayoutPanel mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        // Creazione del pannello di selezione configurato per adattarsi al contenuto
        selectionPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Dock = DockStyle.Top,  // Utilizziamo Top in modo che si adatti al contenuto
            Margin = new Padding(0)
        };

        // Configuriamo le colonne del pannello (30% - 50% - 20%)
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        // Creazione del pulsante di upload con altezza fissa
        btnStartProcedure = new Button
        {
            Text = "Upload Firmware",
            Height = 40,  // Altezza fissa
            Anchor = AnchorStyles.None,  // Centro nella cella, senza espandersi
            Margin = new Padding(3)
        };
        btnStartProcedure.Click += BtnStartProcedure_Click;

        // Aggiungiamo il pannello e il pulsante al layout principale
        mainLayout.Controls.Add(selectionPanel, 0, 0);
        mainLayout.Controls.Add(btnStartProcedure, 0, 1);

        this.Controls.Add(mainLayout);

        // Inizializzazione del BootManager
        BootHndlr = new BootManager();
        BootHndlr.ProgressChanged += UpdateProgressBar;
    }

    // Metodo pubblico per popolare la tab con la lista dei dispositivi
    public void PopulateDevices(List<DeviceInfo> devices)
    {
        // Azzeramento: pulisce il pannello e la lista di selezioni
        selectionPanel.Controls.Clear();
        selectionPanel.RowStyles.Clear();
        selectionPanel.RowCount = 0;
        binSelections.Clear();

        // Per ogni dispositivo crea una riga con Label, TextBox e Button
        foreach (DeviceInfo device in devices)
        {
            // Label che visualizza il DisplayName del dispositivo
            Label lblDevice = new Label
            {
                Text = device.DisplayName,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3)
            };

            // TextBox per mostrare il percorso del file .bin selezionato (sola lettura)
            TextBox txtFilePath = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };

            // Bottone per aprire l'OpenFileDialog e selezionare il file .bin
            Button btnSelectFile = new Button
            {
                Text = "Select .bin",
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };
            btnSelectFile.Click += (s, e) =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Binary Files|*.bin|All Files|*.*",
                    Title = "Select a Binary File"
                };
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = openFileDialog.FileName;
                }
            };

            // Aggiunge una nuova riga al pannello
            int currentRow = selectionPanel.RowCount;
            selectionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selectionPanel.Controls.Add(lblDevice, 0, currentRow);
            selectionPanel.Controls.Add(txtFilePath, 1, currentRow);
            selectionPanel.Controls.Add(btnSelectFile, 2, currentRow);
            selectionPanel.RowCount++;

            // Registra il controllo per la procedura di upload
            binSelections.Add(new BinSelectionControl(device, txtFilePath));
        }
    }

    private async void BtnStartProcedure_Click(object sender, EventArgs e)
    {
        // Verifica che per ogni dispositivo sia stato selezionato un file .bin
        foreach (var selection in binSelections)
        {
            if (string.IsNullOrEmpty(selection.FilePath))
            {
                MessageBox.Show($"Seleziona un file firmware per {selection.Device.DisplayName}");
                return;
            }
        }

        btnStartProcedure.Enabled = false;

        // Esegue il ciclo di upload per ciascun dispositivo
        foreach (var selection in binSelections)
        {
            BootHndlr.SetFirmwarePath(selection.FilePath);
            // Imposta l'indirizzo di destinazione (ad esempio, tramite una proprietà del main form)
            Form1.FormRef.RecipientId = (uint)selection.Device.Address;

            await BootHndlr.StartBoot();
            await BootHndlr.UploadFirmware();
        }

        btnStartProcedure.Enabled = true;
    }

    // Metodo per aggiornare la progress bar (da personalizzare se necessario)
    private void UpdateProgressBar(object sender, ProgressEventArgs e)
    {
        // Implementa eventuale aggiornamento grafico della progressione
    }

}

public class BinSelectionControl
{
    public DeviceInfo Device { get; private set; }
    public TextBox FilePathTextBox { get; private set; }
    public string FilePath => FilePathTextBox.Text;

    public BinSelectionControl(DeviceInfo device, TextBox filePathTextBox)
    {
        Device = device;
        FilePathTextBox = filePathTextBox;
    }

}