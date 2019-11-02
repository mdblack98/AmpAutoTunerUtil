using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModeSwitch
{
    /// <summary>
    /// Input dialog box used for simple user data entry.
    /// </summary>
    public static class InputBox
    {
        /// <summary>
        /// Standard modal DialogResult method used for simple user input (as a string).
        /// </summary>
        /// <param name="caption">Title for the Input form.</param>
        /// <param name="defaultValue">Default to be displayed in the textbox.</param>
        /// <returns>DialogResult, and updates the value of reference parameter 
        /// defaultValue if the result is DialogResult.OK.</returns>
        public static DialogResult ShowDialog(string caption, ref string defaultValue)
        {
            using (InputForm inForm = new InputForm(caption, defaultValue))
            {
                if (inForm.ShowDialog() == DialogResult.OK)
                {
                    defaultValue = inForm.StringValue;
                    return DialogResult.OK;
                }
                return DialogResult.Cancel;
            }
        }

        /// <summary>
        /// Prompts the user to provide a value as a double.
        /// </summary>
        /// <param name="caption">Title for the Input form.</param>
        /// <param name="defaultValue">Default to be displayed in the textbox.</param>
        /// <returns>Nullable value: double.</returns>
        public static double? GetDouble(string caption, string defaultValue)
        {
            using (InputForm inForm = new InputForm(caption, defaultValue))
            {
                if (inForm.ShowDialog() == DialogResult.Cancel) return null;
                if (inForm.StringValue == string.Empty) return null;
                try { return double.Parse(inForm.StringValue); }
                catch { return null; }
            }
        }

        /// <summary>
        /// Prompts the user to provide a value as an int.
        /// </summary>
        /// <param name="caption">Title for the Input form.</param>
        /// <param name="defaultValue">Default to be displayed in the textbox.</param>
        /// <returns>Nullable value: int.</returns>
        public static int? GetInt(string caption, string defaultValue)
        {
            using (InputForm inForm = new InputForm(caption, defaultValue))
            {
                if (inForm.ShowDialog() == DialogResult.Cancel) return null;
                if (inForm.StringValue == string.Empty) return null;
                try { return Int32.Parse(inForm.StringValue); }
                catch { return null; }
            }
        }

        /// <summary>
        /// Prompts the user to provide a value as a long.
        /// </summary>
        /// <param name="caption">Title for the Input form.</param>
        /// <param name="defaultValue">Default to be displayed in the textbox.</param>
        /// <returns>Nullable value: long.</returns>
        public static long? GetLong(string caption, string defaultValue)
        {
            using (InputForm inForm = new InputForm(caption, defaultValue))
            {
                if (inForm.ShowDialog() == DialogResult.Cancel) return null;
                if (inForm.StringValue == string.Empty) return null;
                try { return Int64.Parse(inForm.StringValue); }
                catch { return null; }
            }
        }
    }
}
