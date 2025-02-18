using System;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RoomAssign;

public class TextBoxStreamWriter(TextBox textBox) : TextWriter
{
    public override void Write(char value)
    {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.AppendText(value.ToString());
            textBox.ScrollToEnd();
        }));
    }

    public override void Write(string value)
    {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.AppendText(value);
            textBox.ScrollToEnd();
        }));
    }

    public override Encoding Encoding => Encoding.UTF8;
}