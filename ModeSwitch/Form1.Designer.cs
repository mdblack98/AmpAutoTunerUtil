namespace ModeSwitch
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
            this.buttonMode1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // buttonMode1
            // 
            this.buttonMode1.Location = new System.Drawing.Point(13, 13);
            this.buttonMode1.Name = "buttonMode1";
            this.buttonMode1.Size = new System.Drawing.Size(75, 23);
            this.buttonMode1.TabIndex = 0;
            this.buttonMode1.Text = "Mode 1";
            this.buttonMode1.UseVisualStyleBackColor = true;
            this.buttonMode1.Click += new System.EventHandler(this.ButtonMode1_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(779, 449);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(8, 22);
            this.textBox1.TabIndex = 1;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(272, 61);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.buttonMode1);
            this.Name = "Form1";
            this.Text = "ModeSwitch 0.1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonMode1;
        private System.Windows.Forms.TextBox textBox1;
    }
}

