namespace StemPC
{ 
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            timerBaseTime = new System.Windows.Forms.Timer(components);
            tableLayoutPanel1 = new TableLayoutPanel();
            tabControl = new TabControl();
            tabPageProtocol = new TabPage();
            tableLayoutPanelProtocol = new TableLayoutPanel();
            comboBoxVariables = new ComboBox();
            labelDictionary = new Label();
            richTextBoxTx = new RichTextBox();
            textBox3 = new TextBox();
            textBox2 = new TextBox();
            comboBoxBoard = new ComboBox();
            labelBytes = new Label();
            labelByte2 = new Label();
            labelByte1 = new Label();
            label4 = new Label();
            label2 = new Label();
            comboBoxMachine = new ComboBox();
            comboBoxCommand = new ComboBox();
            textBox1 = new TextBox();
            label3 = new Label();
            buttonSendPS = new Button();
            label12 = new Label();
            tabPageCodeGen = new TabPage();
            tableLayoutPanel2 = new TableLayoutPanel();
            button1 = new Button();
            tabPageUART = new TabPage();
            label1 = new Label();
            listBoxSerialPorts = new ListBox();
            terminalOut = new RichTextBox();
            statusStrip1 = new StatusStrip();
            toolStripSplitButton2 = new ToolStripSplitButton();
            ChannelMenu = new ToolStripMenuItem();
            cANToolStripMenuItem = new ToolStripMenuItem();
            bluetoothLEToolStripMenuItem = new ToolStripMenuItem();
            PCanLabel = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            BLEStatusLabel = new ToolStripStatusLabel();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            tableLayoutPanel1.SuspendLayout();
            tabControl.SuspendLayout();
            tabPageProtocol.SuspendLayout();
            tableLayoutPanelProtocol.SuspendLayout();
            tabPageCodeGen.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tabPageUART.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // timerBaseTime
            // 
            timerBaseTime.Tick += timerBaseTime_Tick;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(tabControl, 0, 0);
            tableLayoutPanel1.Controls.Add(terminalOut, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.Size = new Size(800, 450);
            tableLayoutPanel1.TabIndex = 2;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPageProtocol);
            tabControl.Controls.Add(tabPageCodeGen);
            tabControl.Controls.Add(tabPageUART);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(3, 3);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(794, 354);
            tabControl.TabIndex = 5;
            // 
            // tabPageProtocol
            // 
            tabPageProtocol.Controls.Add(tableLayoutPanelProtocol);
            tabPageProtocol.Location = new Point(4, 24);
            tabPageProtocol.Name = "tabPageProtocol";
            tabPageProtocol.Size = new Size(786, 326);
            tabPageProtocol.TabIndex = 3;
            tabPageProtocol.Text = "Application Layer";
            tabPageProtocol.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanelProtocol
            // 
            tableLayoutPanelProtocol.ColumnCount = 8;
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36.9741173F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.003696F));
            tableLayoutPanelProtocol.Controls.Add(comboBoxVariables, 3, 1);
            tableLayoutPanelProtocol.Controls.Add(labelDictionary, 3, 0);
            tableLayoutPanelProtocol.Controls.Add(richTextBoxTx, 0, 2);
            tableLayoutPanelProtocol.Controls.Add(textBox3, 6, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox2, 5, 1);
            tableLayoutPanelProtocol.Controls.Add(comboBoxBoard, 1, 1);
            tableLayoutPanelProtocol.Controls.Add(labelBytes, 6, 0);
            tableLayoutPanelProtocol.Controls.Add(labelByte2, 5, 0);
            tableLayoutPanelProtocol.Controls.Add(labelByte1, 4, 0);
            tableLayoutPanelProtocol.Controls.Add(label4, 2, 0);
            tableLayoutPanelProtocol.Controls.Add(label2, 1, 0);
            tableLayoutPanelProtocol.Controls.Add(comboBoxMachine, 0, 1);
            tableLayoutPanelProtocol.Controls.Add(comboBoxCommand, 2, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox1, 4, 1);
            tableLayoutPanelProtocol.Controls.Add(label3, 0, 0);
            tableLayoutPanelProtocol.Controls.Add(buttonSendPS, 7, 3);
            tableLayoutPanelProtocol.Controls.Add(label12, 7, 2);
            tableLayoutPanelProtocol.Dock = DockStyle.Fill;
            tableLayoutPanelProtocol.Location = new Point(0, 0);
            tableLayoutPanelProtocol.Name = "tableLayoutPanelProtocol";
            tableLayoutPanelProtocol.RowCount = 4;
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 16.1987019F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 8.207344F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 53.9956856F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 21.5982742F));
            tableLayoutPanelProtocol.Size = new Size(786, 326);
            tableLayoutPanelProtocol.TabIndex = 2;
            // 
            // comboBoxVariables
            // 
            comboBoxVariables.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxVariables.FormattingEnabled = true;
            comboBoxVariables.Location = new Point(213, 55);
            comboBoxVariables.Name = "comboBoxVariables";
            comboBoxVariables.Size = new Size(64, 23);
            comboBoxVariables.TabIndex = 30;
            // 
            // labelDictionary
            // 
            labelDictionary.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelDictionary.AutoSize = true;
            labelDictionary.BackColor = Color.FromArgb(8, 72, 133);
            labelDictionary.Font = new Font("Poppins", 12F, FontStyle.Bold);
            labelDictionary.ForeColor = SystemColors.ControlLightLight;
            labelDictionary.Location = new Point(215, 5);
            labelDictionary.Margin = new Padding(5);
            labelDictionary.Name = "labelDictionary";
            labelDictionary.Size = new Size(60, 42);
            labelDictionary.TabIndex = 29;
            labelDictionary.Text = "Variable Name";
            labelDictionary.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // richTextBoxTx
            // 
            richTextBoxTx.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            richTextBoxTx.BackColor = SystemColors.Window;
            tableLayoutPanelProtocol.SetColumnSpan(richTextBoxTx, 7);
            richTextBoxTx.ForeColor = SystemColors.WindowText;
            richTextBoxTx.Location = new Point(3, 81);
            richTextBoxTx.Name = "richTextBoxTx";
            tableLayoutPanelProtocol.SetRowSpan(richTextBoxTx, 2);
            richTextBoxTx.Size = new Size(704, 242);
            richTextBoxTx.TabIndex = 28;
            richTextBoxTx.Text = "";
            // 
            // textBox3
            // 
            textBox3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanelProtocol.SetColumnSpan(textBox3, 2);
            textBox3.Location = new Point(423, 55);
            textBox3.MaxLength = 300;
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(360, 23);
            textBox3.TabIndex = 18;
            textBox3.WordWrap = false;
            textBox3.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox2
            // 
            textBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox2.Location = new Point(353, 55);
            textBox2.MaxLength = 2;
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(64, 23);
            textBox2.TabIndex = 17;
            textBox2.KeyPress += MaskedTextBox_KeyPress;
            // 
            // comboBoxBoard
            // 
            comboBoxBoard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxBoard.FormattingEnabled = true;
            comboBoxBoard.Location = new Point(73, 55);
            comboBoxBoard.Name = "comboBoxBoard";
            comboBoxBoard.Size = new Size(64, 23);
            comboBoxBoard.TabIndex = 15;
            comboBoxBoard.SelectedIndexChanged += comboBoxBoard_SelectedIndexChanged;
            // 
            // labelBytes
            // 
            labelBytes.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelBytes.AutoSize = true;
            labelBytes.BackColor = Color.FromArgb(8, 72, 133);
            tableLayoutPanelProtocol.SetColumnSpan(labelBytes, 2);
            labelBytes.Font = new Font("Poppins", 12F, FontStyle.Bold);
            labelBytes.ForeColor = SystemColors.ControlLightLight;
            labelBytes.Location = new Point(425, 5);
            labelBytes.Margin = new Padding(5);
            labelBytes.Name = "labelBytes";
            labelBytes.Size = new Size(356, 42);
            labelBytes.TabIndex = 9;
            labelBytes.Text = "Byte3 (HEX)";
            labelBytes.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelByte2
            // 
            labelByte2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelByte2.AutoSize = true;
            labelByte2.BackColor = Color.FromArgb(8, 72, 133);
            labelByte2.Font = new Font("Poppins", 12F, FontStyle.Bold);
            labelByte2.ForeColor = SystemColors.ControlLightLight;
            labelByte2.Location = new Point(355, 5);
            labelByte2.Margin = new Padding(5);
            labelByte2.Name = "labelByte2";
            labelByte2.Size = new Size(60, 42);
            labelByte2.TabIndex = 8;
            labelByte2.Text = "Byte2 (HEX)";
            labelByte2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelByte1
            // 
            labelByte1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelByte1.AutoSize = true;
            labelByte1.BackColor = Color.FromArgb(8, 72, 133);
            labelByte1.Font = new Font("Poppins", 12F, FontStyle.Bold);
            labelByte1.ForeColor = SystemColors.ControlLightLight;
            labelByte1.Location = new Point(285, 5);
            labelByte1.Margin = new Padding(5);
            labelByte1.Name = "labelByte1";
            labelByte1.Size = new Size(60, 42);
            labelByte1.TabIndex = 7;
            labelByte1.Text = "Byte1 (HEX)";
            labelByte1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label4.AutoSize = true;
            label4.BackColor = Color.FromArgb(8, 72, 133);
            label4.Font = new Font("Poppins", 12F, FontStyle.Bold);
            label4.ForeColor = SystemColors.ControlLightLight;
            label4.Location = new Point(145, 5);
            label4.Margin = new Padding(5);
            label4.Name = "label4";
            label4.Size = new Size(60, 42);
            label4.TabIndex = 6;
            label4.Text = "Comando";
            label4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.BackColor = Color.FromArgb(8, 72, 133);
            label2.Font = new Font("Poppins", 12F, FontStyle.Bold);
            label2.ForeColor = SystemColors.ControlLightLight;
            label2.Location = new Point(75, 5);
            label2.Margin = new Padding(5);
            label2.Name = "label2";
            label2.Size = new Size(60, 42);
            label2.TabIndex = 3;
            label2.Text = "Scheda";
            label2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // comboBoxMachine
            // 
            comboBoxMachine.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxMachine.FormattingEnabled = true;
            comboBoxMachine.Location = new Point(3, 55);
            comboBoxMachine.Name = "comboBoxMachine";
            comboBoxMachine.Size = new Size(64, 23);
            comboBoxMachine.TabIndex = 5;
            comboBoxMachine.SelectedIndexChanged += comboBoxMachine_SelectedIndexChanged;
            // 
            // comboBoxCommand
            // 
            comboBoxCommand.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxCommand.FormattingEnabled = true;
            comboBoxCommand.Location = new Point(143, 55);
            comboBoxCommand.Name = "comboBoxCommand";
            comboBoxCommand.Size = new Size(64, 23);
            comboBoxCommand.TabIndex = 14;
            comboBoxCommand.SelectedIndexChanged += comboBoxCommand_SelectedIndexChanged;
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Location = new Point(283, 55);
            textBox1.MaxLength = 2;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(64, 23);
            textBox1.TabIndex = 16;
            textBox1.KeyPress += MaskedTextBox_KeyPress;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.BackColor = Color.FromArgb(8, 72, 133);
            label3.Font = new Font("Poppins", 12F, FontStyle.Bold);
            label3.ForeColor = SystemColors.ControlLightLight;
            label3.Location = new Point(5, 5);
            label3.Margin = new Padding(5);
            label3.Name = "label3";
            label3.Size = new Size(60, 42);
            label3.TabIndex = 4;
            label3.Text = "Macchina";
            label3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // buttonSendPS
            // 
            buttonSendPS.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonSendPS.Font = new Font("Poppins", 12F, FontStyle.Bold);
            buttonSendPS.Location = new Point(713, 257);
            buttonSendPS.Name = "buttonSendPS";
            buttonSendPS.Size = new Size(70, 66);
            buttonSendPS.TabIndex = 27;
            buttonSendPS.Text = "Invia";
            buttonSendPS.UseVisualStyleBackColor = true;
            buttonSendPS.Click += buttonSendPS_Click;
            // 
            // label12
            // 
            label12.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label12.AutoSize = true;
            label12.BackColor = Color.FromArgb(8, 72, 133);
            label12.Font = new Font("Poppins", 12F, FontStyle.Bold);
            label12.ForeColor = SystemColors.ControlLightLight;
            label12.Location = new Point(715, 83);
            label12.Margin = new Padding(5);
            label12.Name = "label12";
            label12.Size = new Size(66, 166);
            label12.TabIndex = 23;
            label12.Text = "Indirizzo";
            label12.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tabPageCodeGen
            // 
            tabPageCodeGen.Controls.Add(tableLayoutPanel2);
            tabPageCodeGen.Location = new Point(4, 24);
            tabPageCodeGen.Name = "tabPageCodeGen";
            tabPageCodeGen.Padding = new Padding(3);
            tabPageCodeGen.Size = new Size(786, 326);
            tabPageCodeGen.TabIndex = 1;
            tabPageCodeGen.Text = "Code Gen";
            tabPageCodeGen.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 4;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));
            tableLayoutPanel2.Controls.Add(button1, 3, 3);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(3, 3);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 4;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableLayoutPanel2.Size = new Size(780, 320);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            button1.Image = (Image)resources.GetObject("button1.Image");
            button1.ImageAlign = ContentAlignment.MiddleLeft;
            button1.Location = new Point(633, 243);
            button1.Name = "button1";
            button1.Size = new Size(144, 74);
            button1.TabIndex = 0;
            button1.Text = "Generate code";
            button1.TextImageRelation = TextImageRelation.ImageBeforeText;
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // tabPageUART
            // 
            tabPageUART.Controls.Add(label1);
            tabPageUART.Controls.Add(listBoxSerialPorts);
            tabPageUART.Location = new Point(4, 24);
            tabPageUART.Name = "tabPageUART";
            tabPageUART.Padding = new Padding(3);
            tabPageUART.Size = new Size(786, 326);
            tabPageUART.TabIndex = 2;
            tabPageUART.Text = "UART";
            tabPageUART.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label1.Location = new Point(590, 6);
            label1.Name = "label1";
            label1.Size = new Size(150, 15);
            label1.TabIndex = 1;
            label1.Text = "SERIAL PORTS AVAILABLE";
            // 
            // listBoxSerialPorts
            // 
            listBoxSerialPorts.FormattingEnabled = true;
            listBoxSerialPorts.ItemHeight = 15;
            listBoxSerialPorts.Location = new Point(556, 21);
            listBoxSerialPorts.Name = "listBoxSerialPorts";
            listBoxSerialPorts.Size = new Size(197, 169);
            listBoxSerialPorts.TabIndex = 0;
            // 
            // terminalOut
            // 
            terminalOut.BackColor = SystemColors.WindowFrame;
            terminalOut.Dock = DockStyle.Fill;
            terminalOut.ForeColor = SystemColors.Info;
            terminalOut.Location = new Point(3, 363);
            terminalOut.Name = "terminalOut";
            terminalOut.Size = new Size(794, 84);
            terminalOut.TabIndex = 4;
            terminalOut.Text = "";
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripSplitButton2, PCanLabel, toolStripStatusLabel2, BLEStatusLabel, toolStripStatusLabel1 });
            statusStrip1.Location = new Point(0, 428);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(800, 22);
            statusStrip1.TabIndex = 3;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripSplitButton2
            // 
            toolStripSplitButton2.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripSplitButton2.DropDownItems.AddRange(new ToolStripItem[] { ChannelMenu });
            toolStripSplitButton2.Image = STEMPM.Properties.Resources.ic_fluent_serial_port_24_filled;
            toolStripSplitButton2.ImageTransparentColor = Color.Magenta;
            toolStripSplitButton2.Name = "toolStripSplitButton2";
            toolStripSplitButton2.Size = new Size(32, 20);
            toolStripSplitButton2.Text = "toolStripSplitButton2";
            // 
            // ChannelMenu
            // 
            ChannelMenu.DropDownItems.AddRange(new ToolStripItem[] { cANToolStripMenuItem, bluetoothLEToolStripMenuItem });
            ChannelMenu.Name = "ChannelMenu";
            ChannelMenu.Size = new Size(180, 22);
            ChannelMenu.Text = "Comm Type";
            // 
            // cANToolStripMenuItem
            // 
            cANToolStripMenuItem.Name = "cANToolStripMenuItem";
            cANToolStripMenuItem.Size = new Size(141, 22);
            cANToolStripMenuItem.Text = "CAN";
            cANToolStripMenuItem.Click += cANToolStripMenuItem_Click;
            // 
            // bluetoothLEToolStripMenuItem
            // 
            bluetoothLEToolStripMenuItem.Name = "bluetoothLEToolStripMenuItem";
            bluetoothLEToolStripMenuItem.Size = new Size(141, 22);
            bluetoothLEToolStripMenuItem.Text = "Bluetooth LE";
            bluetoothLEToolStripMenuItem.Click += bluetoothLEToolStripMenuItem_Click;
            // 
            // PCanLabel
            // 
            PCanLabel.BackColor = Color.Salmon;
            PCanLabel.Name = "PCanLabel";
            PCanLabel.Size = new Size(126, 17);
            PCanLabel.Text = "PCAN: Not Connected";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(13, 17);
            toolStripStatusLabel2.Text = "  ";
            // 
            // BLEStatusLabel
            // 
            BLEStatusLabel.BackColor = Color.Salmon;
            BLEStatusLabel.Name = "BLEStatusLabel";
            BLEStatusLabel.Size = new Size(113, 17);
            BLEStatusLabel.Text = "BLE: Not Connected";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(10, 17);
            toolStripStatusLabel1.Text = " ";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(statusStrip1);
            Controls.Add(tableLayoutPanel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "STEM Device Manager ";
            WindowState = FormWindowState.Maximized;
            tableLayoutPanel1.ResumeLayout(false);
            tabControl.ResumeLayout(false);
            tabPageProtocol.ResumeLayout(false);
            tableLayoutPanelProtocol.ResumeLayout(false);
            tableLayoutPanelProtocol.PerformLayout();
            tabPageCodeGen.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            tabPageUART.ResumeLayout(false);
            tabPageUART.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Timer timerBaseTime;
        private TableLayoutPanel tableLayoutPanel1;
        private TabControl tabControl;
        private TabPage tabPageCodeGen;
        private TabPage tabPageUART;
        private Label label1;
        private ListBox listBoxSerialPorts;
        private RichTextBox terminalOut;
        private TabPage tabPageProtocol;
        private TableLayoutPanel tableLayoutPanel2;
        private Button button1;
        private TableLayoutPanel tableLayoutPanelProtocol;
        private Label label3;
        private Label label2;
        private ComboBox comboBoxMachine;
        private ComboBox comboBoxBoard;
        private Label labelByte2;
        private Label labelByte1;
        private Label label4;
        private ComboBox comboBoxCommand;
        private TextBox textBox1;
        private TextBox textBox2;
        private Label label12;
        private Button buttonSendPS;
        private RichTextBox richTextBoxTx;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel PCanLabel;
        private ComboBox comboBoxVariables;
        private Label labelDictionary;
        private TextBox textBox3;
        private Label labelBytes;
        private ToolStripStatusLabel BLEStatusLabel;
        private ToolStripSplitButton toolStripSplitButton1;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripSplitButton toolStripSplitButton2;
        private ToolStripMenuItem ChannelMenu;
        private ToolStripMenuItem cANToolStripMenuItem;
        private ToolStripMenuItem bluetoothLEToolStripMenuItem;
    }
}
