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
        // 判断如果日志行数大于50则在添加前删除第一行
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (textBox.LineCount > 50)
            {
                var firstLine = textBox.GetLineIndexFromCharacterIndex(0);
                textBox.Text = textBox.Text.Remove(0, textBox.GetCharacterIndexFromLineIndex(firstLine + 1));
            }
            textBox.AppendText(value.ToString());
            textBox.ScrollToEnd();
        }));
    }

    public override void Write(string value)
    {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (textBox.LineCount > 50)
            {
                var firstLine = textBox.GetLineIndexFromCharacterIndex(0);
                textBox.Text = textBox.Text.Remove(0, textBox.GetCharacterIndexFromLineIndex(firstLine + 1));
            }
            textBox.AppendText(value);
            textBox.ScrollToEnd();
        }));
    }

    public override Encoding Encoding => Encoding.UTF8;
}