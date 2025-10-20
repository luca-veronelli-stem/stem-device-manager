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
    // Comando per la lettura della variabile (telemetri lenta)
    private const ushort CMD_READ_VARIABLE = 0x0001;
    private const ushort CMD_WRITE_VARIABLE = 0x0002;

    // Comandi per l'impostazione e l'avvio della telmetria e ricezione dei dati (telemetria veloce)
    private const ushort CMD_CONFIGURE_TELEMETRY = 0x0015;
    private const ushort CMD_START_TELEMETRY = 0x0016;
    private const ushort CMD_STOP_TELEMETRY = 0x0017;
    private const ushort CMD_TELEMETRY_DATA = 0x0018;

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

        //TELEMETRIA LENTA

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

        //TELEMETRIA VELOCE

        //prosegui solo se ricevi un pacchetto di telemetria
        if ((payload[0] == 0x00) && (payload[1] == 0x18) && (payload.Length > 6))
        {
            //Prosegui solo se il tipo di telemetria è zero
            if ((payload[2] == 0x00) && (payload[3] == 0x00) && (payload[4] == 0x00) && (payload[5] == 0x00))
            {
                int dataPosition = 6;
                //Cerca nella lista TelemetryDictionary come interpretare la coda dei dati del payload
                foreach (ExcelHandler.VariableData data in TelemetryDictionary)
                {
                    //      //calcola l'indirizzo logico della variabile
                    //       int addrLogical = (int.Parse(data.AddrH, System.Globalization.NumberStyles.HexNumber) << 8) | (int.Parse(data.AddrL, System.Globalization.NumberStyles.HexNumber));
                    //verifica che la posizione del dato sia valida
                    if (dataPosition < payload.Length)
                    {
                        string dataType = data.DataType.Trim();

                        if (dataType == "uint8_t")
                        {
                            Value = payload[dataPosition]; //dato a byte
                        }
                        else if (dataType == "uint16_t")
                        {
                            //i dati di telemetria sono in little endian
                            Value = (((uint)payload[dataPosition + 1] << 8) | ((uint)payload[dataPosition])); //dato a word
                        }
                        else if (dataType == "uint32_t")
                        {
                            //i dati di telemetria sono in little endian
                            Value = (((uint)payload[dataPosition + 3] << 24) | ((uint)payload[dataPosition + 2] << 16) | ((uint)payload[dataPosition + 1] << 8) | ((uint)payload[dataPosition])); //dato a dword
                        }
                        //Aggiorna la label opportuna
                        DataReadyEventArgs dataReadyEventArgs = new DataReadyEventArgs(TelemetryDictionary.IndexOf(data), Value);
                        DataReady?.Invoke(this, dataReadyEventArgs);

                        //prepara il dataposition per la variabile successiva
                        //calcola la posizione del dato nel payload
                        dataPosition = dataPosition + ((data.DataType == "uint8_t") ? 1 : (data.DataType == "uint16_t") ? 2 : 4);
                    }
                }
            }
        }


    }

    public async Task TelemetryStart()
    {
        TelemetryOn = true;

        //avvia telemetria veloce

        //configura il dizionario nella telemetria veloce

        //Il comando di configurazione è così composto:
        //byte 0->3 Tipo telemetria HH Tipo telemetria HL Tipo telemetria LH	Tipo telemetria LL
        //Il tipo è un codice univoco deciso dall'amministratore tramite portale all'atto del configurare la telemetria e viene usato per la decodifica dei dati binari"		
        //byte 4->7 Indirizzo Destinazione HH Indirizzo Destinazione HL	Indirizzo Destinazione LH	Indirizzo Destinazione LL
        //Indirizzo del protocollo Stem a cui inviare i messaggi di telemetria (è il mio indirizzo myAddress)
        //byte 8 Istanza Telemetria: è il numero del task in cui faccio girare questa configurazione di telemetria	(normalemnte è 0)
        //byte 9->10 Periodo ms H	Periodo ms L
        //byte 11->14	"Indirizzo Scheda HH Indirizzo Scheda HL	Indirizzo Scheda LH	Indirizzo Scheda LL
        //(normalmente è l'indirizzo della scheda da cui prelevi,  ma potrebbe essere anche quello di un'altra)
        //byte 15->16
        //Indirizzo Logico Data1 H Indirizzo Logico Data1 L
        // …	
        //Indirizzo Logico Data16 H Indirizzo Logico Data16 L
        //(Il numero di variabili è dinamico e al massimo vale SP_TEL_N_VARS che viene configurato nel firmware. Normalmente vale 16)
        //Gli indirizzi che vengono spediti sono quelli del TelemetryDictionary	

        //costruisci il payload del comando di configurazione come da descrizione precedente

        byte[] Payload = new byte[4 + 4 + 1 + 2 + 4 + (TelemetryDictionary.Count * 2)];
        //tipo telemetria (0 default)
        Payload[0] = 0x00;
        Payload[1] = 0x00;
        Payload[2] = 0x00;
        Payload[3] = 0x00;
        //indirizzo destinazione
        Payload[4] = (byte)((myAddress >> 24) & 0xFF);
        Payload[5] = (byte)((myAddress >> 16) & 0xFF);
        Payload[6] = (byte)((myAddress >> 8) & 0xFF);
        Payload[7] = (byte)(myAddress & 0xFF);
        //istanza telemetria
        Payload[8] = (byte)(0x00);
        //periodo ms (100ms)
        Payload[9] = (byte)(0x00);
        Payload[10] = (byte)(0xC8);
        //indirizzo scheda (sourceAddress)
        Payload[11] = (byte)((sourceAddress >> 24) & 0xFF);
        Payload[12] = (byte)((sourceAddress >> 16) & 0xFF);
        Payload[13] = (byte)((sourceAddress >> 8) & 0xFF);
        Payload[14] = (byte)(sourceAddress & 0xFF);
        //indirizzi logici delle variabili del TelemetryDictionary
        for (int i = 0; i < TelemetryDictionary.Count; i++)
        {
            Payload[15 + (i * 2)] = Convert.ToByte(TelemetryDictionary[i].AddrH, 16);
            Payload[16 + (i * 2)] = Convert.ToByte(TelemetryDictionary[i].AddrL, 16);
        }
        await protocolManager.SendCommand(CMD_CONFIGURE_TELEMETRY, Payload, false);
        await Task.Delay(150);

        byte[] PayloadStart = new byte[1];
        PayloadStart[0] = 0x00; //istanza telemetria da avviare

        await protocolManager.SendCommand(CMD_START_TELEMETRY, PayloadStart, false);
        await Task.Delay(150);

        ////avvia telemetria lenta
        //await TelemetryRequestTask();
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

    public async void TelemetryStop()
    {
        TelemetryOn = false;

        byte[] PayloadStop = new byte[1];
        PayloadStop[0] = 0x00; //istanza telemetria da fermare

        await protocolManager.SendCommand(CMD_STOP_TELEMETRY, PayloadStop, false);
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

