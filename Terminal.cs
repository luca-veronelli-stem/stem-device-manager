using System;
using System.Text;

public class Terminal
{
    private StringBuilder _log;

    public Terminal()
    {
        _log = new StringBuilder();
    }

    public void WriteLine(string message)
    {
        _log.AppendLine(message);
        // Puoi anche scrivere sulla console se vuoi
        // Console.WriteLine(message);
    }

    public string GetLog()
    {
        return _log.ToString();
    }

    public string WriteLog(string message)
    {
        WriteLine(message);
        return _log.ToString(); 
    }
}
