using System;
using System.IO.Ports;

public class SerialPortManager
{
    private SerialPort _serialPort;

    public SerialPortManager(string portName, int baudRate)
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
    }

    public void Open()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
    }

    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }

    public void Send(string message)
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.WriteLine(message);
        }
    }

    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        string inData = sp.ReadExisting();
        Console.WriteLine($"Data Received: {inData}");
        // Puoi aggiungere ulteriori logiche per gestire i dati ricevuti
    }
}
