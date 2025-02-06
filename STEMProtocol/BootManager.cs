using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows.Forms;
using Stem_Protocol;
using StemPC;
using DocumentFormat.OpenXml.Drawing;

namespace Stem_Protocol.BootManager
{
    public class BootManager
    {
        //Eventi della classe
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        // Comandi CAN proprietari per il bootloader
        private const ushort CMD_START_PROCEDURE = 0x0005;
        private const ushort CMD_PROGRAM_BLOCK = 0x0007;
        private const ushort CMD_END_PROCEDURE = 0x0006;
        private const ushort CMD_RESTART_MACHINE = 0x000A;

        // Dimensione blocco firmware
        private const int FIRMWARE_BLOCK_SIZE = 1024;

        //Path del firmware
        private string firmwareName="";
        public int currentOffset;
        public int totalLength;
        private byte[] firmwareData;

        //variabili varie
        private uint pageNum;
        private ushort fwType = 5;

        private ProtocolManager protocolManager;

        public BootManager()
        {
            protocolManager = new ProtocolManager();
            BootHndlr.SendCanCommandRequest += OnSendCanCommand;
            AnswerReceivedFlag += BootHndlr.AnswerReceived;
        }

        public void SetFirmwarePath(string FirmwareName)
        {
            // Legge il firmware
            firmwareData = File.ReadAllBytes(FirmwareName);
            totalLength = firmwareData.Length;
            currentOffset = 0;
            // Aggiorna il progresso
            OnProgressChanged(currentOffset, totalLength);
        }

        public async Task StartBoot()
        {
            bool Answer=false;

            // 1. Avvio procedura
            for (int i = 0; i < 10; i++)
            {
                Answer = false;
                Answer = await SendCanCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
                if (Answer == true) break;
                await Task.Delay(100); // attesa
            }
        }

        public async Task UploadFirmware()
        {
         //   return;

            //// 1. Avvio procedura
            //for (int i = 0; i < 2; i++)
            //{
            //    SendCanCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
            //    await Task.Delay(100); // attesa
            //}
     
            // 2. Ciclo di programmazione blocchi
            pageNum = 0;

          //  int offset = 0;
            for (int offset = 0; offset < firmwareData.Length; offset += FIRMWARE_BLOCK_SIZE)
            {

                // Prepara il blocco corrente
                byte[] currentBlock = new byte[FIRMWARE_BLOCK_SIZE];

                // Riempimento iniziale di currentBlock con 0xFF
                Array.Fill(currentBlock, (byte) 0xFF);

                byte[] currentBlockShrinked = GetCurrentBlock(firmwareData, offset);

                // Copia dei dati di currentBlockShrinked in currentBlock
                Array.Copy(currentBlockShrinked, currentBlock, currentBlockShrinked.Length);

                //if (pageNum == 0)
                //{
                //    for (int i = 0; i < 8; i++)
                //    {
                //        // Invia il blocco
                //        await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE);

                //        await Task.Delay(400); // attesa tra un comando e il successivo

                //        Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
                //    }
                //}
                //else
                //{
                //    for (int i = 0; i < 2; i++)
                //    {
                        // Invia il blocco
                        await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE);

                   //     await Task.Delay(50); // attesa tra un comando e il successivo

                        Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
             //       }
         //       }

                currentOffset = offset;
                pageNum++;

                // Aggiorna progress bar
                OnProgressChanged(currentOffset, totalLength);
            //    break;
            }

            // 3. Comando di fine procedura
            bool Answer = false;

            // Aggiorna progress bar
            OnProgressChanged(totalLength, totalLength); //100%

            for (int i = 0; i < 5; i++)
            {
                Answer = false;
                Answer = await SendCanCommand(CMD_END_PROCEDURE, Array.Empty<byte>(), true);
                if (Answer == true) break;
                await Task.Delay(100); // attesa
            }

            // 4. Comando di reset
            for (int i = 0; i < 2; i++)
            {
                SendCanCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);
                await Task.Delay(1000); // attesa tra un comando e il successivo
            }

            MessageBox.Show("Aggiornamento firmware completato!", "Successo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private byte[] GetCurrentBlock(byte[] firmwareData, int offset)
        {
            int remainingBytes = Math.Min(FIRMWARE_BLOCK_SIZE, firmwareData.Length - offset);
            byte[] block = new byte[remainingBytes];

            Array.Copy(firmwareData, offset, block, 0, remainingBytes);
            return block;
        }

    
        private async Task SendFirmwareBlock(uint pageNumber, byte[] block, uint pageSize)
        {
            try
            {
                //Crea il comando invia pagina 
                byte[] Data = new byte[]{
                    (byte)(fwType >> 8), (byte)fwType,
                    (byte)(pageNumber >> 24), (byte)(pageNumber >> 16), (byte)(pageNumber >> 8), (byte)(pageNumber),
                    (byte)(pageSize>> 24), (byte)(pageSize >> 16), (byte)(pageSize >> 8), (byte)(pageSize),
                    0x00, 0x00, 0x00, 0x00
                }.Concat(block).ToArray();

                bool Answer = false;

                // Invia blocco firmware al dispositivo CAN
                for (int i = 0; i < 10; i++)
                {
                    Answer = false;
                    Answer = await SendCanCommand(CMD_PROGRAM_BLOCK, Data, true);
                    if (Answer == true) break;
                }         
            }
            catch (Exception ex)
            {
                throw new Exception($"Errore durante l'invio della pagina {pageNumber}: {ex.Message}");
            }
        }

        protected virtual void OnProgressChanged(int currentOffset, int totalLength)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(currentOffset, totalLength));
        }

        // Metodo per attivare l'evento SendCanCommand
 

        //private void UpdateProgressBar(int currentOffset, int totalLength)
        //{
        //    // Aggiorna la progress bar in modo thread-safe
        //    if (progressBarFirmware.InvokeRequired)
        //    {
        //        progressBarFirmware.Invoke(new Action(() =>
        //            progressBarFirmware.Value = (int)((double)currentOffset / totalLength * 100)));
        //    }
        //    else
        //    {
        //        progressBarFirmware.Value = (int)((double)currentOffset / totalLength * 100);
        //    }
        //}

        //public async Task SendFirmwareBlock(byte[] block, int offset)
        //{
        //    // TODO: Implementare invio blocco firmware tramite CAN
        //    // Includere logica di indirizzamento e sequenziamento
        //    await Task.CompletedTask;
        //}

        //public async Task SendCommand(ushort command)
        //{
        //    // TODO: Implementare logica di invio comando specifico per protocollo CAN
        //    // Potrebbe utilizzare una libreria come PEAK-System, Vector, ecc.
        //    await Task.CompletedTask;
        //}
    }

    // Classe per incapsulare i parametri dell'evento progresschanged
    public class ProgressEventArgs : EventArgs
    {
        public int CurrentOffset { get; }
        public int TotalLength { get; }

        public ProgressEventArgs(int currentOffset, int totalLength)
        {
            CurrentOffset = currentOffset;
            TotalLength = totalLength;
        }
    }

    // Classe per incapsulare i parametri dell'evento sendcommand del protocollo stem
    public class SendCanCommandEventArgs : EventArgs
    {
        public ushort Command { get; }
        public byte[] Payload { get; }
        public bool WaitAnswer { get; }

        public SendCanCommandEventArgs(ushort command, byte[] payload, bool waitAnswer)
        {
            Command = command;
            WaitAnswer = waitAnswer;
            Payload = payload;
        }
    }
}

