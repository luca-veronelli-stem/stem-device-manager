using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows.Forms;
using Stem_Protocol;
using DocumentFormat.OpenXml.Drawing;
using static Stem_Protocol.NetworkLayer;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace Stem_Protocol.TelemetryManager;

public class TelemetryManager
{
    // Comando per la lettura della variabile
    private const ushort CMD_READ_VARIABLE = 0x0001;
    private const ushort CMD_WRITE_VARIABLE = 0x0002;

    // Gestore del protocollo
    private ProtocolManager protocolManager;

    //Lista variabili da richiedere
    private List<ExcelHandler.VariableData> TelemetryDictionary;
    private List<string> TelemetryDictionaryValues;

    //global rx packet manager
    private PacketManager.PacketManager rXPacketManager;

    //source address to ask variables
    uint sourceAddress;

    //my own address
    uint myAddress;

    public bool TelemetryOn;

    private string TelemetryHardwareChannel = "ble";

    //Eventi della classe
    public event EventHandler<DataReadyEventArgs>? DataReady;

    public TelemetryManager(PacketManager.PacketManager packetManager)
    {
        protocolManager = new ProtocolManager();
    //    protocolManager.SendCommandRequest += protocolManager.OnSendCanCommand; //per il momento forzo il can poi dovrò gestirlo coi canali attivi
        rXPacketManager = packetManager;
        rXPacketManager.OnAppLayerPacketReceived += onAppLayerPacketReady;
        TelemetryOn = false;
        TelemetryDictionary = new List<ExcelHandler.VariableData>();
        TelemetryDictionaryValues = new List<string>();
    }

    public void SetHardwareChannel(string channel)
    {
        TelemetryHardwareChannel = channel;
        //azzera le chiamate del protocl manager
        protocolManager.SendCommandRequest -= protocolManager.OnSendCanCommand;
        protocolManager.SendCommandRequest -= protocolManager.OnSendBleCommand;
        //aggiunge il canale di comunicazione
        if (TelemetryHardwareChannel == "can")
        {
            protocolManager.SendCommandRequest += protocolManager.OnSendCanCommand;
        }
        else if (TelemetryHardwareChannel == "ble")
        {
            protocolManager.SendCommandRequest += protocolManager.OnSendBleCommand;
        }
        else if (TelemetryHardwareChannel == "serial")
        {
            protocolManager.SendCommandRequest += protocolManager.OnSendSerialCommand;
        }
    }

    public void AddToDictionary(ExcelHandler.VariableData data)
    {
        TelemetryDictionary.Add(data);
    }

    public void AddToDictionaryForWrite(ExcelHandler.VariableData data, string value)
    {
        TelemetryDictionary.Add(data);
        TelemetryDictionaryValues.Add(value);
    }

    public void RemoveFromDictionary(int index)
    {
        TelemetryDictionary.RemoveAt(index);
    }

    public void ResetDictionary()
    {
        int Count = TelemetryDictionary.Count;
        for (int i=0; i<Count; i++) TelemetryDictionary.RemoveAt(0);

        Count = TelemetryDictionaryValues.Count;
        for (int i = 0; i < Count; i++) TelemetryDictionaryValues.RemoveAt(0);     
    }

    public void UpdateMyAddress(uint address)
    {
        myAddress = address;
    }

    public void UpdateSourceAddress(uint address)
    {
        sourceAddress = address;
    }

    public string GetVariableName(int index)
    {
        if (index < 0 || index >= TelemetryDictionary.Count)
            return "Index out of range";
        else 
            return TelemetryDictionary[index].Name;
    }

    public int GetVariableIndex(String Name)
    {
        for (int i= 0; i<TelemetryDictionary.Count; i++)
        {
            if (TelemetryDictionary[i].Name==Name) return i;
        }

        return -1;
    }

    public void onAppLayerPacketReady(object sender, PacketReadyEventArgs e)
    {
        // Accesso all'array di byte ricevuto
        byte[] payload = e.Packet;
        uint sourceAddressPackt = e.SourceAddress;
        uint destinationAddressPackt = e.DestinationAddress;
        uint Value = 0;
        if (payload.Length < 2) return;
        //prosegui solo se ricevi una risposta a lettura dalla sorgente per me
        if ((payload[0] == 0x80) && (payload[1] == 0x01) && (payload.Length > 4))
        {
            //Cerca nella lista TelemetryDictionary se esiste un elemento il cui corridpondente numerico della stringa addrH e addrL è uguale al payload[2] e payload[3]
            foreach (ExcelHandler.VariableData data in TelemetryDictionary)
            {
                if ((int.Parse(data.AddrH, System.Globalization.NumberStyles.HexNumber) == payload[2]) &&
                    (int.Parse(data.AddrL, System.Globalization.NumberStyles.HexNumber) == payload[3]))
                {
                    if (payload.Length == 5)
                    {
                        Value = payload[4]; //dato a byte
                    }
                    else if (payload.Length == 6)
                    {
                        Value = (((uint)payload[4] << 8) | ((uint)payload[5])); //dato a word
                    }
                    else if (payload.Length == 8)
                    {
                        Value = (((uint)payload[4] << 24) | ((uint)payload[5] << 16) | ((uint)payload[6] << 8) | ((uint)payload[7])); //dato a dword
                    }
                    //Aggiorna la label opportuna
                    DataReadyEventArgs dataReadyEventArgs = new DataReadyEventArgs(TelemetryDictionary.IndexOf(data), Value);
                    DataReady?.Invoke(this, dataReadyEventArgs);
                }
            }
        }
    }

    public async Task TelemetryStart()
    {
        TelemetryOn = true;
        await TelemetryRequestTask();
    }


    public async Task ReadOneShot()
    {
        TelemetryOn = true;
        await TelemetryRequestTaskOneShot();
    }

    public async Task WriteOneShot()
    {
        TelemetryOn = true;
        await TelemetryWriteTaskOneShot();
    }

    private int CurrentIndex = 0;

    public async Task TelemetryRequestTask()
    {
        while (TelemetryOn == true)
        {
            //richiedi una variabile alla volta in modo sequenziale del dizionario TelemetryDictionary
            if (CurrentIndex < TelemetryDictionary.Count - 1)
            {
                CurrentIndex++;
            }
            else
            {
                CurrentIndex = 0;
            }

            //crea un array di byte dove nei primi 2 bytes ci sono i valori Addrh e AddrL della variabile da richiedere dal TelemetryDictionary di indice CurrentIndex
            byte[] Data = new byte[] { Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrH, 16), Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrL, 16) };

            await protocolManager.SendCommand(CMD_READ_VARIABLE, Data, false);
            await Task.Delay(250);
        }
    }

    public async Task TelemetryRequestTaskOneShot()
    {
        while (TelemetryOn == true)
        {
            //richiedi una variabile alla volta in modo sequenziale del dizionario TelemetryDictionary
            if (CurrentIndex < TelemetryDictionary.Count - 1)
            {
                CurrentIndex++;
            }
            else
            {
                CurrentIndex = 0;
                TelemetryOn = false;
            }

            //crea un array di byte dove nei primi 2 bytes ci sono i valori Addrh e AddrL della variabile da richiedere dal TelemetryDictionary di indice CurrentIndex
            byte[] Data = new byte[] { Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrH, 16), Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrL, 16) };

            await protocolManager.SendCommand(CMD_READ_VARIABLE, Data, false);
            await Task.Delay(150);
        }
    }

    public async Task TelemetryWriteTaskOneShot()
    {
        while (TelemetryOn == true)
        {
            //richiedi una variabile alla volta in modo sequenziale del dizionario TelemetryDictionary
            if (CurrentIndex < TelemetryDictionary.Count - 1)
            {
                CurrentIndex++;
            }
            else
            {
                CurrentIndex = 0;
                TelemetryOn = false;
            }

            //e poi c'è il valore della variabile con la dimensione ricavata dal dizionario stem
            //switch (TelemetryDictionary[CurrentIndex].DataType)
            //{
            //    case "uint16_t":
            //        Data.Append(Convert.ToByte(TelemetryDictionaryValues[CurrentIndex], 10));
            //        break;
            //    default:
            //        break;
            //}

            if (int.TryParse(TelemetryDictionaryValues[CurrentIndex], out int valuetest) && valuetest >= 0 && valuetest <= 32767)
            {
                // Input valido
              
            }
            else
            {
                // Input non valido
                continue;
            }

            // Converte la stringa in ushort (valore 0–65535)
            ushort value = Convert.ToUInt16(TelemetryDictionaryValues[CurrentIndex], 10);

            // Converte in array di byte in little-endian (default)
            byte[] bytesVal = BitConverter.GetBytes(value);
            // Se l'architettura è little-endian, inverti l’ordine per ottenere big-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytesVal);
            }

            //crea un array di byte dove nei primi 2 bytes ci sono i valori Addrh e AddrL della variabile da richiedere dal TelemetryDictionary di indice CurrentIndex
            List<byte> data = new List<byte> { 
                Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrH, 16), 
                Convert.ToByte(TelemetryDictionary[CurrentIndex].AddrL, 16), };

            data.AddRange(bytesVal);

            // Se ti serve di nuovo un array:
            byte[] Data = data.ToArray();



            await protocolManager.SendCommand(CMD_WRITE_VARIABLE, Data, false);
            await Task.Delay(150);
        }
    }

    public void TelemetryStop()
    {
        TelemetryOn = false;
    }
}

// Classe per incapsulare i parametri dell'evento dataready del pacchetto di lettura variabile
public class DataReadyEventArgs : EventArgs
{
    public int ListIndex { get; }
    public uint Value { get; }

    public DataReadyEventArgs(int listIndex, uint value)
    {
        ListIndex = listIndex;
        Value = value;
    }
}

