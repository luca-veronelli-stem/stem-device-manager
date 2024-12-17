using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows.Forms;
using Stem_Protocol;

//using static NetworkLayer;
using StemPC;


namespace Stem_Protocol.PacketManager
{
    public class PacketManager
    {
        private uint _id;
        private uint _sniffer_id;
        private bool _canRunning = false;
        private bool _bluetoothRunning = false;
        private Dictionary<int, List<byte[]>> packetQueues = new Dictionary<int, List<byte[]>>();
        private NetworkLayer _networkPacket;

        public List<NetworkLayer.PacketReadyEventHandler> PacketReadyEventList;

        public PacketManager(uint id)
        {
            _id = id;
            PacketReadyEventList=new List<NetworkLayer.PacketReadyEventHandler>();
        }

        public uint Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public bool BluetoothRunning
        {
            get { return _bluetoothRunning; }
            set { _bluetoothRunning = value; }
        }

        public bool CanRunning
        {
            get { return _canRunning; }
            set { _canRunning = value; }
        }

        public NetworkLayer NetworkPacket
        {
            get { return _networkPacket; }
            set { _networkPacket = value; }
        }

        public void RegisterPacketReadyEvent(NetworkLayer.PacketReadyEventHandler Handler)
        {
            PacketReadyEventList.Add(Handler);
        }

        public bool SendThroughCAN(List<Tuple<byte[], uint, byte[]>> networkPackets)
        {
            try
            {
                var canInterface = "pcan";
                var channel = "PCAN_USBBUS1";
                var bitrate = 100000;

                using (var bus = new CANBus(channel, canInterface, bitrate))
                {
                    foreach (var packet in networkPackets)
                    {
                        var netInfo = packet.Item1;
                        var recipientId = packet.Item2;
                        var packetChunk = packet.Item3;

                        var message = new CANMessage(recipientId, netInfo.Concat(packetChunk).ToArray(), true);

                        try
                        {
                            bus.Send(message);
                            Console.WriteLine($"Message sent on {bus.ChannelInfo}: {BitConverter.ToString(message.Data)}");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Message not sent.");
                        }
                    }
                    Thread.Sleep(2000);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        public async Task<bool> SendThroughBluetooth(BlockingCollection<bool> qSend, BlockingCollection<List<Tuple<byte[], int, byte[]>>> qMessage, string deviceAddress)
        {
            try
            {
                using (var client = new BluetoothClient(deviceAddress, HandleBLEDisconnect))
                {
                    if (client.IsConnected)
                    {
                        Console.WriteLine($"Connected to {client.Address}");
                        BluetoothRunning = true;

                        var UART_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
                        var UART_RX_CHAR_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
                        var UART_TX_CHAR_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

                        await client.StartNotify(UART_TX_CHAR_UUID, ProcessBLEPacket);

                        while (true)
                        {
                            var send = await Task.Run(() => qSend.Take());
                            if (send)
                            {
                                var networkPackets = await Task.Run(() => qMessage.Take());
                                foreach (var packet in networkPackets)
                                {
                                    var netInfo = packet.Item1;
                                    var recipientId = packet.Item2;
                                    var packetChunk = packet.Item3;

                                    var packetData = netInfo.Concat(BitConverter.GetBytes(recipientId)).Concat(packetChunk).ToArray();
                                    Console.WriteLine($"Message sent via Bluetooth: {BitConverter.ToString(packetData)}");
                                    await client.Write(UART_RX_CHAR_UUID, packetData, true);
                                }
                                qMessage.CompleteAdding();
                                qSend.CompleteAdding();
                            }
                            else
                            {
                                Console.WriteLine("Disconnecting from Bluetooth...");
                                return true;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to connect to Bluetooth device.");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection error: {e.Message}");
                return false;
            }
        }

        private void HandleBLEDisconnect(BluetoothClient client)
        {
            Console.WriteLine("Disconnected from Bluetooth.");
            BluetoothRunning = false;
        }

        public void ProcessCANPacket(CANMessage msg)
        {
            if ((msg.ArbitrationId == _id)|| (_id == 0xFFFFFFFF))
            {
                _sniffer_id = _id;
                if (_id == 0xFFFFFFFF) _sniffer_id = msg.ArbitrationId;
                
                if (msg.IsErrorFrame)
                {
                  //  Console.WriteLine("Error Frame");
                }
                else
                {
                    
                    ProcessPacket("can", msg.Data);
                }
            }
        }

        public void ProcessBLEPacket(byte[] packet)
        {
            Console.WriteLine($"Message received: {BitConverter.ToString(packet)}");
            var recipientId = BitConverter.ToUInt32(packet, 2);
            if (recipientId == _id)
            {
                _sniffer_id = _id;
                var data = packet.Take(2).Concat(packet.Skip(6)).ToArray();
                ProcessPacket("ble", data);
            }
            else if (recipientId == 0xFFFFFFFF) 
            {
                 _sniffer_id = recipientId;
                 var data = packet.Take(2).Concat(packet.Skip(6)).ToArray();
                 ProcessPacket("ble", data);
            }
            else 
            {
                 Console.WriteLine($"Recipient ID mismatch: {recipientId} != {_id}");
            }
        }

        private void ProcessPacket(string interfaceType, byte[] packet)
        {
            var netInfoBytes = packet.Take(2).ToArray();
            var packetChunkBytes = packet.Skip(2).ToArray();
            var netInfo = BitConverter.ToUInt16(netInfoBytes, 0);

            var remainingChunks = (netInfo >> 6) & 0x3FF;
            var setLength = (netInfo >> 5) & 0x01;
            var packetId = (netInfo >> 2) & 0x07;
            var version = netInfo & 0x03;

            if (!packetQueues.ContainsKey(packetId))
            {
                //Azzera la lista pacchetti ricevuti ad ogni cambio di Id
                packetQueues[packetId] = new List<byte[]>();
            } 
                
            packetQueues[packetId].Add(packetChunkBytes);

            if (remainingChunks == 0)
            {
                var unifiedPacket = packetQueues[packetId].SelectMany(chunk => chunk).ToArray();
                packetQueues[packetId].Clear();
                _networkPacket = new NetworkLayer(interfaceType, version, _sniffer_id, unifiedPacket, true);

                // Sottoscrizione degli eventi
                foreach (NetworkLayer.PacketReadyEventHandler Handler in PacketReadyEventList)
                {
                    _networkPacket.SP_PacketReadyEvent += Handler;
                }

                //Packet is ready, decode it
                _networkPacket.SP_PacketReady();
            }
        }
    }

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

    public class CANBus : IDisposable
    {
        public string ChannelInfo { get; }

        public CANBus(string channel, string canInterface, int bitrate)
        {
            // Implementation to initialize CAN bus
            ChannelInfo = channel;
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
            Form1.FormRef.CanTabPageRef.SendCANMessage(message.ArbitrationId, message.Data);
        }

        public void Dispose()
        {
            // Cleanup resources
        }
    }

    public class BluetoothClient : IDisposable
    {
        public string Address { get; }
        public bool IsConnected { get; }

        public BluetoothClient(string address, Action<BluetoothClient> disconnectCallback)
        {
            Address = address;
            IsConnected = true;
            // Initialize connection
        }

        public async Task StartNotify(string charUuid, Action<byte[]> callback)
        {
            // Implementation to start notifications
        }

        public async Task Write(string charUuid, byte[] data, bool response)
        {
            // Implementation to write data via Bluetooth
        }

        public void Dispose()
        {
            // Cleanup resources
        }
    }
}

