using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows.Forms;

using Stem_Protocol;
using CanDataLayer;
using SerialDataLayer;
using static Stem_Protocol.NetworkLayer;
using static StemPC.Form1;
using StemPC;
using Windows.Storage.Streams;

namespace Stem_Protocol.PacketManager
{
    public class PacketManager
    {
        //properties
        private uint _id;
        private uint _sniffer_id;
        private bool _canRunning = false;
        private bool _bluetoothRunning = false;
        private Dictionary<int, List<byte[]>> packetQueues = new Dictionary<int, List<byte[]>>();
        private NetworkLayer _networkPacket;
        public List<NetworkLayer.PacketReadyEventHandler> PacketReadyEventList;

        public List<CANDataLayer> CANChannelsList = new List<CANDataLayer>();
        public List<SDL> BLEChannelsList = new List<SDL>();

        //events
        //  public event Action<PacketReadyEventArgs> OnAppLayerPacketReceived;
        public event PacketReadyEventHandler OnAppLayerPacketReceived = null;

        //methods
        //    public PacketManager(uint id, Action<PacketReadyEventArgs> eventHandler)
        public PacketManager(uint id) 
        {
        _id = id;
            //if (eventHandler != null)
            //{
            //    OnAppLayerPacketReceived = eventHandler;
            //}
            PacketReadyEventList =new List<NetworkLayer.PacketReadyEventHandler>();
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
                packetQueues.Add(packetId, new List<byte[]>());
            }

            packetQueues[packetId].Add(packetChunkBytes);

            if (remainingChunks == 0)
            {
                //PACKET READY
                var unifiedPacket = packetQueues[packetId].SelectMany(chunk => chunk).ToArray();
                packetQueues.Remove(packetId);

                //FIX PACKET LENGHT

                if (unifiedPacket.Length > 7)
                {

                    _networkPacket = new NetworkLayer(interfaceType, version, _sniffer_id, unifiedPacket, true);

                    // Sottoscrizione degli eventi

                    if (OnAppLayerPacketReceived != null)
                    {
                        _networkPacket.SP_PacketReadyEvent += OnAppLayerPacketReceived;
                    }

                    //foreach (NetworkLayer.PacketReadyEventHandler Handler in PacketReadyEventList)
                    //{
                    //    _networkPacket.SP_PacketReadyEvent += Handler;
                    //}

                    //Packet is ready, decode it
                    _networkPacket.SP_PacketReady();
                }
            }
        }


    //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
    //                  CAN related functions
    //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

    public int Add_CAN_Channel(CANDataLayer canDataLayer)
    {
         canDataLayer.PacketReceived += OnCANPacketReceived;
         CANChannelsList.Add(canDataLayer);
          
         return (CANChannelsList.Count-1); //return the index of can channel list
    }

    public async Task<bool> SendCANAndWaitForResponseAsync(
    List<Tuple<byte[], uint, byte[]>> networkPackets,
    Func<byte[], bool> responseValidator, // Funzione di validazione risposta
    int timeoutMs = 600 // Timeout in millisecondi
)
        {
            var tcs = new TaskCompletionSource<bool>();
            var cancellationTokenSource = new CancellationTokenSource();

            void OnCanMessageReceived(object sender, AppLayerDecoderEventArgs e)
            {
                if (responseValidator(e.Payload))
                {
                    // Completa il TaskCompletionSource con successo
                    tcs.TrySetResult(true);
                }
            }

            try
            {
                // Sottoscrivi all'evento per ricevere i pacchetti
                Form1.FormRef.AppLayerCommandDecoded += OnCanMessageReceived;

                foreach (var packet in networkPackets)
                {
                    var netInfo = packet.Item1;
                    var recipientId = packet.Item2;
                    var packetChunk = packet.Item3;

                    var message = new CANMessage(recipientId, netInfo.Concat(packetChunk).ToArray(), true, DateTime.Now);

                    if (CANChannelsList.Count > 0)
                    {
                        try
                        {
                            CANChannelsList.ElementAt(0).Send(message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore nell'invio del messaggio: {ex.Message}");
                        }
                    }
                }

                // Timeout usando il CancellationTokenSource
               //var timeoutTask = Task.Delay(timeoutMs, cancellationTokenSource.Token);
                var timeoutTask = Task.Delay(1000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    // Risposta ricevuta prima del timeout
                    cancellationTokenSource.Cancel(); // Cancella il timeout
                    return tcs.Task.Result;
                }
                else
                {
                    // Timeout raggiunto
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'invio del pacchetto: {ex.Message}");
                return false;
            }
            finally
            {
                // Rimuovi l'evento per evitare memory leaks
                Form1.FormRef.AppLayerCommandDecoded -= OnCanMessageReceived;

                // Rilascia il CancellationTokenSource
                cancellationTokenSource.Dispose();
            }
        }

        public async Task<bool> SendThroughCANAsync(List<Tuple<byte[], uint, byte[]>> networkPackets)
        {
            try
            {
                //// Usa Task.Run per eseguire il lavoro intensivo
                //return await Task.Run(() =>
                //{
                    foreach (var packet in networkPackets)
                    {
                        var netInfo = packet.Item1;
                        var recipientId = packet.Item2;
                        var packetChunk = packet.Item3;

                        var message = new CANMessage(recipientId, netInfo.Concat(packetChunk).ToArray(), true, DateTime.Now);

                        if (CANChannelsList.Count > 0) 
                        {
                            try
                            {
                                CANChannelsList.ElementAt(0).Send(message);
                                // Console.WriteLine($"Message sent on {bus.ChannelInfo}: {BitConverter.ToString(message.Data)}");
                            }
                            catch (Exception)
                            {
                                // Console.WriteLine("Message not sent.");
                            }
                        }
                    await Task.Delay(5); //ritardo tra un chunck e il successivo
                    }
                //            });
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private void OnCANPacketReceived(object sender, CANMessage RxPacket)
        {
            ProcessCANPacket(RxPacket);
        }

        public void ProcessCANPacket(CANMessage msg)
        {
            if ((msg.ArbitrationId == _id) || (_id == 0xFFFFFFFF))
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

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        //                  BLE related functions
        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

        public int Add_BLE_Channel(SDL serialDataLayer)
        {
            serialDataLayer.PacketReceived += OnBLEPacketReceived;
            BLEChannelsList.Add(serialDataLayer);

            return (BLEChannelsList.Count - 1); //return the index of ble channel list
        }

    public async Task<bool> SendBLEAndWaitForResponseAsync(
    List<Tuple<byte[], uint, byte[]>> networkPackets,
    Func<byte[], bool> responseValidator, // Funzione di validazione risposta
    int timeoutMs = 600 // Timeout in millisecondi
)
        {
            var tcs = new TaskCompletionSource<bool>();
            var cancellationTokenSource = new CancellationTokenSource();

            void OnBleMessageReceived(object sender, AppLayerDecoderEventArgs e)
            {
                if (responseValidator(e.Payload))
                {
                    // Completa il TaskCompletionSource con successo
                    tcs.TrySetResult(true);
                }
            }

            try
            {
                // Sottoscrivi all'evento per ricevere i pacchetti
                Form1.FormRef.AppLayerCommandDecoded += OnBleMessageReceived;

                foreach (var packet in networkPackets)
                {
                    var netInfo = packet.Item1;
                    var recipientId = packet.Item2;
                    var packetChunk = packet.Item3;

                    var message = new SerialMessage(netInfo.Concat(BitConverter.GetBytes(recipientId)).Concat(packetChunk).ToArray(), DateTime.Now);

                    if (BLEChannelsList.Count > 0)
                    {
                        try
                        {
                            BLEChannelsList.ElementAt(0).Send(message);
                            // Console.WriteLine($"Message sent on {bus.ChannelInfo}: {BitConverter.ToString(message.Data)}");
                        }
                        catch (Exception)
                        {
                            // Console.WriteLine("Message not sent.");
                        }
                    }
                //    await Task.Delay(5); //ritardo tra un chunck e il successivo
                }

                // Timeout usando il CancellationTokenSource
                //var timeoutTask = Task.Delay(timeoutMs, cancellationTokenSource.Token);
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    // Risposta ricevuta prima del timeout
                    cancellationTokenSource.Cancel(); // Cancella il timeout
                    return tcs.Task.Result;
                }
                else
                {
                    // Timeout raggiunto
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'invio del pacchetto: {ex.Message}");
                return false;
            }
            finally
            {
                // Rimuovi l'evento per evitare memory leaks
                Form1.FormRef.AppLayerCommandDecoded -= OnBleMessageReceived;

                // Rilascia il CancellationTokenSource
                cancellationTokenSource.Dispose();
            }
        }

        public async Task<bool> SendThroughBLEAsync(List<Tuple<byte[], uint, byte[]>> networkPackets)
        {
            try
            {
                //// Usa Task.Run per eseguire il lavoro intensivo
                //return await Task.Run(() =>
                //{
                foreach (var packet in networkPackets)
                {
                    var netInfo = packet.Item1;
                    var recipientId = packet.Item2;
                    var packetChunk = packet.Item3;

                                  
                    var message = new SerialMessage(netInfo.Concat(BitConverter.GetBytes(recipientId)).Concat(packetChunk).ToArray(), DateTime.Now);

                    if (BLEChannelsList.Count > 0)
                    {
                        try
                        {
                            BLEChannelsList.ElementAt(0).Send(message);
                            // Console.WriteLine($"Message sent on {bus.ChannelInfo}: {BitConverter.ToString(message.Data)}");
                        }
                        catch (Exception)
                        {
                            // Console.WriteLine("Message not sent.");
                        }
                    }
                    await Task.Delay(5); //ritardo tra un chunck e il successivo
                }
                //            });
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private void OnBLEPacketReceived(object sender, SerialMessage RxPacket)
        {
            ProcessBLEPacket(RxPacket);
        }

        public void ProcessBLEPacket(SerialMessage packet)
        {
            // Elimina l'indirizzo del sender
            int PackToCopyLenght = packet.Data.Length - 6;
            Array.Copy(packet.Data, 6, packet.Data, 2, PackToCopyLenght);
            // e rimuovi gli ultimi 4 byte dal pacchetto
            packet.Data = packet.Data.Take(packet.Data.Length - 4).ToArray();
            //poi interpretalo
            ProcessPacket("ble", packet.Data);
        }
    }
}

