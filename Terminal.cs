using System;
using System.Text;

namespace StemPC
{
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
            Console.WriteLine(message);
        }

        public string GetLog()
        {
            return _log.ToString();
        }
    }
}
