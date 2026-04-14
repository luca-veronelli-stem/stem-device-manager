using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using App.STEMProtocol;

//BLE support

//COM support

//using System.Windows.Forms;

namespace App.STEMProtocol;

public class SerialMessage
{
    public uint ArbitrationId { get; }
    public byte[] Data { get; set; }
    public bool IsErrorFrame { get; }
    public DateTime Timestamp { get; }

    public SerialMessage(byte[] data, DateTime timestamp)
    {
        Data = data;
        Timestamp = timestamp;
    }
}

public class TX_Serial_Data
{
    public int Result;
    public SerialMessage Message;
}

public class SDL : IDisposable
{
    public string Channel { get; }
    public string SerialInterface { get; }
    public int    Bitrate { get; }
    public bool   IsConnected;

    private BLEManager          _bleManager = null;
    private SerialPortManager   _serialManager = null;

    // Eventi
    public event EventHandler<bool> ConnectionStatusChanged;
    public event EventHandler<SerialMessage> PacketReceived;
    public event EventHandler<TX_Serial_Data> PacketSended;

    public SDL(string channel, string serialInterface, int bitrate, object Manager)
    {
        Channel = channel;
        SerialInterface = serialInterface;
        Bitrate = bitrate;

        if (SerialInterface == "ble")
        {
            //BLE
            _bleManager = (BLEManager) Manager;

            // Sottoscrizione agli eventi
            _bleManager.PacketReceived += OnBLEPacketReceived;
            _bleManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            IsConnected = false;
        }
        else if (SerialInterface == "serial")
        {
            //Serial
            _serialManager = (SerialPortManager) Manager;

            // Sottoscrizione agli eventi
            _serialManager.PacketReceived += OnSerialPacketReceived;
            _serialManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            IsConnected = false;
        }

    }

    public async void Send(SerialMessage message)
    {
        int result=0;

        //Implementation to send message through BLE or Serial
        if ((_bleManager != null) && (SerialInterface == "ble"))
        {
            //if (_bleManager.IsConnected)
            //{
                if (await _bleManager.SendMessageAsync(message.Data)== true)
                {
                    result = 1;
                }
                else
                {
                    result = 0;
                }

                TX_Serial_Data TX_serial_Data = new TX_Serial_Data();
                TX_serial_Data.Result = result;
                TX_serial_Data.Message = message;
                PacketSended?.Invoke(this, TX_serial_Data);
//            }
        }
        else if ((_serialManager != null) && (SerialInterface == "serial"))
        {
            //if (_bleManager.IsConnected)
            //{
            if (await _serialManager.SendMessageAsync(message.Data) == true)
            {
                result = 1;
            }
            else
            {
                result = 0;
            }

            TX_Serial_Data TX_serial_Data = new TX_Serial_Data();
            TX_serial_Data.Result = result;
            TX_serial_Data.Message = message;
            PacketSended?.Invoke(this, TX_serial_Data);
            //            }
        }
    }

    private void OnBLEPacketReceived(object sender, BLEPacketEventArgs e)
    {
        //aggiungi i messaggi alla coda del network layer
        SerialMessage RxMessage = new SerialMessage(e.Data, e.Timestamp);
        PacketReceived?.Invoke(this, RxMessage);
    }

    private void OnSerialPacketReceived(object sender, SerialPacketEventArgs e)
    {
        //aggiungi i messaggi alla coda del network layer
        SerialMessage RxMessage = new SerialMessage(e.Data, e.Timestamp);
        PacketReceived?.Invoke(this, RxMessage);
    }

    private void OnConnectionStatusChanged(object sender, bool isConnected)
    {
        IsConnected = isConnected;

        if (isConnected)
        {
            ConnectionStatusChanged?.Invoke(this, true);
        }
        else
        {
            ConnectionStatusChanged?.Invoke(this, false);
        }
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
