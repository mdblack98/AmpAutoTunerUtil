using System.Windows.Forms;
using System;

public partial class CustomMessageBoxForm : Form
{
    public CustomMessageBoxForm(string message, string title)
    {
        InitializeComponent();
        this.Text = title;
        this.labelMessage.Text = message;
    }

    private void buttonOK_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}