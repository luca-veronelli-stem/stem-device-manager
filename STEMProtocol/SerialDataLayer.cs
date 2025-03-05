using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using Stem_Protocol;

//BLE support
using BLE_Handler;

//using System.Windows.Forms;

namespace SerialDataLayer;

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

    private BLEManager _bleManager =null;

    // Eventi
    public event EventHandler<bool> ConnectionStatusChanged;
    public event EventHandler<SerialMessage> PacketReceived;
    public event EventHandler<TX_Serial_Data> PacketSended;

    public SDL(string channel, string serialInterface, int bitrate, BLEManager bleManager)
    {
        Channel = channel;
        SerialInterface = serialInterface;
        Bitrate = bitrate;

        // Implementation to initialize CAN bus:

        if (SerialInterface == "ble")
        {
            //BLE
            _bleManager = bleManager;

            // Sottoscrizione agli eventi
            _bleManager.PacketReceived += OnBLEPacketReceived;
            _bleManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            //_pcanManager.ErrorOccurred += OnErrorOccurred;

            ////avvia il pcan
            //if (_pcanManager.IsConnected)
            //{
            //    _pcanManager.StartReading();
            //}
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
    }

    private void OnBLEPacketReceived(object sender, BLEPacketEventArgs e)
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
