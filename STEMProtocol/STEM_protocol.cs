using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using StemPC;

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
                _packetChunkSize = 100;
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
        packetId=Form1.FormRef.RollingCodeGen.GetIndex();
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
            if (chunk.Length < _packetChunkSize)
            {
                Array.Resize(ref chunk, _packetChunkSize);
            }
            chunks.Add(chunk);
        }
        return chunks;
    }

    public void SP_PacketReady()
    {
        //Se il transport layer è a posto

        Form1.FormRef.DecodeCommandSP(ApplicationPacket);
        //decodifica l'app layer e mettilo nel RichTextBoxTx

    }
}

