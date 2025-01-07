using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using Stem_Protocol;

//PCAN support
using PCAN_Handler;
using Peak.Can.Basic;

namespace CanDataLayer;



public class CANMessage
{
    public uint ArbitrationId { get; }
    public byte[] Data { get; }
    public bool IsErrorFrame { get; }

    public CANMessage(uint arbitrationId, byte[] data, bool isErrorFrame)
    {
        ArbitrationId = arbitrationId;
        Data = data;
        IsErrorFrame = isErrorFrame;
    }
}

public class CANDataLayer : IDisposable
{
    public string Channel { get; }
    public string CanInterface { get; }
    public int    Bitrate { get; }
    public bool   IsConnected;

    private PCANManager _pcanManager;

    // Eventi
    public event EventHandler<bool> ConnectionStatusChanged;

    public CANDataLayer(string channel, string canInterface, int bitrate)
    {
        Channel = channel;
        CanInterface = canInterface;
        Bitrate = bitrate;

        // Implementation to initialize CAN bus:

        if (canInterface == "pcan")
        {
            //PCAN
            _pcanManager = new PCANManager();

            // Sottoscrizione agli eventi
            _pcanManager.PacketReceived += OnPacketReceived;
            _pcanManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            //_pcanManager.ErrorOccurred += OnErrorOccurred;

            //avvia il pcan
            if (_pcanManager.IsConnected)
            {
                _pcanManager.StartReading();
            }
        }
    }

    public void Send(CANMessage message)
    {

        //    public class CANMessage
        //{
        //    public uint ArbitrationId { get; }
        //    public byte[] Data { get; }
        //    public bool IsErrorFrame { get; }

        //    public CANMessage(uint arbitrationId, byte[] data, bool isErrorFrame)
        //    {
        //        ArbitrationId = arbitrationId;
        //        Data = data;
        //        IsErrorFrame = isErrorFrame;
        //    }
        //}

        // Implementation to send message through CAN
        //       Form1.CanTabPageRef.thisRef.SendCANMessage(message.ArbitrationId, message.Data);
    }

    private void OnPacketReceived(object sender, CANPacketEventArgs e)
    {
        //aggiungi i messaggi alla coda del network layer
        CANMessage RxMessage = new CANMessage(e.ArbitrationId, e.Data, false);
 //       ParentPacketManager.ProcessCANPacket(RxMessage);
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
