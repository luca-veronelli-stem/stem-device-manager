namespace STEMPM.GUI.Views
{
    partial class ButtonPanelTestTabControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            buttonRunTests = new Button();
            listBoxResults = new ListBox();
            labelStatus = new Label();
            labelSelectTestType = new Label();
            comboBoxSelectTest = new ComboBox();
            panelToggleButtons = new Panel();
            pictureBoxImage = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBoxImage).BeginInit();
            SuspendLayout();
            // 
            // buttonRunTests
            // 
            buttonRunTests.Location = new Point(210, 611);
            buttonRunTests.Name = "buttonRunTests";
            buttonRunTests.Size = new Size(699, 23);
            buttonRunTests.TabIndex = 1;
            buttonRunTests.Text = "Avvia collaudo";
            buttonRunTests.UseVisualStyleBackColor = true;
            // 
            // listBoxResults
            // 
            listBoxResults.FormattingEnabled = true;
            listBoxResults.ItemHeight = 15;
            listBoxResults.Location = new Point(210, 356);
            listBoxResults.Name = "listBoxResults";
            listBoxResults.Size = new Size(699, 244);
            listBoxResults.TabIndex = 2;
            // 
            // labelStatus
            // 
            labelStatus.AutoSize = true;
            labelStatus.Dock = DockStyle.Bottom;
            labelStatus.Location = new Point(0, 684);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(64, 15);
            labelStatus.TabIndex = 3;
            labelStatus.Text = "labelStatus";
            labelStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelSelectTestType
            // 
            labelSelectTestType.AutoSize = true;
            labelSelectTestType.Location = new Point(207, 310);
            labelSelectTestType.Name = "labelSelectTestType";
            labelSelectTestType.Size = new Size(151, 15);
            labelSelectTestType.TabIndex = 5;
            labelSelectTestType.Text = "Seleziona il tipo di collaudo";
            // 
            // comboBoxSelectTest
            // 
            comboBoxSelectTest.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSelectTest.FormattingEnabled = true;
            comboBoxSelectTest.Location = new Point(210, 328);
            comboBoxSelectTest.Name = "comboBoxSelectTest";
            comboBoxSelectTest.Size = new Size(699, 23);
            comboBoxSelectTest.TabIndex = 6;
            // 
            // panelToggleButtons
            // 
            panelToggleButtons.Location = new Point(3, 3);
            panelToggleButtons.Name = "panelToggleButtons";
            panelToggleButtons.Size = new Size(200, 693);
            panelToggleButtons.TabIndex = 7;
            // 
            // pictureBoxImage
            // 
            pictureBoxImage.Location = new Point(210, 3);
            pictureBoxImage.Name = "pictureBoxImage";
            pictureBoxImage.Size = new Size(699, 300);
            pictureBoxImage.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxImage.TabIndex = 8;
            pictureBoxImage.TabStop = false;
            // 
            // ButtonPanelTestTabControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(pictureBoxImage);
            Controls.Add(panelToggleButtons);
            Controls.Add(comboBoxSelectTest);
            Controls.Add(labelSelectTestType);
            Controls.Add(labelStatus);
            Controls.Add(listBoxResults);
            Controls.Add(buttonRunTests);
            Name = "ButtonPanelTestTabControl";
            Size = new Size(912, 699);
            ((System.ComponentModel.ISupportInitialize)pictureBoxImage).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonRunTests;
        private ListBox listBoxResults;
        private Label labelStatus;
        private Label labelSelectTestType;
        private ComboBox comboBoxSelectTest;
        private Panel panelToggleButtons;
        private PictureBox pictureBoxImage;
    }
}
