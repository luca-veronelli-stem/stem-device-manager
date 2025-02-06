using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Stem_Protocol.PacketManager;
using Stem_Protocol.TelemetryManager;

public class Telemetry_Tab : TabPage
{
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

    public Telemetry_Tab(PacketManager packetManagerRX)
    {
        Name = "tabPageTelemetry";
        Text = "Telemetry";

        // 1. Creazione del TableLayoutPanel con 5 colonne e 10 righe.
        tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 10
        };

        // Impostiamo larghezze uguali per le colonne (20% ciascuna)
        for (int i = 0; i < 5; i++)
        {
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
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
        label.Font = new Font(label.Font, FontStyle.Bold);
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
        List<string> items = new List<string> { "Variabile 1", "Variabile 2", "Variabile 3" };
        comboBox.Items.AddRange(items.ToArray());
        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
        tableLayout.Controls.Add(comboBox, 0, 1);

        // 4. Aggiunta del Button in cella (1,1)
        Button button = new Button
        {
            Text = "Add",
            Anchor = AnchorStyles.Left
        };
        button.Click += Button_Click;
        tableLayout.Controls.Add(button, 1, 1);

        // 5. Aggiunta del Button in cella (2,1)
        Button buttonStart = new Button
        {
            Text = "Start",
            Anchor = AnchorStyles.Left
        };
        buttonStart.Font = new Font(buttonStart.Font, FontStyle.Bold);
        buttonStart.Click += ButtonStart_Click;
        tableLayout.Controls.Add(buttonStart, 2, 1);

        // 6. Aggiunta del Button in cella (3,1)
        Button buttonStop = new Button
        {
            Text = "Stop",
            Anchor = AnchorStyles.Left
        };
        buttonStop.Font = new Font(buttonStop.Font, FontStyle.Bold);
        buttonStop.Click += ButtonStop_Click;
        tableLayout.Controls.Add(buttonStop, 3, 1);

        // 7. Aggiunta del TableLayoutPanel alla TabPage
        Controls.Add(tableLayout);

        // 8. Creazione del gestore per la telemetria
        telemetryManager = new TelemetryManager(packetManagerRX);

        // Aggiunta del gestore per l'evento DataReady
        telemetryManager.DataReady += onDataReady;
    }

    private void onDataReady(object sender, DataReadyEventArgs e)
    {


    }

    private void Button_Click(object sender, EventArgs e)
    {
        if (comboBox.SelectedItem != null)
        {
            string selectedText = comboBox.SelectedItem.ToString();

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

            // Creazione del pulsante "rimuovi"
            Button removeButton = new Button
            {
                Text = "Remove",
                AutoSize = true
            };

            // Gestore per la rimozione: rimuove il contenitore dal TableLayoutPanel e dalla lista
            removeButton.Click += (s, args) =>
            {
                // Rimuovo il contenitore dal TableLayoutPanel (se presente)
                if (tableLayout.Controls.Contains(container))
                {
                    tableLayout.Controls.Remove(container);
                }
                // Rimuovo il contenitore dalla lista degli elementi attivi
                activeElements.Remove(container);
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

            // Aggiorno la posizione per il prossimo inserimento
            UpdateInsertionPosition();
        }
        else
        {
            MessageBox.Show("Seleziona un elemento dal ComboBox prima di aggiungere.",
                            "Informazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ButtonStart_Click(object sender, EventArgs e)
    {
        telemetryManager.TelemetryStart();
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
