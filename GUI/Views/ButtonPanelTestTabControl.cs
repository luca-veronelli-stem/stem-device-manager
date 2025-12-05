using StemPC;
using STEMPM.Core.ButtonPanelEnums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;

using System.Drawing.Drawing2D;

namespace STEMPM.GUI.Views
{
    public partial class ButtonPanelTestTabControl : UserControl, IButtonPanelTestTab
    {
        // Gestori eventi invocati quando l'utente avvia o arresta il collaudo
        public event EventHandler? OnStartTestClicked;
        public event EventHandler? OnStopTestClicked;
        public event EventHandler? OnDownloadTestResultClicked;

        private ButtonPanelType? selectedPanelType;
        private ButtonPanelTestType? selectedTestType;
        private List<ButtonIndicator> _buttonIndicators = [];
        private readonly Dictionary<ButtonPanelType, List<RectangleF>> _buttonRegions = new()
        {
            {
                ButtonPanelType.DIS0023789, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0025205, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0026166 , new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0026182, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            }
        };

        // Costruttore del controllo
        public ButtonPanelTestTabControl()
        {
            // Popola la pagina con gli elementi grafici
            InitializeComponent();

            // Popola il pannello con i pulsanti per selezionare il tipo di pulsantiera
            CreateSelectPanelButtons();

            // Popola il pannello con i pulsanti per selezionare il tipo di collaudo
            ButtonPanelTestType[] testTypes = [.. Enum.GetValues(typeof(ButtonPanelTestType)).Cast<ButtonPanelTestType>()];
            CreateSelectTestButtons(testTypes);

            // Associa i gestori agli eventi di click
            buttonStartTest.Click += (s, e) => OnStartTestClicked?.Invoke(this, EventArgs.Empty);
            buttonStopTest.Click += (s, e) => OnStopTestClicked?.Invoke(this, EventArgs.Empty);
            buttonDownloadTestResult.Click += (s, e) => OnDownloadTestResultClicked?.Invoke(this, EventArgs.Empty);

            // Associa il metodo per colorare gli indicatori
            pictureBoxImage.Paint += PictureBoxImage_Paint;
            pictureBoxImage.SizeChanged += (s, e) => pictureBoxImage.Invalidate();
        }

        // Metodo per creare dinamicamente i toggle buttons di selezione pulsantiera
        private void CreateSelectPanelButtons()
        {
            ButtonPanelType[] buttonPaneltypes = [.. Enum.GetValues(typeof(ButtonPanelType)).Cast<ButtonPanelType>()];
            int buttonHeight = 150;
            int spacing = 10;

            for (int i = 0; i < buttonPaneltypes.Length; i++)
            {
                Button btn = new()
                {
                    Text = buttonPaneltypes[i].ToString(),
                    Tag = buttonPaneltypes[i],
                    Location = new Point(10, 10 + i * (buttonHeight + spacing)),
                    Size = new Size(180, buttonHeight),
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 1, BorderColor = Color.LightGray },
                    BackColor = SystemColors.Control,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                btn.Click += ButtonSelectPanel_Click;
                MakeRounded(btn, 10);
                panelButtonPanelSelection.Controls.Add(btn);
            }
        }

        // Metodo per creare dinamicamente i toggle buttons di selezione collaudo
        private void CreateSelectTestButtons(ButtonPanelTestType[] testTypes)
        {
            int buttonWidth = 150;
            int buttonHeight = 35;
            int spacing = 10;

            for (int i = 0; i < testTypes.Length; i++)
            {
                Button btn = new()
                {
                    Text = testTypes[i].ToString(),
                    Tag = testTypes[i],
                    Location = new Point(i * (buttonWidth + spacing), 0),
                    Size = new Size(buttonWidth, buttonHeight),
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 1, BorderColor = Color.Gainsboro },
                    BackColor = SystemColors.Control,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                btn.Click += ButtonSelectTest_Click;
                MakeRounded(btn, 10);
                panelTestSelection.Controls.Add(btn);
            }

            // Seleziona il primo per default (Complete)
            if (panelTestSelection.Controls.Count > 0 && panelTestSelection.Controls[0] is Button firstBtn)
            {
                ButtonSelectTest_Click(firstBtn, EventArgs.Empty);
            }
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

        // Gestore per l'evento click su selezione pulsantiera
        private void ButtonSelectPanel_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag == null)
                {
                    throw new InvalidOperationException("Tipo di pulsantiera sconosciuto");
                }

                selectedPanelType = (ButtonPanelType)btn.Tag;

                // Cambia il colore del bottone selezionato
                btn.BackColor = Color.FromArgb(0, 70, 128);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderSize = 0;

                // Resetta gli altri bottoni
                foreach (Button b in panelButtonPanelSelection.Controls)
                {
                    if (b != btn)
                    {
                        b.BackColor = SystemColors.Control;
                        b.ForeColor = Color.Black;
                        b.FlatAppearance.BorderSize = 1;
                        b.FlatAppearance.BorderColor = Color.LightGray;
                    }
                }

                UpdateRecipientIdForPanel(selectedPanelType.Value);
                UpdateImage(selectedPanelType.Value);
                UpdateButtonIndicators(selectedPanelType.Value);
                UpdateTestButtons(selectedPanelType.Value);
            }
        }

        // Gestore per click su selezione collaudo
        private void ButtonSelectTest_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.Tag == null)
            {
                throw new InvalidOperationException("Tipo di pulsantiera sconosciuto");
            }

            // Imposta il tipo di test selezionato
            selectedTestType = (ButtonPanelTestType)btn.Tag;

            // Cambia il colore del bottone selezionato
            btn.BackColor = Color.FromArgb(0, 70, 128);
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;

            // Resetta gli altri bottoni
            foreach (Button b in panelTestSelection.Controls)
            {
                if (b != btn)
                {
                    b.BackColor = SystemColors.Control;
                    b.ForeColor = Color.Black;
                    b.FlatAppearance.BorderSize = 1;
                    b.FlatAppearance.BorderColor = Color.Gainsboro;
                }
            }
        }

        // Restituisce il testo dei risultati del collaudo
        public string GetResultsText() => richTextBoxTestResult.Text;

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

        // Restituisce i test disponibili per un tipo di pulsantiera
        private static ButtonPanelTestType[] GetAvailableTests(ButtonPanelType panelType)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);
            List<ButtonPanelTestType> availableTestTypes = [.. Enum.GetValues(typeof(ButtonPanelTestType)).Cast<ButtonPanelTestType>()];

            // Rimuovi Led se non supportato
            if (!panel.HasLed)
            {
                availableTestTypes.Remove(ButtonPanelTestType.Led);
            }

            return [.. availableTestTypes];
        }

        // Metodo helper per aggiornare i pulsanti dei test disponibili in base alla pulsantiera
        private void UpdateTestButtons(ButtonPanelType panelType)
        {
            ButtonPanelTestType[] availableTests = GetAvailableTests(panelType);
            panelTestSelection.Controls.Clear();
            CreateSelectTestButtons(availableTests);
        }

        // Metodo per aggiornare l'immagine basata sul tipo selezionato
        private void UpdateImage(ButtonPanelType panelType)
        {
            pictureBoxImage.Image = Image.FromFile($"..\\..\\..\\..\\images\\ButtonPanels\\{panelType}.jpg") ?? null;
        }

        // Metodo per aggiornare gli indicatori dei pulsanti
        private void UpdateButtonIndicators(ButtonPanelType panelType)
        {
            if (!_buttonRegions.TryGetValue(panelType, out var regions))
            {
                _buttonIndicators.Clear();
                pictureBoxImage.Invalidate();
                return;
            }



            _buttonIndicators = [.. regions.Select(r => new ButtonIndicator
            {
                Bounds = r,
                State = IndicatorState.Idle
            })];

            pictureBoxImage.Invalidate();
        }

        // Gestore per disegnare gli indicatori sui pulsanti
        private void PictureBoxImage_Paint(object? sender, PaintEventArgs e)
        {
            if (_buttonIndicators.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (var indicator in _buttonIndicators)
            {
                var rect = new Rectangle(
                    (int)(indicator.Bounds.X * pictureBoxImage.Width),
                    (int)(indicator.Bounds.Y * pictureBoxImage.Height),
                    (int)(indicator.Bounds.Width * pictureBoxImage.Width),
                    (int)(indicator.Bounds.Height * pictureBoxImage.Height)
                );

                Color fillColor = indicator.State switch
                {
                    IndicatorState.Waiting => Color.FromArgb(180, Color.Yellow),
                    IndicatorState.Success => Color.FromArgb(180, Color.LimeGreen),
                    IndicatorState.Failed => Color.FromArgb(180, Color.Red),
                    _ => Color.FromArgb(120, Color.White)
                };

                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillRectangle(brush, rect);
                }

                using (var pen = new Pen(Color.Black, 1))
                {
                    g.DrawRectangle(pen, rect);
                }
            }
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
            if (selectedTestType.HasValue)
            {
                return selectedTestType.Value;
            }

            throw new InvalidOperationException("Nessun tipo di collaudo selezionato");
        }

        // Imposta l'indicatore in attesa
        public void SetButtonWaiting(int buttonIndex)
        {
            if (buttonIndex < _buttonIndicators.Count)
            {
                _buttonIndicators[buttonIndex].State = IndicatorState.Waiting;
                pictureBoxImage.Invalidate();
            }
        }

        // Imposta l'indicatore con il risultato
        public void SetButtonResult(int buttonIndex, bool success)
        {
            if (buttonIndex < _buttonIndicators.Count)
            {
                _buttonIndicators[buttonIndex].State = success ? IndicatorState.Success : IndicatorState.Failed;
                pictureBoxImage.Invalidate();
            }
        }

        // Reimposta gli indicatori
        public void ResetAllIndicators()
        {
            foreach (var ind in _buttonIndicators)
                ind.State = IndicatorState.Idle;

            pictureBoxImage.Invalidate();
        }

        // Mostra un prompt all'utente in richTextBoxTestProgress
        public async Task ShowPromptAsync(string message)
        {
            richTextBoxTestProgress.SelectionStart = richTextBoxTestProgress.TextLength;
            richTextBoxTestProgress.SelectionLength = 0;
            richTextBoxTestProgress.SelectionColor = Color.Yellow;
            richTextBoxTestProgress.AppendText(message + Environment.NewLine);
            richTextBoxTestProgress.SelectionColor = richTextBoxTestProgress.ForeColor;
            richTextBoxTestProgress.ScrollToCaret();
            await Task.CompletedTask;
        }

        // Chiedi una conferma all'utente
        public Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType)
        {
            string title = $"Conferma collaudo {testType}";
            var result = MessageBox.Show(ParentForm, message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            return Task.FromResult(result);
        }

        // Mostra la finestra di dialogo per il salvataggio del file
        public string? ShowSaveFileDialog()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "File di testo (*.txt)|*.txt|Tutti i file (*.*)|*.*";
                sfd.DefaultExt = "txt";
                sfd.Title = "Salva Risultati Collaudo";
                sfd.FileName = $"Risultati_Collaudo_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                return sfd.ShowDialog() == DialogResult.OK ? sfd.FileName : null;
            }
        }

        // Mostra un messaggio all'utente
        public void ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            MessageBox.Show(message, title, buttons, icon);
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            // Pulisci i risultati precedenti
            richTextBoxTestResult.Clear();
            // Mostra header comune ai collaudi
            if (results.Count > 0)
            {
                richTextBoxTestResult.AppendText($"Risultati collaudo pulsantiera [{results[0].PanelType}]" + Environment.NewLine);
            }

            foreach (var result in results)
            {
                // Mostra nome del collaudo
                richTextBoxTestResult.AppendText($"Collaudo {result.TestType}: ");

                // Determina stato e colore
                string status;
                Color statusColor;
                if (result.Interrupted)
                {
                    status = "INTERROTTO";
                    statusColor = Color.Orange;
                }
                else
                {
                    status = result.Passed ? "PASSATO" : "FALLITO";
                    statusColor = result.Passed ? Color.LimeGreen : Color.Red;
                }

                richTextBoxTestResult.SelectionColor = statusColor;
                richTextBoxTestResult.AppendText(status);
                richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                richTextBoxTestResult.AppendText(Environment.NewLine);

                // Gestisci il messaggio
                string[] lines = result.Message.Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.Contains("interrotto"))
                    {
                        richTextBoxTestResult.SelectionColor = Color.Orange;
                        richTextBoxTestResult.AppendText(line);
                        richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                    }
                    else
                    {
                        int colonIndex = line.LastIndexOf(':');
                        if (colonIndex != -1)
                        {
                            string before = line.Substring(0, colonIndex + 1) + " ";
                            string after = line.Substring(colonIndex + 1).Trim();

                            richTextBoxTestResult.AppendText(before);

                            bool subPassed = after.Contains("PASSATO");
                            Color subColor = subPassed ? Color.LimeGreen : Color.Red;

                            richTextBoxTestResult.SelectionColor = subColor;
                            richTextBoxTestResult.AppendText(after);
                            richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                        }
                        else
                        {
                            richTextBoxTestResult.AppendText(line);
                        }
                    }

                    richTextBoxTestResult.AppendText(Environment.NewLine);
                }

                richTextBoxTestResult.AppendText(Environment.NewLine);
            }

            // Scrolla alla fine
            richTextBoxTestResult.ScrollToCaret();
        }

        // Mostra un messaggio di progresso
        public void ShowProgress(string message)
        {
            richTextBoxTestProgress.AppendText(message + Environment.NewLine);
            richTextBoxTestProgress.ScrollToCaret();
        }

        // Aggiorna il colore dell'ultimo prompt visualizzato
        public void UpdateLastPromptColor(string lastMessage, Color color)
        {
            int startIndex = richTextBoxTestProgress.TextLength - lastMessage.Length - 1;

            // Select the text
            richTextBoxTestProgress.SelectionStart = startIndex;
            richTextBoxTestProgress.SelectionLength = lastMessage.Length;

            // Apply the new color
            richTextBoxTestProgress.SelectionColor = color;

            // Deselect to avoid highlighting
            richTextBoxTestProgress.SelectionLength = 0;
            richTextBoxTestProgress.SelectionStart = richTextBoxTestProgress.TextLength;
        }

        // Mostra eventuali messaggi di errore
        public void ShowError(string message)
        {
            MessageBox.Show(ParentForm, message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
