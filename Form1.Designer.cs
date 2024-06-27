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
            tabControl = new TabControl();
            tabPage1 = new TabPage();
            terminalOut = new RichTextBox();
            tabPage2 = new TabPage();
            tabPage3 = new TabPage();
            label1 = new Label();
            listBoxSerialPorts = new ListBox();
            timerBaseTime = new System.Windows.Forms.Timer(components);
            tabControl.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage3.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabPage1);
            tabControl.Controls.Add(tabPage2);
            tabControl.Controls.Add(tabPage3);
            tabControl.Location = new Point(12, 12);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(776, 426);
            tabControl.TabIndex = 1;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(terminalOut);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(768, 398);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Debug";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // terminalOut
            // 
            terminalOut.BackColor = SystemColors.WindowFrame;
            terminalOut.Dock = DockStyle.Fill;
            terminalOut.ForeColor = SystemColors.Info;
            terminalOut.Location = new Point(3, 3);
            terminalOut.Name = "terminalOut";
            terminalOut.Size = new Size(762, 392);
            terminalOut.TabIndex = 0;
            terminalOut.Text = "";
            // 
            // tabPage2
            // 
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(768, 398);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Init";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(label1);
            tabPage3.Controls.Add(listBoxSerialPorts);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(768, 398);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "UART";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label1.Location = new Point(587, 3);
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
            listBoxSerialPorts.SelectedIndexChanged += listBoxSerialPorts_SelectedIndexChanged;
            // 
            // timerBaseTime
            // 
            timerBaseTime.Tick += timerBaseTime_Tick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "STEM Protocol Companion";
            tabControl.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private RichTextBox terminalOut;
        private System.Windows.Forms.Timer timerBaseTime;
        private TabPage tabPage3;
        private Label label1;
        private ListBox listBoxSerialPorts;
    }
}
