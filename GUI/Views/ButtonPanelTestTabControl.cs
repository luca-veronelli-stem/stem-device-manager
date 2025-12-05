using StemPC;
using STEMPM.Core.ButtonPanelEnums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;

using System.Drawing.Drawing2D;

namespace STEMPM.GUI.Views
{
    public partial class ButtonPanelTestTabControl : UserControl, IButtonPanelTestTab
    {
        // Evento pubblico per notificare quando l'utente fa clic sul pulsante di esecuzione dei test
        public event EventHandler? OnRunTestsClicked;

        private ButtonPanelType? selectedPanelType;
        private Button? selectedButton;

        // Costruttore del controllo
        public ButtonPanelTestTabControl()
        {
            InitializeComponent();

            // Crea dinamicamente i toggle buttons basati sull'enum ButtonPanelType
            CreateToggleButtons();

            // Associa l'evento di click del pulsante all'evento pubblico
            buttonRunTests.Click += (s, e) => OnRunTestsClicked?.Invoke(this, EventArgs.Empty);
        }

        // Metodo per creare i toggle buttons dinamicamente (rectangular con round corners)
        private void CreateToggleButtons()
        {
            var types = Enum.GetValues(typeof(ButtonPanelType)).Cast<ButtonPanelType>().ToArray();
            int buttonHeight = 150;
            int spacing = 10;

            for (int i = 0; i < types.Length; i++)
            {
                Button btn = new()
                {
                    Text = types[i].ToString(),
                    Tag = types[i],
                    Location = new Point(10, 10 + i * (buttonHeight + spacing)),
                    Size = new Size(180, buttonHeight),
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    BackColor = SystemColors.Control,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btn.Click += ButtonToggle_Click;
                MakeRounded(btn, 20);
                panelToggleButtons.Controls.Add(btn);
            }

            // Seleziona il primo per default
            //if (panelToggleButtons.Controls.Count > 0 && panelToggleButtons.Controls[0] is Button firstBtn)
            //{
            //    ButtonToggle_Click(firstBtn, EventArgs.Empty);
            //}
        }

        // Metodo helper per rendere un button con angoli arrotondati
        private static void MakeRounded(Button btn, int radius)
        {
            GraphicsPath path = new();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(btn.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(btn.Width - radius * 2, btn.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, btn.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            btn.Region = new Region(path);
        }

        // Gestore per l'evento Click dei toggle buttons
        private void ButtonToggle_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag == null)
                {
                    throw new InvalidOperationException("Tipo di pulsantiera sconosciuto");
                }

                // Resetta il precedente selezionato
                if (selectedButton != null && selectedButton != btn)
                {
                    selectedButton.BackColor = SystemColors.Control;
                    selectedButton.ForeColor = Color.Black;
                }

                // Imposta il nuovo selezionato
                selectedButton = btn;
                selectedButton.BackColor = Color.FromArgb(0, 70, 128);
                selectedButton.ForeColor = Color.White;

                selectedPanelType = (ButtonPanelType)btn.Tag;

                UpdateTestTypeComboBox(selectedPanelType.Value);
                UpdateImage(selectedPanelType.Value);
                SetRecipientIdForPanel(selectedPanelType.Value);
            }
        }

        // Imposta il recipientId in base alla pulsantiera
        private static void UpdateRecipientIdForPanel(ButtonPanelType panelType)
        {
            // Mappa il recipientId in base al tipo di pulsantiera
            uint recipientId = panelType switch
            {
                ButtonPanelType.DIS0023789 => 0x00030101,
                ButtonPanelType.DIS0025205 => 0x000A0101,
                ButtonPanelType.DIS0026166 => 0x000B0101,
                ButtonPanelType.DIS0026182 => 0x000C0101,
                _ => (0x00000000)
            };

            // Chiama il metodo nel Form1
            Form1.FormRef.SetRecipientIdSilently(recipientId);
        }

        // Metodo helper per aggiornare comboBoxSelectTest filtrando opzioni non supportate
        private void UpdateTestTypeComboBox(ButtonPanelType panelType)
        {
            var panel = ButtonPanel.GetByType(panelType);
            var allTestTypes = Enum.GetValues(typeof(ButtonPanelTestType)).Cast<ButtonPanelTestType>().ToList();

            // Rimuovi Led se non supportato
            if (!panel.HasLed)
            {
                allTestTypes.Remove(ButtonPanelTestType.Led);
            }

            comboBoxSelectTest.DataSource = allTestTypes;
            comboBoxSelectTest.SelectedIndex = 0;
        }

        // Metodo per aggiornare l'immagine basata sul tipo selezionato
        private void UpdateImage(ButtonPanelType panelType)
        {
            pictureBoxImage.Image = Image.FromFile($"..\\..\\..\\..\\images\\ButtonPanels\\{panelType}.jpg") ?? null;
        }

        // Restituisce il tipo di pulsantiera selezionato
        public ButtonPanelType GetSelectedPanelType()
        {
            if (selectedPanelType.HasValue)
            {
                return selectedPanelType.Value;
            }

            throw new InvalidOperationException("Nessun tipo di pulsantiera selezionato.");
        }

        // Restituisce il tipo di test selezionato
        public ButtonPanelTestType GetSelectedTestType()
        {
            if (comboBoxSelectTest.SelectedItem == null)
            {
                throw new InvalidOperationException("Nessun tipo di collaudo selezionato");
            }

            return (ButtonPanelTestType)comboBoxSelectTest.SelectedItem;
        }

        // Mostra un prompt all'utente
        public async Task ShowPromptAsync(string message, string title = "Istruzione collaudo pulsanti")
        {
            await Task.Run(() => MessageBox.Show(message, title,
                MessageBoxButtons.OK, MessageBoxIcon.Information));
        }

        // Chiedi una conferma all'utente
        public async Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType)
        {
            string title = $"Conferma collaudo {testType}";

            return await Task.Run(() => MessageBox.Show(message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            foreach (var result in results)
            {
                string status = result.Passed ? "PASSATO" : "FALLITO";
                // TODO: tornare a capo se il messaggio è troppo lungo
                listBoxResults.Items.Add($"[{result.PanelType}] {result.TestType}: {status} - {result.Message}");
            }
        }

        // Aggiorna lo stato del collaudo
        public void ShowProgress(string message)
        {
            labelStatus.Text = message;
        }

        // Mostra eventuali messaggi di errore
        public void ShowError(string message)
        {
            MessageBox.Show(message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
