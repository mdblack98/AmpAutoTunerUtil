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
            this.button1 = new System.Windows.Forms.Button();
            this.labelVFO = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.textBoxFrequencyA = new System.Windows.Forms.TextBox();
            this.textBoxFrequencyB = new System.Windows.Forms.TextBox();
            this.labelVFOB = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(13, 13);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Connect";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // labelVFO
            // 
            this.labelVFO.AutoSize = true;
            this.labelVFO.Location = new System.Drawing.Point(13, 43);
            this.labelVFO.Name = "labelVFO";
            this.labelVFO.Size = new System.Drawing.Size(35, 13);
            this.labelVFO.TabIndex = 1;
            this.labelVFO.Text = "VFOA";
            this.labelVFO.Click += new System.EventHandler(this.labelVFOA_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 300;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // textBoxFrequencyA
            // 
            this.textBoxFrequencyA.Location = new System.Drawing.Point(49, 40);
            this.textBoxFrequencyA.Name = "textBoxFrequencyA";
            this.textBoxFrequencyA.Size = new System.Drawing.Size(100, 20);
            this.textBoxFrequencyA.TabIndex = 2;
            this.textBoxFrequencyA.TextChanged += new System.EventHandler(this.textBoxFrequencyA_TextChanged);
            this.textBoxFrequencyA.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textBoxFrequencyA_KeyUp);
            // 
            // textBoxFrequencyB
            // 
            this.textBoxFrequencyB.AcceptsReturn = true;
            this.textBoxFrequencyB.Location = new System.Drawing.Point(49, 67);
            this.textBoxFrequencyB.Name = "textBoxFrequencyB";
            this.textBoxFrequencyB.Size = new System.Drawing.Size(100, 20);
            this.textBoxFrequencyB.TabIndex = 3;
            this.textBoxFrequencyB.TextChanged += new System.EventHandler(this.textBoxFrequencyB_TextChanged);
            this.textBoxFrequencyB.Enter += new System.EventHandler(this.textBoxFrequencyB_Enter);
            this.textBoxFrequencyB.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textBoxFrequencyB_KeyUp);
            // 
            // labelVFOB
            // 
            this.labelVFOB.AutoSize = true;
            this.labelVFOB.Location = new System.Drawing.Point(13, 70);
            this.labelVFOB.Name = "labelVFOB";
            this.labelVFOB.Size = new System.Drawing.Size(35, 13);
            this.labelVFOB.TabIndex = 4;
            this.labelVFOB.Text = "VFOB";
            this.labelVFOB.Click += new System.EventHandler(this.labelVFOB_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.labelVFOB);
            this.Controls.Add(this.textBoxFrequencyB);
            this.Controls.Add(this.textBoxFrequencyA);
            this.Controls.Add(this.labelVFO);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "RigTest";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label labelVFO;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TextBox textBoxFrequencyA;
        private System.Windows.Forms.TextBox textBoxFrequencyB;
        private System.Windows.Forms.Label labelVFOB;
    }
}

