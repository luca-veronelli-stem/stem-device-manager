using App.Properties;
using App.STEMProtocol;
using Core.Models;
using Services.Cache;

public class Telemetry_Tab : TabPage
{
    //Cache centralizzata dizionario (sostituisce UpdateDictionary callback)
    private readonly DictionaryCache _cache;
    //Lista variabili della macchina (snapshot letto dalla cache)
    private IReadOnlyList<Variable> MachineDictionary = [];

    // Classe per il backend
    public TelemetryManager telemetryManager;

    // Campi per accedere dal gestore degli eventi
    private TableLayoutPanel tableLayout;
    private ComboBox comboBox;

    // Lista per memorizzare gli elementi attivi aggiunti
    private List<Control> activeElements = new List<Control>();

    // Variabili per tenere traccia della posizione per l'inserimento
    // Gli elementi vengono inseriti a partire dalla riga 3 (indice 2)
    private int currentColumn = 0;
    private int currentRow = 2;

    public Telemetry_Tab(PacketManager packetManagerRX, DictionaryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        MachineDictionary = _cache.Variables;
        _cache.DictionaryUpdated += OnCacheDictionaryUpdated;

        Name = "tabPageTelemetry";
        Text = "Telemetry";

        // 1. Creazione del TableLayoutPanel con 4 colonne e 10 righe.
        tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 10
        };

        // Impostiamo larghezze uguali per le colonne (25% ciascuna)
        for (int i = 0; i < 4; i++)
        {
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        // Impostiamo altezze uguali per le righe (10% ciascuna)
        for (int i = 0; i < 10; i++)
        {
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        }

        // 2. Aggiunta della Label in cella (0,0)
        Label label = new Label
        {
            Text = "Variables",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        // Imposta il font in grassetto
        label.Font = new System.Drawing.Font(label.Font, FontStyle.Bold);
        tableLayout.Controls.Add(label, 0, 0);
        // Imposta il ColumnSpan a 2 (la label occuperà due colonne)
        tableLayout.SetColumnSpan(label, 2);

        // 3. Aggiunta della ComboBox in cella (0,1)
        comboBox = new ComboBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // Lista di stringhe da inserire nella ComboBox
        UpdateComboBox();
        tableLayout.Controls.Add(comboBox, 0, 1);

        // 4. Aggiunta del Button in cella (1,1)
        Button button = new Button
        {
            Text = "Add",
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        button.Click += Button_Click;
        tableLayout.Controls.Add(button, 1, 1);

        // 5. Aggiunta del Button in cella (2,1)
        Button buttonStart = new Button
        {
            Text = "Start",
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Left
        };
        buttonStart.Font = new System.Drawing.Font(buttonStart.Font, FontStyle.Bold);
        buttonStart.Click += ButtonStart_Click;
        tableLayout.Controls.Add(buttonStart, 2, 1);

        // 6. Aggiunta del Button in cella (3,1)
        Button buttonStop = new Button
        {
            Text = "Stop",
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Left
        };
        buttonStop.Font = new System.Drawing.Font(buttonStop.Font, FontStyle.Bold);
        buttonStop.Click += ButtonStop_Click;
        tableLayout.Controls.Add(buttonStop, 3, 1);

        // 7. Aggiunta del TableLayoutPanel alla TabPage
        Controls.Add(tableLayout);

        // 8. Creazione del gestore per la telemetria
        telemetryManager = new TelemetryManager(packetManagerRX);

        // Aggiunta del gestore per l'evento DataReady
        telemetryManager.DataReady += onDataReady;
    }

    private void UpdateComboBox()
    {
        // Aggiorna la ComboBox con i nomi delle variabili
        comboBox.Items.Clear();

        if (MachineDictionary == null) return;

        comboBox.SelectedIndex = -1;

        foreach (var variable in MachineDictionary)
        {
            comboBox.Items.Add(variable.Name);
            comboBox.SelectedIndex = 0;
        }
    }

    private async void onDataReady(object sender, DataReadyEventArgs e)
    {
        if (e.ListIndex >= activeElements.Count)
        {
            MessageBox.Show("Il controllo non è una Label.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var container = activeElements[e.ListIndex];
        var control = container.Controls[1];
        if (control is Label label)
        {
            await Task.Run(() => label.Invoke((MethodInvoker)(() => label.Text = telemetryManager.GetVariableName(e.ListIndex) + " " + e.Value.ToString())));
        }
        else
        {
            MessageBox.Show("Il controllo non è una Label.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnCacheDictionaryUpdated(object sender, EventArgs e)
    {
        MachineDictionary = _cache.Variables;
        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(new Action(UpdateComboBox));
        else
            UpdateComboBox();
    }

    private void Button_Click(object sender, EventArgs e)
    {
        if (comboBox.SelectedItem != null)
        {
            string selectedText = comboBox.SelectedItem.ToString();

            // Controlla se esiste già un elemento con la stessa label
            foreach (var element in activeElements)
            {
                if (element.Controls[1] is Label label && label.Text == selectedText)
                {
                    MessageBox.Show("L'elemento esiste già nella lista.",
                                    "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (!HasAvailableSpace())
            {
                MessageBox.Show("Non ci sono più celle libere per aggiungere nuovi elementi.",
                                "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Creazione di un contenitore per l'elemento composto (pulsante "rimuovi" + label)
            FlowLayoutPanel container = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Creazione del pulsante "rimuovi" con icona
            Button removeButton = new Button
            {
                AutoSize = true
            };
            removeButton.Image = Resources.delete_24x24;
            removeButton.ImageAlign = ContentAlignment.MiddleCenter;
            removeButton.Size = new Size(24, 24);

            // Gestore per la rimozione: rimuove il contenitore dal TableLayoutPanel e dalla lista
            removeButton.Click += (s, args) =>
            {
                // Rimuovo il contenitore dal TableLayoutPanel (se presente)
                if (tableLayout.Controls.Contains(container))
                {
                    tableLayout.Controls.Remove(container);
                }

                // Recupera l'indice del container nella lista activeElements
                int index = activeElements.IndexOf(container);

                // Rimuovo il contenitore dalla lista degli elementi attivi
                activeElements.Remove(container);

                // Rimuovo l'elemento dalla lista dei dispositivi da interrogare
                telemetryManager.RemoveFromDictionary(index);

                // Ricompongo gli elementi rimasti
                ReLayoutElements();
            };

            // Creazione della Label con il testo selezionato
            Label newLabel = new Label
            {
                Text = selectedText,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            // Aggiunta dei controlli al contenitore
            container.Controls.Add(removeButton);
            container.Controls.Add(newLabel);

            // Aggiungo il contenitore al TableLayoutPanel nella cella corrente
            tableLayout.Controls.Add(container, currentColumn, currentRow);

            // Registro il contenitore nella lista degli elementi attivi
            activeElements.Add(container);

            // Aggiorno la lista dei dispositivi da interrogare
            telemetryManager.AddToDictionary(MachineDictionary[comboBox.SelectedIndex]);

            // Aggiorno la posizione per il prossimo inserimento
            UpdateInsertionPosition();
        }
        else
        {
            MessageBox.Show("Seleziona una macchina prima di procedere.",
                            "Informazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void ButtonStart_Click(object sender, EventArgs e)
    {
#if TOPLIFT
        // Aggiorno la lista dei dispositivi da interrogare
        telemetryManager.AddToDictionary(MachineDictionary[1]);
#endif
        await telemetryManager.TelemetryStart();
    }

    private void ButtonStop_Click(object sender, EventArgs e)
    {
        telemetryManager.TelemetryStop();
    }

    /// <summary>
    /// Verifica se c'è spazio disponibile per un nuovo elemento.
    /// </summary>
    private bool HasAvailableSpace()
    {
        int capacityPerColumn = tableLayout.RowCount - 2;
        int maxElements = tableLayout.ColumnCount * capacityPerColumn;
        return activeElements.Count < maxElements;
    }

    /// <summary>
    /// Aggiorna le variabili di posizionamento per l'inserimento del prossimo elemento.
    /// </summary>
    private void UpdateInsertionPosition()
    {
        int capacityPerColumn = tableLayout.RowCount - 2;
        int count = activeElements.Count;
        currentColumn = count / capacityPerColumn;
        currentRow = (count % capacityPerColumn) + 2;
    }

    /// <summary>
    /// Ricompatta gli elementi attivi: li rimuove dal TableLayoutPanel e li reinserisce 
    /// in ordine, a partire dalla cella (0,2) (ossia la terza riga) senza spazi vuoti.
    /// </summary>
    private void ReLayoutElements()
    {
        // Rimuovo tutti gli elementi attivi dal TableLayoutPanel...
        foreach (Control ctrl in activeElements)
        {
            if (tableLayout.Controls.Contains(ctrl))
            {
                tableLayout.Controls.Remove(ctrl);
            }
        }

        // ... e li reinserisco in ordine
        int capacityPerColumn = tableLayout.RowCount - 2;
        for (int i = 0; i < activeElements.Count; i++)
        {
            int col = i / capacityPerColumn;
            int row = (i % capacityPerColumn) + 2;
            tableLayout.Controls.Add(activeElements[i], col, row);
        }

        UpdateInsertionPosition();
        tableLayout.PerformLayout();
    }
}
