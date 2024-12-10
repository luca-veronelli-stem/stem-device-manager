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
            tabPageCodeGen = new TabPage();
            tableLayoutPanel2 = new TableLayoutPanel();
            button1 = new Button();
            tabPageProtocol = new TabPage();
            tableLayoutPanelProtocol = new TableLayoutPanel();
            textBoxAddress = new TextBox();
            textBox7 = new TextBox();
            textBox6 = new TextBox();
            textBox5 = new TextBox();
            textBox4 = new TextBox();
            textBox3 = new TextBox();
            textBox2 = new TextBox();
            comboBoxBoard = new ComboBox();
            label11 = new Label();
            label10 = new Label();
            label9 = new Label();
            label8 = new Label();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            label4 = new Label();
            label2 = new Label();
            comboBoxMachine = new ComboBox();
            comboBoxCommand = new ComboBox();
            textBox1 = new TextBox();
            label12 = new Label();
            label13 = new Label();
            richTextBox1 = new RichTextBox();
            label3 = new Label();
            buttonSendPS = new Button();
            tabPageUART = new TabPage();
            label1 = new Label();
            listBoxSerialPorts = new ListBox();
            terminalOut = new RichTextBox();
            tableLayoutPanel1.SuspendLayout();
            tabControl.SuspendLayout();
            tabPageCodeGen.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tabPageProtocol.SuspendLayout();
            tableLayoutPanelProtocol.SuspendLayout();
            tabPageUART.SuspendLayout();
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
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabPageCodeGen);
            tabControl.Controls.Add(tabPageProtocol);
            tabControl.Controls.Add(tabPageUART);
            tabControl.Location = new Point(3, 3);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(794, 354);
            tabControl.TabIndex = 5;
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
            // tabPageProtocol
            // 
            tabPageProtocol.Controls.Add(tableLayoutPanelProtocol);
            tabPageProtocol.Location = new Point(4, 24);
            tabPageProtocol.Name = "tabPageProtocol";
            tabPageProtocol.Size = new Size(786, 326);
            tabPageProtocol.TabIndex = 3;
            tabPageProtocol.Text = "Protocol";
            tabPageProtocol.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanelProtocol
            // 
            tableLayoutPanelProtocol.ColumnCount = 10;
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelProtocol.Controls.Add(textBoxAddress, 1, 2);
            tableLayoutPanelProtocol.Controls.Add(textBox7, 9, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox6, 8, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox5, 7, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox4, 6, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox3, 5, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox2, 4, 1);
            tableLayoutPanelProtocol.Controls.Add(comboBoxBoard, 1, 1);
            tableLayoutPanelProtocol.Controls.Add(label11, 9, 0);
            tableLayoutPanelProtocol.Controls.Add(label10, 8, 0);
            tableLayoutPanelProtocol.Controls.Add(label9, 7, 0);
            tableLayoutPanelProtocol.Controls.Add(label8, 6, 0);
            tableLayoutPanelProtocol.Controls.Add(label7, 5, 0);
            tableLayoutPanelProtocol.Controls.Add(label6, 4, 0);
            tableLayoutPanelProtocol.Controls.Add(label5, 3, 0);
            tableLayoutPanelProtocol.Controls.Add(label4, 2, 0);
            tableLayoutPanelProtocol.Controls.Add(label2, 1, 0);
            tableLayoutPanelProtocol.Controls.Add(comboBoxMachine, 0, 1);
            tableLayoutPanelProtocol.Controls.Add(comboBoxCommand, 2, 1);
            tableLayoutPanelProtocol.Controls.Add(textBox1, 3, 1);
            tableLayoutPanelProtocol.Controls.Add(label12, 0, 2);
            tableLayoutPanelProtocol.Controls.Add(label13, 0, 3);
            tableLayoutPanelProtocol.Controls.Add(richTextBox1, 1, 3);
            tableLayoutPanelProtocol.Controls.Add(label3, 0, 0);
            tableLayoutPanelProtocol.Controls.Add(buttonSendPS, 9, 4);
            tableLayoutPanelProtocol.Dock = DockStyle.Fill;
            tableLayoutPanelProtocol.Location = new Point(0, 0);
            tableLayoutPanelProtocol.Name = "tableLayoutPanelProtocol";
            tableLayoutPanelProtocol.RowCount = 5;
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanelProtocol.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanelProtocol.Size = new Size(786, 326);
            tableLayoutPanelProtocol.TabIndex = 2;
            // 
            // textBoxAddress
            // 
            textBoxAddress.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxAddress.Location = new Point(81, 133);
            textBoxAddress.MaxLength = 2;
            textBoxAddress.Name = "textBoxAddress";
            textBoxAddress.Size = new Size(72, 23);
            textBoxAddress.TabIndex = 24;
            // 
            // textBox7
            // 
            textBox7.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox7.Location = new Point(705, 68);
            textBox7.MaxLength = 2;
            textBox7.Name = "textBox7";
            textBox7.Size = new Size(78, 23);
            textBox7.TabIndex = 22;
            textBox7.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox6
            // 
            textBox6.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox6.Location = new Point(627, 68);
            textBox6.MaxLength = 2;
            textBox6.Name = "textBox6";
            textBox6.Size = new Size(72, 23);
            textBox6.TabIndex = 21;
            textBox6.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox5
            // 
            textBox5.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox5.Location = new Point(549, 68);
            textBox5.MaxLength = 2;
            textBox5.Name = "textBox5";
            textBox5.Size = new Size(72, 23);
            textBox5.TabIndex = 20;
            textBox5.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox4
            // 
            textBox4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox4.Location = new Point(471, 68);
            textBox4.MaxLength = 2;
            textBox4.Name = "textBox4";
            textBox4.Size = new Size(72, 23);
            textBox4.TabIndex = 19;
            textBox4.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox3
            // 
            textBox3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox3.Location = new Point(393, 68);
            textBox3.MaxLength = 2;
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(72, 23);
            textBox3.TabIndex = 18;
            textBox3.KeyPress += MaskedTextBox_KeyPress;
            // 
            // textBox2
            // 
            textBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox2.Location = new Point(315, 68);
            textBox2.MaxLength = 2;
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(72, 23);
            textBox2.TabIndex = 17;
            textBox2.KeyPress += MaskedTextBox_KeyPress;
            // 
            // comboBoxBoard
            // 
            comboBoxBoard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxBoard.FormattingEnabled = true;
            comboBoxBoard.Location = new Point(81, 68);
            comboBoxBoard.Name = "comboBoxBoard";
            comboBoxBoard.Size = new Size(72, 23);
            comboBoxBoard.TabIndex = 15;
            comboBoxBoard.SelectedIndexChanged += comboBoxBoard_SelectedIndexChanged;
            // 
            // label11
            // 
            label11.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label11.AutoSize = true;
            label11.BackColor = Color.RoyalBlue;
            label11.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label11.ForeColor = SystemColors.ControlLightLight;
            label11.Location = new Point(707, 5);
            label11.Margin = new Padding(5);
            label11.Name = "label11";
            label11.Size = new Size(74, 55);
            label11.TabIndex = 13;
            label11.Text = "Byte7 (HEX)";
            label11.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label10
            // 
            label10.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label10.AutoSize = true;
            label10.BackColor = Color.RoyalBlue;
            label10.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label10.ForeColor = SystemColors.ControlLightLight;
            label10.Location = new Point(629, 5);
            label10.Margin = new Padding(5);
            label10.Name = "label10";
            label10.Size = new Size(68, 55);
            label10.TabIndex = 12;
            label10.Text = "Byte6 (HEX)";
            label10.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label9
            // 
            label9.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label9.AutoSize = true;
            label9.BackColor = Color.RoyalBlue;
            label9.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label9.ForeColor = SystemColors.ControlLightLight;
            label9.Location = new Point(551, 5);
            label9.Margin = new Padding(5);
            label9.Name = "label9";
            label9.Size = new Size(68, 55);
            label9.TabIndex = 11;
            label9.Text = "Byte5 (HEX)";
            label9.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label8
            // 
            label8.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label8.AutoSize = true;
            label8.BackColor = Color.RoyalBlue;
            label8.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label8.ForeColor = SystemColors.ControlLightLight;
            label8.Location = new Point(473, 5);
            label8.Margin = new Padding(5);
            label8.Name = "label8";
            label8.Size = new Size(68, 55);
            label8.TabIndex = 10;
            label8.Text = "Byte4 (HEX)";
            label8.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            label7.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label7.AutoSize = true;
            label7.BackColor = Color.RoyalBlue;
            label7.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label7.ForeColor = SystemColors.ControlLightLight;
            label7.Location = new Point(395, 5);
            label7.Margin = new Padding(5);
            label7.Name = "label7";
            label7.Size = new Size(68, 55);
            label7.TabIndex = 9;
            label7.Text = "Byte3 (HEX)";
            label7.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            label6.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label6.AutoSize = true;
            label6.BackColor = Color.RoyalBlue;
            label6.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label6.ForeColor = SystemColors.ControlLightLight;
            label6.Location = new Point(317, 5);
            label6.Margin = new Padding(5);
            label6.Name = "label6";
            label6.Size = new Size(68, 55);
            label6.TabIndex = 8;
            label6.Text = "Byte2 (HEX)";
            label6.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label5.AutoSize = true;
            label5.BackColor = Color.RoyalBlue;
            label5.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label5.ForeColor = SystemColors.ControlLightLight;
            label5.Location = new Point(239, 5);
            label5.Margin = new Padding(5);
            label5.Name = "label5";
            label5.Size = new Size(68, 55);
            label5.TabIndex = 7;
            label5.Text = "Byte1 (HEX)";
            label5.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label4.AutoSize = true;
            label4.BackColor = Color.RoyalBlue;
            label4.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label4.ForeColor = SystemColors.ControlLightLight;
            label4.Location = new Point(161, 5);
            label4.Margin = new Padding(5);
            label4.Name = "label4";
            label4.Size = new Size(68, 55);
            label4.TabIndex = 6;
            label4.Text = "Comando";
            label4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.BackColor = Color.RoyalBlue;
            label2.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label2.ForeColor = SystemColors.ControlLightLight;
            label2.Location = new Point(83, 5);
            label2.Margin = new Padding(5);
            label2.Name = "label2";
            label2.Size = new Size(68, 55);
            label2.TabIndex = 3;
            label2.Text = "Scheda";
            label2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // comboBoxMachine
            // 
            comboBoxMachine.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxMachine.FormattingEnabled = true;
            comboBoxMachine.Location = new Point(3, 68);
            comboBoxMachine.Name = "comboBoxMachine";
            comboBoxMachine.Size = new Size(72, 23);
            comboBoxMachine.TabIndex = 5;
            comboBoxMachine.SelectedIndexChanged += comboBoxMachine_SelectedIndexChanged;
            // 
            // comboBoxCommand
            // 
            comboBoxCommand.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxCommand.FormattingEnabled = true;
            comboBoxCommand.Location = new Point(159, 68);
            comboBoxCommand.Name = "comboBoxCommand";
            comboBoxCommand.Size = new Size(72, 23);
            comboBoxCommand.TabIndex = 14;
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Location = new Point(237, 68);
            textBox1.MaxLength = 2;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(72, 23);
            textBox1.TabIndex = 16;
            textBox1.KeyPress += MaskedTextBox_KeyPress;
            // 
            // label12
            // 
            label12.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label12.AutoSize = true;
            label12.BackColor = Color.RoyalBlue;
            label12.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label12.ForeColor = SystemColors.ControlLightLight;
            label12.Location = new Point(5, 135);
            label12.Margin = new Padding(5);
            label12.Name = "label12";
            label12.Size = new Size(68, 55);
            label12.TabIndex = 23;
            label12.Text = "Indirizzo";
            label12.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label13
            // 
            label13.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label13.AutoSize = true;
            label13.BackColor = Color.RoyalBlue;
            label13.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label13.ForeColor = SystemColors.ControlLightLight;
            label13.Location = new Point(5, 200);
            label13.Margin = new Padding(5);
            label13.Name = "label13";
            label13.Size = new Size(68, 55);
            label13.TabIndex = 25;
            label13.Text = "Pacchetto";
            label13.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // richTextBox1
            // 
            richTextBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanelProtocol.SetColumnSpan(richTextBox1, 9);
            richTextBox1.Location = new Point(81, 198);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(702, 59);
            richTextBox1.TabIndex = 26;
            richTextBox1.Text = "";
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.BackColor = Color.RoyalBlue;
            label3.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label3.ForeColor = SystemColors.ControlLightLight;
            label3.Location = new Point(5, 5);
            label3.Margin = new Padding(5);
            label3.Name = "label3";
            label3.Size = new Size(68, 55);
            label3.TabIndex = 4;
            label3.Text = "Macchina";
            label3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // buttonSendPS
            // 
            buttonSendPS.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonSendPS.Location = new Point(705, 263);
            buttonSendPS.Name = "buttonSendPS";
            buttonSendPS.Size = new Size(78, 60);
            buttonSendPS.TabIndex = 27;
            buttonSendPS.Text = "Invia";
            buttonSendPS.UseVisualStyleBackColor = true;
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tableLayoutPanel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "STEM Protocol Manager";
            WindowState = FormWindowState.Maximized;
            tableLayoutPanel1.ResumeLayout(false);
            tabControl.ResumeLayout(false);
            tabPageCodeGen.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            tabPageProtocol.ResumeLayout(false);
            tableLayoutPanelProtocol.ResumeLayout(false);
            tableLayoutPanelProtocol.PerformLayout();
            tabPageUART.ResumeLayout(false);
            tabPageUART.PerformLayout();
            ResumeLayout(false);
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
        private Label label11;
        private Label label10;
        private Label label9;
        private Label label8;
        private Label label7;
        private Label label6;
        private Label label5;
        private Label label4;
        private ComboBox comboBoxCommand;
        private TextBox textBox1;
        private TextBox textBox7;
        private TextBox textBox6;
        private TextBox textBox5;
        private TextBox textBox4;
        private TextBox textBox3;
        private TextBox textBox2;
        private TextBox textBoxAddress;
        private Label label12;
        private Label label13;
        private RichTextBox richTextBox1;
        private Button buttonSendPS;
    }
}
