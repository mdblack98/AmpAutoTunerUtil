namespace TunerTest
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
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.button_Tune = new System.Windows.Forms.Button();
            this.button_Open = new System.Windows.Forms.Button();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.buttonAmpOff = new System.Windows.Forms.Button();
            this.buttonAmpOn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "LDG",
            "MFJ-998"});
            this.comboBox1.Location = new System.Drawing.Point(22, 13);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(121, 24);
            this.comboBox1.TabIndex = 0;
            // 
            // button_Tune
            // 
            this.button_Tune.Location = new System.Drawing.Point(22, 84);
            this.button_Tune.Name = "button_Tune";
            this.button_Tune.Size = new System.Drawing.Size(75, 23);
            this.button_Tune.TabIndex = 1;
            this.button_Tune.Text = "Tune";
            this.button_Tune.UseVisualStyleBackColor = true;
            this.button_Tune.Click += new System.EventHandler(this.Button_Tune_Click);
            // 
            // button_Open
            // 
            this.button_Open.Location = new System.Drawing.Point(22, 55);
            this.button_Open.Name = "button_Open";
            this.button_Open.Size = new System.Drawing.Size(75, 23);
            this.button_Open.TabIndex = 2;
            this.button_Open.Text = "Open";
            this.button_Open.UseVisualStyleBackColor = true;
            this.button_Open.Click += new System.EventHandler(this.Button_Open_Click);
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(187, 13);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(601, 425);
            this.richTextBox1.TabIndex = 3;
            this.richTextBox1.Text = "";
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // buttonAmpOff
            // 
            this.buttonAmpOff.Location = new System.Drawing.Point(22, 114);
            this.buttonAmpOff.Name = "buttonAmpOff";
            this.buttonAmpOff.Size = new System.Drawing.Size(75, 23);
            this.buttonAmpOff.TabIndex = 4;
            this.buttonAmpOff.Text = "Amp Off";
            this.buttonAmpOff.UseVisualStyleBackColor = true;
            this.buttonAmpOff.Click += new System.EventHandler(this.ButtonAmpOff_Click);
            // 
            // buttonAmpOn
            // 
            this.buttonAmpOn.Location = new System.Drawing.Point(22, 143);
            this.buttonAmpOn.Name = "buttonAmpOn";
            this.buttonAmpOn.Size = new System.Drawing.Size(75, 23);
            this.buttonAmpOn.TabIndex = 5;
            this.buttonAmpOn.Text = "Amp On";
            this.buttonAmpOn.UseVisualStyleBackColor = true;
            this.buttonAmpOn.Click += new System.EventHandler(this.ButtonAmpOn_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.buttonAmpOn);
            this.Controls.Add(this.buttonAmpOff);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.button_Open);
            this.Controls.Add(this.button_Tune);
            this.Controls.Add(this.comboBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button button_Tune;
        private System.Windows.Forms.Button button_Open;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button buttonAmpOff;
        private System.Windows.Forms.Button buttonAmpOn;
    }
}

