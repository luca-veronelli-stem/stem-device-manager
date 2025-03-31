using PCAN_Handler;
using Stem_Protocol.BootManager;
using StemPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stem_Protocol.PacketManager;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Stem_Protocol
{
    public class Layer
    {
        protected byte[] _data;
        protected byte[] _header;
        protected byte[] _packet;

        public Layer(byte[] data, bool pack = true)
        {
            _data = data;
            if (pack)
            {
                BuildHeader();
                BuildPacket();
            }
        }

        public byte[] Data
        {
            get { return _data; }
            set
            {
                _data = value;
                BuildPacket();
            }
        }

        public byte[] Header
        {
            get { return _header; }
            set { _header = value; }
        }

        public byte[] Packet
        {
            get { return _packet; }
            set { _packet = value; }
        }

        protected virtual void BuildHeader()
        {
            _header = new byte[0]; // Default empty header
        }

        protected virtual void BuildPacket()
        {
            _packet = _header.Concat(_data).ToArray();
        }
    }

    public class ApplicationLayer : Layer
    {
        private byte _cmdInit;
        private byte _cmdOpt;
        private string _formatString = ">BB";
        private byte[] _applicationHeader;
        private byte[] _applicationPacket;

        public ApplicationLayer(byte cmdInit, byte cmdOpt, byte[] data, bool pack = true)
            : base(data)
        {
            _cmdInit = cmdInit;
            _cmdOpt = cmdOpt;
            if (pack)
            {
                BuildApplicationHeader();
                BuildApplicationPacket();
            }
        }

        public void CleanApplicationBuffer()
        {
            Array.Clear(_applicationPacket,0, _applicationPacket.Length);
        }

        public byte CmdInit
        {
            get { return _cmdInit; }
            set
            {
                _cmdInit = value;
                BuildApplicationHeader();
                BuildApplicationPacket();
            }
        }

        public byte CmdOpt
        {
            get { return _cmdOpt; }
            set
            {
                _cmdOpt = value;
                BuildApplicationHeader();
                BuildApplicationPacket();
            }
        }

        public byte[] ApplicationHeader
        {
            get { return _applicationHeader; }
            set { _applicationHeader = value; }
        }

        public byte[] ApplicationPacket
        {
            get { return _applicationPacket; }
            set { _applicationPacket = value; }
        }

        private void BuildApplicationHeader()
        {
            _applicationHeader = new byte[] { _cmdInit, _cmdOpt };
        }

        private void BuildApplicationPacket()
        {
            _applicationPacket = _applicationHeader.Concat(_packet).ToArray();
        }
    }

    public class TransportLayer : ApplicationLayer
    {
        private byte _cryptFlag;
        private int _senderId;
        private ushort _lPack;
        private byte[] _transportHeader;
        private byte[] _transportPacket;
        private byte[] _crc;
        private string _formatString = ">BIH";

        public TransportLayer(byte cryptFlag, int senderId, byte[] data, bool pack = true)
            : base(data[0], data[1], data.Skip(2).ToArray(), pack)
        {
            _cryptFlag = cryptFlag;
            _senderId = senderId;
            _lPack = (ushort)data.Length;
            if (pack)
            {
                BuildTransportHeader();
                BuildTransportPacket();
            }
        }

        public byte CryptFlag
        {
            get { return _cryptFlag; }
            set
            {
                _cryptFlag = value;
                BuildTransportHeader();
                BuildTransportPacket();
            }
        }

        public int SenderId
        {
            get { return _senderId; }
            set
            {
                _senderId = value;
                BuildTransportHeader();
                BuildTransportPacket();
            }
        }

        public int LPack
        {
            get { return _lPack; }
        }

        public byte[] Crc
        {
            get { return _crc; }
            set { _crc = value; }
        }

        public byte[] TransportHeader
        {
            get { return _transportHeader; }
            set { _transportHeader = value; }
        }

        public byte[] TransportPacket
        {
            get { return _transportPacket; }
            set { _transportPacket = value; }
        }

        private void BuildTransportHeader()
        {
            // Swap bytes per _senderId (32 bit)
            byte[] senderIdBytes = BitConverter.GetBytes(_senderId);
            Array.Reverse(senderIdBytes);

            // Swap bytes per _lPack (16 bit)
            byte[] lPackBytes = BitConverter.GetBytes(_lPack);
            Array.Reverse(lPackBytes);

            _transportHeader = new[] { _cryptFlag }
                .Concat(senderIdBytes)
                .Concat(lPackBytes)
                .ToArray();
        }

        private void BuildTransportPacket()
        {
            var packet = _transportHeader.Concat(ApplicationPacket).ToArray();
            SetCrc(packet);
            Array.Reverse(_crc);
            _transportPacket = packet.Concat(_crc).ToArray();
        }

        private void SetCrc(byte[] packet)
        {
            // Implementazione di un calcolo CRC (Modbus CRC16)
            ushort crc = Crc16(packet);
            _crc = BitConverter.GetBytes(crc);
        }

        private ushort Crc16(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }

    public class NetworkLayer : TransportLayer
    {
        // Delegate per definire il tipo dell'evento con array di byte
        public delegate void PacketReadyEventHandler(object sender, PacketReadyEventArgs e);

        // Evento che altre classi possono sottoscrivere
        public event PacketReadyEventHandler SP_PacketReadyEvent;

        //   public event EventHandler<PacketReadyEventArgs> SP_PacketReadyEvent;

        private string _interface;
        private int _version;
        private uint _recipientId;
        private int _packetChunkSize;
        private byte[] _netInfo;
        private byte[] _networkHeader;
        private byte[] _networkPacket;
        private List<Tuple<byte[], uint, byte[]>> _networkPackets;

        private int packetId;

        public NetworkLayer(string interfaceType, int version, uint recipientId, byte[] data, bool pack = true)
            : base(data[0], BitConverter.ToInt32(data, 1), data.Skip(7).ToArray(), pack)
        {
            _interface = interfaceType.ToLower();
            _version = version;
            _recipientId = recipientId;
            packetId = 0;
            SetPacketChunkSize();
            if (pack)
            {
                BuildNetworkHeader();
                BuildNetworkPacket();
            }
        }

        public string Interface
        {
            get { return _interface; }
            set
            {
                _interface = value.ToLower();
                SetPacketChunkSize();
                BuildNetworkHeader();
                BuildNetworkPacket();
            }
        }

        public int Version
        {
            get { return _version; }
            set
            {
                _version = value;
                BuildNetworkHeader();
                BuildNetworkPacket();
            }
        }

        public uint RecipientId
        {
            get { return _recipientId; }
            set
            {
                _recipientId = value;
                BuildNetworkHeader();
                BuildNetworkPacket();
            }
        }

        public int PacketChunkSize
        {
            get { return _packetChunkSize; }
        }

        public byte[] NetInfo
        {
            get { return _netInfo; }
        }

        public byte[] NetworkHeader
        {
            get { return _networkHeader; }
            set { _networkHeader = value; }
        }

        public byte[] NetworkPacket
        {
            get { return _networkPacket; }
            set { _networkPacket = value; }
        }

        public List<Tuple<byte[], uint, byte[]>> NetworkPackets
        {
            get { return _networkPackets; }
        }

        private void SetPacketChunkSize()
        {
            switch (_interface)
            {
                case "can":
                    _packetChunkSize = 6;
                    break;
                case "ble":
                    _packetChunkSize = 98;
                    break;
                default:
                    throw new ArgumentException("Unrecognized interface");
            }
        }

        private void BuildNetworkHeader()
        {
            BuildNetInfo(1, 1, 0);
            _networkHeader = _netInfo.Concat(BitConverter.GetBytes(_recipientId)).ToArray();
        }

        private void BuildNetInfo(int packetId, int setLength, int remainingChunks)
        {
            int netInfo = (remainingChunks << 6) | (setLength << 5) | (packetId << 2) | _version;
            _netInfo = BitConverter.GetBytes((ushort)netInfo);
        }

        private void BuildNetworkPacket()
        {
            _networkPacket = _networkHeader.Concat(TransportPacket).ToArray();
            BuildNetworkPackets();
        }

        private void BuildNetworkPackets()
        {
            var chunks = SplitDataIntoChunks();
            _networkPackets = new List<Tuple<byte[], uint, byte[]>>();
            int remainingChunks = chunks.Count - 1;
            //rolling code del packid
            packetId = RollingCodeGenerator.GetIndex();
            //indicatore primo chunck
            int setLength = 1;

            foreach (var chunk in chunks)
            {
                BuildNetInfo(packetId, setLength, remainingChunks);
                var networkPacket = Tuple.Create(_netInfo, _recipientId, chunk);
                _networkPackets.Add(networkPacket);
                remainingChunks--;
                setLength = 0;
            }
        }

        private List<byte[]> SplitDataIntoChunks()
        {
            var chunks = new List<byte[]>();
            for (int i = 0; i < TransportPacket.Length; i += _packetChunkSize)
            {
                var chunk = TransportPacket.Skip(i).Take(_packetChunkSize).ToArray();

                //if (chunk.Length < _packetChunkSize)
                //{
                //    Array.Resize(ref chunk, _packetChunkSize);
                //}

                chunks.Add(chunk);
            }
            return chunks;
        }

        public void SP_PacketReady()
        {
            // Logica per verificare che il transport layer sia a posto
            uint Source_Address = (((uint)TransportPacket[4]) << 24) | (((uint)TransportPacket[3]) << 16) | (((uint)TransportPacket[2]) << 8) | (((uint)TransportPacket[1]));

            //(da sistemare TOPX)
            //CAN
              ApplicationPacket = ApplicationPacket.Take(ApplicationPacket.Length - 2).ToArray();
              OnSP_PacketReady(ApplicationPacket, Source_Address, _recipientId);

            //BLE
            //Togli i primi 7 byte di intestazione e togli il crc al transport packet eliminando  gli ultimi 2 byte del buffer
        //    byte[] PacketApplication=new byte[TransportPacket[6]-2];
        //    Buffer.BlockCopy(TransportPacket.ToArray(), 7, PacketApplication, 0, TransportPacket[6] - 6);   
            
            // Emettere l'evento con il pacchetto ricevuto
        //    OnSP_PacketReady(PacketApplication, Source_Address, _recipientId);
        }

        // Metodo protetto per emettere l'evento
        protected virtual void OnSP_PacketReady(byte[] receivedPacket, uint sourceAddress, uint destinationAddress)
        {
            SP_PacketReadyEvent?.Invoke(this, new PacketReadyEventArgs(receivedPacket, sourceAddress, destinationAddress));
        }

        // Classe per passare dati personalizzati con l'evento
        public class PacketReadyEventArgs : EventArgs
        {
            public byte[] Packet { get; }
            public uint SourceAddress { get; }
            public uint DestinationAddress { get; }

            public PacketReadyEventArgs(byte[] packet, uint sourceAddress, uint destinationAddress)
            {
                Packet = packet;
                SourceAddress = sourceAddress;
                DestinationAddress = destinationAddress;
            }
        }
    }

    //classe che gestisce l'instradamento dei pacchetti verso il canale fisico
    public class ProtocolManager
    {
        public event EventHandler<SendCommandEventArgs>? SendCommandRequest;

        bool Answer_Received;
        bool Answer_Result;

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        //         Common function for all type of channels 
        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        public async Task<bool> SendCommand(ushort command, byte[] payload, bool waitAnswer)
        {
            Answer_Received = false;

            if (SendCommandRequest?.GetInvocationList().Length == 0)
            {
                // Nessun evento iscritto, esegui codice alternativo
                Answer_Received = true;
                Answer_Result = true;
                return Answer_Result;
            }
            else
            {
                // Invoca l'evento normalmente
                // Controlla se ci sono iscritti all'evento prima di invocarlo
                SendCommandRequest?.Invoke(this, new SendCommandEventArgs(command, payload, waitAnswer));

                if (waitAnswer == false)
                {
                    Answer_Received = true;
                }

                // Attende la risposta
                while (Answer_Received == false)
                {
                    await Task.Delay(10); // Attende 10 ms prima di ricontrollare
                }
                return Answer_Result;
            }
        }

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        //                  CAN related functions
        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%


        // Gestore dell'evento send command da instradare tramite can
        public async void OnSendCanCommand(object sender, SendCommandEventArgs e)
        {
            await HandleSendCanCommandAsync(sender, e);
        }

        private async Task HandleSendCanCommandAsync(object sender, SendCommandEventArgs e)
        {
            try
            {
                // Crea il pacchetto per l'applicationLayer
                byte[] AppData = { (byte)(e.Command >> 8), (byte)(e.Command) };

                // AL
                byte cmdInit = AppData[0]; // comando byte alto
                byte cmdOpt = AppData[1]; // comando byte basso
                byte[] payload = e.Payload;

                // TL
                byte cryptFlag = 0x00; // Nessuna crittografia

                // NL
                string interfaceType = "can";
                int version = 1;
                uint recipientId = Form1.FormRef.RecipientId;

                // Crea il pacchetto di livello Network
                var networkLayer = new NetworkLayer(
                    interfaceType,
                    version,
                    recipientId,
                    new byte[] { cryptFlag, (byte)Form1.FormRef.senderId, (byte)(Form1.FormRef.senderId >> 8), (byte)(Form1.FormRef.senderId >> 16), (byte)(Form1.FormRef.senderId >> 24), 0, 0, cmdInit, cmdOpt }.Concat(payload).ToArray(),
                    true
                );

                // Stampa i dettagli           
                Form1.FormRef.UpdateTerminal("Invio Comando can");
                //Form1.FormRef.UpdateTerminal("Comando Boot manager:");
                //Form1.FormRef.UpdateTerminal($"{string.Join(" ", networkLayer.ApplicationPacket.Select(b => b.ToString("X2")))}");

                // Ottieni i chunk da spedire
                var networkPackets = networkLayer.NetworkPackets;
                var packetManager = new PacketManager.PacketManager(Form1.FormRef.senderId);
                packetManager.Add_CAN_Channel(Form1.FormRef._CDL);

                // Invia i pacchetti tramite CAN in modo asincrono
                bool result = false;

                if (e.WaitAnswer)
                {
                    // Funzione di validazione della risposta
                    Func<byte[], bool> responseValidator = (data) =>
                    {
                        //il validatore di risposta nel caso della pagina firmware deve verificare anche che il numero di pagina sia corretto
                        if ((AppData[0] == 0) && (AppData[1] == 7))
                        {
                            return (
                            (data.Length > 0)
                            && (data[0] == (0x80 | AppData[0]))
                            && (data[1] == (AppData[1]))
                            ////tipo firmware per ora commentato, da attivare come parametro dall'esterno
                            //&& (data[2] == (payload[0]))
                            //&& (data[3] == (payload[1]))
                            //numero pagina 
                            && (data[4] == (payload[2]))
                            && (data[5] == (payload[3]))
                            && (data[6] == (payload[4]))
                            && (data[7] == (payload[5]))
                            );
                        }
                        else return (
                            (data.Length > 0)
                            && (data[0] == (0x80 | AppData[0]))
                            && (data[1] == (AppData[1]))
                            );
                    };
                    result = await packetManager.SendCANAndWaitForResponseAsync(networkPackets, responseValidator);
                }
                else
                {
                    result = await packetManager.SendThroughCANAsync(networkPackets);
                }

                // Usa il risultato aggiornando il semaforo
                Answer_Received = true;
                Answer_Result = result;
            }
            catch (Exception ex)
            {
                // Gestione dell'eccezione
                Form1.FormRef.UpdateTerminal($"Errore durante l'invio del comando CAN: {ex.Message}");
            }
        }

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        //                  BLE related functions
        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        
        // Gestore dell'evento send command da instradare tramite ble
        public async void OnSendBleCommand(object sender, SendCommandEventArgs e)
        {
            await HandleSendBleCommandAsync(sender, e);
        }

        private async Task HandleSendBleCommandAsync(object sender, SendCommandEventArgs e)
        {
            try
            {
                // Crea il pacchetto per l'applicationLayer
                byte[] AppData = { (byte)(e.Command >> 8), (byte)(e.Command) };

                // AL
                byte cmdInit = AppData[0]; // comando byte alto
                byte cmdOpt = AppData[1]; // comando byte basso
                byte[] payload = e.Payload;

                // TL
                byte cryptFlag = 0x00; // Nessuna crittografia

                // NL
                string interfaceType = "ble";
                int version = 1;
                uint recipientId = Form1.FormRef.RecipientId;

                // Crea il pacchetto di livello Network
                var networkLayer = new NetworkLayer(
                    interfaceType,
                    version,
                    recipientId,
                    new byte[] { cryptFlag, (byte)Form1.FormRef.senderId, (byte)(Form1.FormRef.senderId >> 8), (byte)(Form1.FormRef.senderId >> 16), (byte)(Form1.FormRef.senderId >> 24), 0, 0, cmdInit, cmdOpt }.Concat(payload).ToArray(),
                    true
                );

                // Stampa i dettagli           
                Form1.FormRef.UpdateTerminal("Invio Comando ble");
                //Form1.FormRef.UpdateTerminal("Comando Boot manager:");
                //Form1.FormRef.UpdateTerminal($"{string.Join(" ", networkLayer.ApplicationPacket.Select(b => b.ToString("X2")))}");

                // Ottieni i chunk da spedire
                var networkPackets = networkLayer.NetworkPackets;
                var packetManager = new PacketManager.PacketManager(Form1.FormRef.senderId);
                packetManager.Add_BLE_Channel(Form1.FormRef._BLE_SDL);

                // Invia i pacchetti tramite BLE in modo asincrono
                bool result = false;

                if (e.WaitAnswer)
                {
                    // Funzione di validazione della risposta
                    Func<byte[], bool> responseValidator = (data) =>
                    {
                        //il validatore di risposta nel caso della pagina firmware deve verificare anche che il numero di pagina sia corretto
                        if ((AppData[0] == 0) && (AppData[1] == 7))
                        {
                            return (
                            (data.Length > 0)
                            //comando
                            && (data[0] == (0x80 | AppData[0]))
                            && (data[1] == (AppData[1]))
                            ////tipo firmware per ora commentato, da attivare come parametro dall'esterno
                            //&& (data[2] == (payload[0]))
                            //&& (data[3] == (payload[1]))
                            //numero pagina
                            && (data[4] == (payload[2]))
                            && (data[5] == (payload[3]))
                            && (data[6] == (payload[4]))
                            && (data[7] == (payload[5]))
                            );
                        }
                        else return (
                            (data.Length > 0)
                            && (data[0] == (0x80 | AppData[0]))
                            && (data[1] == (AppData[1]))
                            );
                    };
                    result = await packetManager.SendBLEAndWaitForResponseAsync(networkPackets, responseValidator);
                }
                else
                {
                    result = await packetManager.SendThroughBLEAsync(networkPackets);
                }

                // Usa il risultato aggiornando il semaforo
                Answer_Received = true;
                Answer_Result = result;
            }
            catch (Exception ex)
            {
                // Gestione dell'eccezione
                Form1.FormRef.UpdateTerminal($"Errore durante l'invio del comando BLE: {ex.Message}");
            }
        }

    }


    //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
    //                  classi che incapuslano i parametri dei sendcommand 
    //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

    // Classe per incapsulare i parametri dell'evento sendcommand del protocollo stem
    public class SendCommandEventArgs : EventArgs
    {
        public ushort Command { get; }
        public byte[] Payload { get; }
        public bool WaitAnswer { get; }

        public SendCommandEventArgs(ushort command, byte[] payload, bool waitAnswer)
        {
            Command = command;
            WaitAnswer = waitAnswer;
            Payload = payload;
        }
    }
}

