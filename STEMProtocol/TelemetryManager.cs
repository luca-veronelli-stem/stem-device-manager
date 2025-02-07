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

namespace Stem_Protocol.TelemetryManager;

public class TelemetryManager
{
    // Gestore del protocollo
    private ProtocolManager protocolManager;

    //Lista variabili da richiedere
    private List<ExcelHandler.VariableData> TelemetryDictionary;

    //global rx packet manager
    private PacketManager.PacketManager rXPacketManager;

    //source address to ask variables
    uint sourceAddress;

    //my own address
    uint myAddress;

    public bool TelemetryOn;

    //Eventi della classe
    public event EventHandler<DataReadyEventArgs>? DataReady;

    public TelemetryManager(PacketManager.PacketManager packetManager)
    {
        protocolManager = new ProtocolManager();
        protocolManager.SendCanCommandRequest += protocolManager.OnSendCanCommand; //per il momento forzo il can poi dovrò gestirlo coi canali attivi
        rXPacketManager = packetManager;
        rXPacketManager.OnAppLayerPacketReceived += onAppLayerPacketReady;
        TelemetryOn = false;
        TelemetryDictionary = new List<ExcelHandler.VariableData>();
    }

    public void AddToDictionary(ExcelHandler.VariableData data)
    {
        TelemetryDictionary.Add(data);
    }

    public void RemoveFromDictionary(int index)
    {
        TelemetryDictionary.RemoveAt(index);
    }

    public void UpdateMyAddress(uint address)
    {
        myAddress = address;
    }

    public void UpdateSourceAddress(uint address)
    {
        sourceAddress = address;
    }

    public void onAppLayerPacketReady(object sender, PacketReadyEventArgs e)
    {
        // Accesso all'array di byte ricevuto
        byte[] payload = e.Packet;
        uint sourceAddressPackt = e.SourceAddress;
        uint destinationAddressPackt = e.DestinationAddress;
        uint Value = 0;

        //prosegui solo se ricevi una risposta a lettura dalla sorgente per me
        if ((payload[0] == 0x80) && (payload[1] == 0x01)&&(payload.Length > 4))
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
            DataReadyEventArgs dataReadyEventArgs = new DataReadyEventArgs(0, Value);
            DataReady?.Invoke(this, dataReadyEventArgs);
        }
    }

    public async Task TelemetryStart()
    {
        TelemetryOn = true;
        await TelemetryRequestTask();
    }

    public async Task TelemetryRequestTask()
    {
        while (TelemetryOn == true)
        {
            await Task.Delay(1000);
        }
    }

    public void TelemetryStop()
    {
        TelemetryOn = false;
    }
    //public async Task StartBoot()
    //{
    //    bool Answer=false;

    //    // 1. Avvio procedura
    //    for (int i = 0; i < 10; i++)
    //    {
    //        Answer = false;
    //        Answer = await protocolManager.SendCanCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
    //        if (Answer == true) break;
    //        await Task.Delay(100); // attesa
    //    }
    //}

    //public async Task UploadFirmware()
    //{
    // //   return;

    //    //// 1. Avvio procedura
    //    //for (int i = 0; i < 2; i++)
    //    //{
    //    //    SendCanCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
    //    //    await Task.Delay(100); // attesa
    //    //}

    //    // 2. Ciclo di programmazione blocchi
    //    pageNum = 0;

    //  //  int offset = 0;
    //    for (int offset = 0; offset < firmwareData.Length; offset += FIRMWARE_BLOCK_SIZE)
    //    {

    //        // Prepara il blocco corrente
    //        byte[] currentBlock = new byte[FIRMWARE_BLOCK_SIZE];

    //        // Riempimento iniziale di currentBlock con 0xFF
    //        Array.Fill(currentBlock, (byte) 0xFF);

    //        byte[] currentBlockShrinked = GetCurrentBlock(firmwareData, offset);

    //        // Copia dei dati di currentBlockShrinked in currentBlock
    //        Array.Copy(currentBlockShrinked, currentBlock, currentBlockShrinked.Length);

    //        //if (pageNum == 0)
    //        //{
    //        //    for (int i = 0; i < 8; i++)
    //        //    {
    //        //        // Invia il blocco
    //        //        await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE);

    //        //        await Task.Delay(400); // attesa tra un comando e il successivo

    //        //        Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
    //        //    }
    //        //}
    //        //else
    //        //{
    //        //    for (int i = 0; i < 2; i++)
    //        //    {
    //                // Invia il blocco
    //                await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE);

    //           //     await Task.Delay(50); // attesa tra un comando e il successivo

    //                Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
    //     //       }
    // //       }

    //        currentOffset = offset;
    //        pageNum++;

    //        // Aggiorna progress bar
    //        OnProgressChanged(currentOffset, totalLength);
    //    //    break;
    //    }

    //    // 3. Comando di fine procedura
    //    bool Answer = false;

    //    // Aggiorna progress bar
    //    OnProgressChanged(totalLength, totalLength); //100%

    //    for (int i = 0; i < 5; i++)
    //    {
    //        Answer = false;
    //        Answer = await protocolManager.SendCanCommand(CMD_END_PROCEDURE, Array.Empty<byte>(), true);
    //        if (Answer == true) break;
    //        await Task.Delay(100); // attesa
    //    }

    //    // 4. Comando di reset
    //    for (int i = 0; i < 2; i++)
    //    {
    //        await protocolManager.SendCanCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);
    //        await Task.Delay(1000); // attesa tra un comando e il successivo
    //    }

    //    MessageBox.Show("Aggiornamento firmware completato!", "Successo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    //}

    //private byte[] GetCurrentBlock(byte[] firmwareData, int offset)
    //{
    //    int remainingBytes = Math.Min(FIRMWARE_BLOCK_SIZE, firmwareData.Length - offset);
    //    byte[] block = new byte[remainingBytes];

    //    Array.Copy(firmwareData, offset, block, 0, remainingBytes);
    //    return block;
    //}


    //private async Task SendFirmwareBlock(uint pageNumber, byte[] block, uint pageSize)
    //{
    //    try
    //    {
    //        //Crea il comando invia pagina 
    //        byte[] Data = new byte[]{
    //            (byte)(fwType >> 8), (byte)fwType,
    //            (byte)(pageNumber >> 24), (byte)(pageNumber >> 16), (byte)(pageNumber >> 8), (byte)(pageNumber),
    //            (byte)(pageSize>> 24), (byte)(pageSize >> 16), (byte)(pageSize >> 8), (byte)(pageSize),
    //            0x00, 0x00, 0x00, 0x00
    //        }.Concat(block).ToArray();

    //        bool Answer = false;

    //        // Invia blocco firmware al dispositivo CAN
    //        for (int i = 0; i < 10; i++)
    //        {
    //            Answer = false;
    //            Answer = await protocolManager.SendCanCommand(CMD_PROGRAM_BLOCK, Data, true);
    //            if (Answer == true) break;
    //        }         
    //    }
    //    catch (Exception ex)
    //    {
    //        throw new Exception($"Errore durante l'invio della pagina {pageNumber}: {ex.Message}");
    //    }
    //}

    //protected virtual void OnProgressChanged(int currentOffset, int totalLength)
    //{
    //    ProgressChanged?.Invoke(this, new ProgressEventArgs(currentOffset, totalLength));
    //}
}

// Classe per incapsulare i parametri dell'evento dataready del pacchetto di lettura variabile
public class DataReadyEventArgs : EventArgs
{
    public ushort ListIndex { get; }
    public uint Value { get; }

    public DataReadyEventArgs(ushort listIndex, uint value)
    {
        ListIndex = listIndex;
        Value = value;
    }
}

