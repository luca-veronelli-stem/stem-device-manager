using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using App.STEMProtocol;

//PCAN support
using Peak.Can.Basic;
using DocumentFormat.OpenXml.InkML;
using System.Windows.Forms;
using Peak.Can.Basic.BackwardCompatibility;

namespace App.STEMProtocol;

public class CANMessage
{
    public uint ArbitrationId { get; }
    public byte[] Data { get; }
    public bool IsErrorFrame { get; }
    public DateTime Timestamp { get; }

    public CANMessage(uint arbitrationId, byte[] data, bool isErrorFrame, DateTime timestamp)
    {
        ArbitrationId = arbitrationId;
        Data = data;
        IsErrorFrame = isErrorFrame;
        Timestamp = timestamp;
    }
}

public class TX_CAN_Data
{
    public int Result;
    public CANMessage Message;
}

public class CANDataLayer : IDisposable
{
    public string Channel { get; }
    public string CanInterface { get; }
    public int    Bitrate { get; }
    public bool   IsConnected;

    private PCANManager _pcanManager=null;

    // Eventi
    public event EventHandler<bool> ConnectionStatusChanged;
    public event EventHandler<CANMessage> PacketReceived;
    public event EventHandler<TX_CAN_Data> PacketSended;

    public CANDataLayer(string channel, string canInterface, int bitrate)
    {
        Channel = channel;
        CanInterface = canInterface;
        Bitrate = bitrate;

        // Implementation to initialize CAN bus:

        if (canInterface == "pcan")
        {
            TPCANBaudrate baudrate;

            switch (Bitrate)
            {
                default:
                case 100000:
                    baudrate=TPCANBaudrate.PCAN_BAUD_100K;
                    break;
                case 125000:
                    baudrate = TPCANBaudrate.PCAN_BAUD_100K;
                    break;
                case 250000:
                    baudrate = TPCANBaudrate.PCAN_BAUD_250K;
                    break;
                case 500000:
                    baudrate = TPCANBaudrate.PCAN_BAUD_500K;
                    break;
            }
            //PCAN
            _pcanManager = new PCANManager(baudrate);

            // Sottoscrizione agli eventi
            _pcanManager.PacketReceived += OnPacketReceived;
            _pcanManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            //_pcanManager.ErrorOccurred += OnErrorOccurred;

            //avvia il pcan
            if (_pcanManager.IsConnected)
            {
                _pcanManager.StartReading();
            }

            IsConnected = _pcanManager.IsConnected;
        }
    }

    public void ChangeBaudrate(string canInterface, int bitrate)
    {
        if (canInterface == "pcan")
        {
            if (_pcanManager == null) return;
            //PCAN
            _pcanManager.ChangeBaudRate(bitrate);
        }
    }

    public async void Send(CANMessage message)
    {
        int result=0;

        //Implementation to send message through CAN
        if (_pcanManager != null)
        {
            if (_pcanManager.IsConnected)
            {
                result=(int) await _pcanManager.SendMessageAsync(message.ArbitrationId, message.Data, true);
                TX_CAN_Data TX_Can_Data = new TX_CAN_Data();
                TX_Can_Data.Result=result;
                TX_Can_Data.Message = message;
                PacketSended?.Invoke(this, TX_Can_Data);
            }
        }
    }

    private void OnPacketReceived(object sender, CANPacketEventArgs e)
    {
        //aggiungi i messaggi alla coda del network layer
        CANMessage RxMessage = new CANMessage(e.ArbitrationId, e.Data, false, e.Timestamp);
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
