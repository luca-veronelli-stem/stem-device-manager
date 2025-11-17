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
            comboBoxPanelType = new ComboBox();
            buttonRunTests = new Button();
            listBoxResults = new ListBox();
            labelStatus = new Label();
            labelSelectPanel = new Label();
            labelSelectTestType = new Label();
            comboBoxSelectTest = new ComboBox();
            SuspendLayout();
            // 
            // comboBoxPanelType
            // 
            comboBoxPanelType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxPanelType.FormattingEnabled = true;
            comboBoxPanelType.Location = new Point(3, 18);
            comboBoxPanelType.Name = "comboBoxPanelType";
            comboBoxPanelType.Size = new Size(906, 23);
            comboBoxPanelType.TabIndex = 0;
            // 
            // buttonRunTests
            // 
            buttonRunTests.Location = new Point(3, 91);
            buttonRunTests.Name = "buttonRunTests";
            buttonRunTests.Size = new Size(906, 23);
            buttonRunTests.TabIndex = 1;
            buttonRunTests.Text = "Avvia collaudo";
            buttonRunTests.UseVisualStyleBackColor = true;
            // 
            // listBoxResults
            // 
            listBoxResults.FormattingEnabled = true;
            listBoxResults.ItemHeight = 15;
            listBoxResults.Location = new Point(3, 124);
            listBoxResults.Name = "listBoxResults";
            listBoxResults.Size = new Size(906, 544);
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
            // labelSelectPanel
            // 
            labelSelectPanel.AutoSize = true;
            labelSelectPanel.Dock = DockStyle.Top;
            labelSelectPanel.Location = new Point(0, 0);
            labelSelectPanel.Name = "labelSelectPanel";
            labelSelectPanel.Size = new Size(163, 15);
            labelSelectPanel.TabIndex = 4;
            labelSelectPanel.Text = "Seleziona il tipo di pulsantiera";
            // 
            // labelSelectTestType
            // 
            labelSelectTestType.AutoSize = true;
            labelSelectTestType.Location = new Point(0, 44);
            labelSelectTestType.Name = "labelSelectTestType";
            labelSelectTestType.Size = new Size(151, 15);
            labelSelectTestType.TabIndex = 5;
            labelSelectTestType.Text = "Seleziona il tipo di collaudo";
            // 
            // comboBoxSelectTest
            // 
            comboBoxSelectTest.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSelectTest.FormattingEnabled = true;
            comboBoxSelectTest.Location = new Point(3, 62);
            comboBoxSelectTest.Name = "comboBoxSelectTest";
            comboBoxSelectTest.Size = new Size(906, 23);
            comboBoxSelectTest.TabIndex = 6;
            // 
            // ButtonPanelTestTabControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(comboBoxSelectTest);
            Controls.Add(labelSelectTestType);
            Controls.Add(labelSelectPanel);
            Controls.Add(labelStatus);
            Controls.Add(listBoxResults);
            Controls.Add(buttonRunTests);
            Controls.Add(comboBoxPanelType);
            Name = "ButtonPanelTestTabControl";
            Size = new Size(912, 699);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox comboBoxPanelType;
        private Button buttonRunTests;
        private ListBox listBoxResults;
        private Label labelStatus;
        private Label labelSelectPanel;
        private Label labelSelectTestType;
        private ComboBox comboBoxSelectTest;
    }
}
