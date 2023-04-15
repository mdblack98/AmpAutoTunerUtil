namespace RigTest
{
    partial class Form1
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.labelVFO = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.textBoxFrequencyA = new System.Windows.Forms.TextBox();
            this.textBoxFrequencyB = new System.Windows.Forms.TextBox();
            this.labelVFOB = new System.Windows.Forms.Label();
            this.buttonPTT = new System.Windows.Forms.Button();
            this.comboBoxModeA = new System.Windows.Forms.ComboBox();
            this.comboBoxModeB = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // buttonConnect
            // 
            this.buttonConnect.Location = new System.Drawing.Point(13, 13);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(75, 23);
            this.buttonConnect.TabIndex = 0;
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.UseVisualStyleBackColor = true;
            this.buttonConnect.Click += new System.EventHandler(this.Button1_Click);
            // 
            // labelVFO
            // 
            this.labelVFO.AutoSize = true;
            this.labelVFO.Location = new System.Drawing.Point(13, 50);
            this.labelVFO.Name = "labelVFO";
            this.labelVFO.Size = new System.Drawing.Size(35, 13);
            this.labelVFO.TabIndex = 1;
            this.labelVFO.Text = "VFOA";
            this.labelVFO.Click += new System.EventHandler(this.LabelVFOA_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 300;
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // textBoxFrequencyA
            // 
            this.textBoxFrequencyA.Location = new System.Drawing.Point(49, 47);
            this.textBoxFrequencyA.Name = "textBoxFrequencyA";
            this.textBoxFrequencyA.Size = new System.Drawing.Size(100, 20);
            this.textBoxFrequencyA.TabIndex = 2;
            this.textBoxFrequencyA.TextChanged += new System.EventHandler(this.TextBoxFrequencyA_TextChanged);
            this.textBoxFrequencyA.KeyUp += new System.Windows.Forms.KeyEventHandler(this.TextBoxFrequencyA_KeyUp);
            // 
            // textBoxFrequencyB
            // 
            this.textBoxFrequencyB.AcceptsReturn = true;
            this.textBoxFrequencyB.Location = new System.Drawing.Point(49, 74);
            this.textBoxFrequencyB.Name = "textBoxFrequencyB";
            this.textBoxFrequencyB.Size = new System.Drawing.Size(100, 20);
            this.textBoxFrequencyB.TabIndex = 3;
            this.textBoxFrequencyB.TextChanged += new System.EventHandler(this.TextBoxFrequencyB_TextChanged);
            this.textBoxFrequencyB.Enter += new System.EventHandler(this.TextBoxFrequencyB_Enter);
            this.textBoxFrequencyB.KeyUp += new System.Windows.Forms.KeyEventHandler(this.TextBoxFrequencyB_KeyUp);
            // 
            // labelVFOB
            // 
            this.labelVFOB.AutoSize = true;
            this.labelVFOB.Location = new System.Drawing.Point(13, 77);
            this.labelVFOB.Name = "labelVFOB";
            this.labelVFOB.Size = new System.Drawing.Size(35, 13);
            this.labelVFOB.TabIndex = 4;
            this.labelVFOB.Text = "VFOB";
            this.labelVFOB.Click += new System.EventHandler(this.LabelVFOB_Click);
            // 
            // buttonPTT
            // 
            this.buttonPTT.Location = new System.Drawing.Point(14, 104);
            this.buttonPTT.Name = "buttonPTT";
            this.buttonPTT.Size = new System.Drawing.Size(75, 23);
            this.buttonPTT.TabIndex = 5;
            this.buttonPTT.Text = "PTT";
            this.buttonPTT.UseVisualStyleBackColor = true;
            this.buttonPTT.Click += new System.EventHandler(this.ButtonPTT_Click);
            // 
            // comboBoxModeA
            // 
            this.comboBoxModeA.FormattingEnabled = true;
            this.comboBoxModeA.Location = new System.Drawing.Point(155, 47);
            this.comboBoxModeA.Name = "comboBoxModeA";
            this.comboBoxModeA.Size = new System.Drawing.Size(75, 21);
            this.comboBoxModeA.TabIndex = 6;
            this.comboBoxModeA.DropDown += new System.EventHandler(this.ComboBoxModeA_DropDown);
            this.comboBoxModeA.SelectedIndexChanged += new System.EventHandler(this.ComboBoxModeA_SelectedIndexChanged);
            this.comboBoxModeA.DropDownClosed += new System.EventHandler(this.ComboBoxModeA_DropDownClosed);
            // 
            // comboBoxModeB
            // 
            this.comboBoxModeB.FormattingEnabled = true;
            this.comboBoxModeB.Location = new System.Drawing.Point(155, 73);
            this.comboBoxModeB.Name = "comboBoxModeB";
            this.comboBoxModeB.Size = new System.Drawing.Size(75, 21);
            this.comboBoxModeB.TabIndex = 7;
            this.comboBoxModeB.DropDown += new System.EventHandler(this.ComboBoxModeB_DropDown);
            this.comboBoxModeB.SelectedIndexChanged += new System.EventHandler(this.ComboBoxModeB_SelectedIndexChanged);
            this.comboBoxModeB.DropDownClosed += new System.EventHandler(this.ComboBoxModeB_DropDownClosed);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.comboBoxModeB);
            this.Controls.Add(this.comboBoxModeA);
            this.Controls.Add(this.buttonPTT);
            this.Controls.Add(this.labelVFOB);
            this.Controls.Add(this.textBoxFrequencyB);
            this.Controls.Add(this.textBoxFrequencyA);
            this.Controls.Add(this.labelVFO);
            this.Controls.Add(this.buttonConnect);
            this.Name = "Form1";
            this.Text = "RigTest";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.Label labelVFO;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TextBox textBoxFrequencyA;
        private System.Windows.Forms.TextBox textBoxFrequencyB;
        private System.Windows.Forms.Label labelVFOB;
        private System.Windows.Forms.Button buttonPTT;
        private System.Windows.Forms.ComboBox comboBoxModeA;
        private System.Windows.Forms.ComboBox comboBoxModeB;
    }
}

