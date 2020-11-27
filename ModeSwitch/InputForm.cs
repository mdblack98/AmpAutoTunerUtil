using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModeSwitch
{
        /// <summary>
        /// Display class for InputBox.  Should not be used directly, use InputBox instead.
        /// </summary>
        internal partial class InputForm : Form
        {
            #region Constructors

            /// <summary>
            /// Default constructor.
            /// </summary>
            internal InputForm()
            {
                InitializeComponent();
            }

            
            /// <summary>
            /// Parameterized constructor.
            /// </summary>
            /// <param name="caption">Title for the Input form.</param>
            /// <param name="defaultValue">Default to be displayed in the textbox.</param>
            internal InputForm(string caption, string defaultValue) : this()
            {
                this.Text = caption;
                txtValue.Text = defaultValue;
            }

            #endregion Constructors

            #region Public Properties

            /// <summary>
            /// Accessor for the textbox value.
            /// </summary>
            internal string StringValue
            {
                get { return txtValue.Text; }
                set { txtValue.Text = value; }
            }

        #endregion Public Properties 

        private void Button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
