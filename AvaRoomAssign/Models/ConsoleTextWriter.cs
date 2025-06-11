using System;
using System.IO;
using System.Text;
using Avalonia.Threading;

namespace AvaRoomAssign.Models;

public class ConsoleTextWriter : TextWriter
{
    private readonly Action<string> _writeAction;
    private readonly StringBuilder _buffer = new();

    public ConsoleTextWriter(Action<string> writeAction)
    {
        _writeAction = writeAction;
    }

    public override void Write(char value)
    {
        _buffer.Append(value);
        if (value == '\n')
        {
            Flush();
        }
    }

    public override void Write(string? value)
    {
        if (value != null)
        {
            _buffer.Append(value);
            if (value.Contains('\n'))
            {
                Flush();
            }
        }
    }

    public override void WriteLine(string? value)
    {
        _buffer.AppendLine(value);
        Flush();
    }

    public override void Flush()
    {
        if (_buffer.Length > 0)
        {
            var text = _buffer.ToString();
            _buffer.Clear();
            
            // 确保在UI线程上执行
            Dispatcher.UIThread.InvokeAsync(() => _writeAction(text));
        }
    }

    public override Encoding Encoding => Encoding.UTF8;
} 