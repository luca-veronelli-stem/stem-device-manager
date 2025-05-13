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
        //private const int FIRMWARE_BLOCK_SIZE = 128;

        //Path del firmware
        private string firmwareName="";
        public int currentOffset;
        public int totalLength;
        private byte[] firmwareData;

        //variabili varie
        private uint pageNum;
        private ushort fwType = 5;

        private string BootHardwareChannel = "can";

        private ProtocolManager protocolManager;

        public BootManager()
        {
            protocolManager = new ProtocolManager();
      //      protocolManager.SendCommandRequest += protocolManager.OnSendCanCommand; //per il momento forzo il can poi dovrò gestirlo coi canali attivi
        }

        public void SetHardwareChannel(string channel)
        {
            BootHardwareChannel = channel;
            //azzera le chiamate del protocol manager
            protocolManager.SendCommandRequest -= protocolManager.OnSendCanCommand;
            protocolManager.SendCommandRequest -= protocolManager.OnSendBleCommand;
            //aggiunge il canale di comunicazione
            if (BootHardwareChannel == "can")
            {
                protocolManager.SendCommandRequest += protocolManager.OnSendCanCommand;
            }else if (BootHardwareChannel == "ble")
            {
                protocolManager.SendCommandRequest += protocolManager.OnSendBleCommand;
            }
        }

        public void SetFirmwarePath(string FirmwareName)
        {
            // Legge il firmware
            firmwareData = File.ReadAllBytes(FirmwareName);
            totalLength = firmwareData.Length;

            //Legge il tipo di firmware facendo il cast a 16 bit litle endian dei byte 14 e 15
            fwType = (ushort)((firmwareData[15] << 8) | firmwareData[14]);

            currentOffset = 0;
            // Aggiorna il progresso
            OnProgressChanged(currentOffset, totalLength);
        }

        public async Task StartBoot()
        {
            bool Answer=false;

            // 1. Avvio procedura
            for (int i = 0; i < 1; i++)
            {
                Answer = false;
                Answer = await protocolManager.SendCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
                if (Answer == true)
                {
                    MessageBox.Show("Bootloader avviato!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                await Task.Delay(100); // attesa
            }
            MessageBox.Show("Risposta al comando non ricevuta!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public async Task UploadFirmware()
        {
            // 1. Avvio procedura
            bool Answer = false;

            // 1. Avvio procedura
            for (int i = 0; i < 1; i++)
            {
                Answer = false;
                Answer = await protocolManager.SendCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
                if (Answer == true)
                {
                    break;
                }
                await Task.Delay(100); // attesa
            }
            if (Answer == false)
            {
                MessageBox.Show("Boot startup failed!", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 2. Ciclo di programmazione blocchi
            pageNum = 0;
            bool UpdateSuccesful = true;

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

                // Invia il blocco
                bool success = await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE); ;
                if (!success)
                {
                    Console.WriteLine($"Error: Programming failed at page {pageNum}");
                    MessageBox.Show($"Error: Programming failed at page {pageNum}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Qui puoi decidere di interrompere il processo o eseguire un'azione di fallback
                    UpdateSuccesful = false;
                    break;
                }

                Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
                currentOffset = offset;
                pageNum++;

                // Aggiorna progress bar
                OnProgressChanged(currentOffset, totalLength);
            }

            // 3. Comando di fine procedura
            Answer = false;

            // Aggiorna progress bar
            OnProgressChanged(totalLength, totalLength); //100%

            if (UpdateSuccesful) {
                for (int i = 0; i < 5; i++)
                {
                    Answer = false;
                    Answer = await protocolManager.SendCommand(CMD_END_PROCEDURE, Array.Empty<byte>(), true);
                    if (Answer == true) break;
                    await Task.Delay(100); // attesa
                }

                // 4. Comando di reset
                for (int i = 0; i < 2; i++)
                {
                    await protocolManager.SendCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);
                    await Task.Delay(1000); // attesa tra un comando e il successivo
                }

         //       MessageBox.Show("Firmware update completed!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }        
        }

        public async Task UploadFirmwareOnly()
        {
            // 2. Ciclo di programmazione blocchi
            pageNum = 0;
            bool UpdateSuccesful = true;

            //  int offset = 0;
            for (int offset = 0; offset < firmwareData.Length; offset += FIRMWARE_BLOCK_SIZE)
            {

                // Prepara il blocco corrente
                byte[] currentBlock = new byte[FIRMWARE_BLOCK_SIZE];

                // Riempimento iniziale di currentBlock con 0xFF
                Array.Fill(currentBlock, (byte)0xFF);

                byte[] currentBlockShrinked = GetCurrentBlock(firmwareData, offset);

                // Copia dei dati di currentBlockShrinked in currentBlock
                Array.Copy(currentBlockShrinked, currentBlock, currentBlockShrinked.Length);

                // Invia il blocco
                bool success = await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE); ;
                if (!success)
                {
                    Console.WriteLine($"Errore: Programmazione fallita alla pagina {pageNum}");
                    MessageBox.Show($"Errore: Programmazione fallita alla pagina {pageNum}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Qui puoi decidere di interrompere il processo o eseguire un'azione di fallback
                    UpdateSuccesful = false;
                    break;
                }
                //     await Task.Delay(50); // attesa tra un comando e il successivo
                Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");

                currentOffset = offset;
                pageNum++;

                // Aggiorna progress bar
                OnProgressChanged(currentOffset, totalLength);
            }

            // Aggiorna progress bar
            OnProgressChanged(totalLength, totalLength); //100%

            if (UpdateSuccesful)
            {
                MessageBox.Show("Aggiornamento firmware completato!", "Successo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        public async Task EndBoot()
        {
            // 3. Comando di fine procedura
            bool Answer = false;
            for (int i = 0; i < 1; i++)
            {
                Answer = false;
                Answer = await protocolManager.SendCommand(CMD_END_PROCEDURE, Array.Empty<byte>(), true);
                if (Answer == true)
                {
                    MessageBox.Show("Bootloader terminato!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }
                await Task.Delay(100); // attesa
            }

            MessageBox.Show("Risposta al comando non ricevuta!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public async Task Restart()
        {
            // 4. Comando di reset
            protocolManager.SendCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);

            //for (int i = 0; i < 2; i++)
            //{
            //    await protocolManager.SendCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);
            //    await Task.Delay(1000); // attesa tra un comando e il successivo
            //}
        }

        private byte[] GetCurrentBlock(byte[] firmwareData, int offset)
        {
            int remainingBytes = Math.Min(FIRMWARE_BLOCK_SIZE, firmwareData.Length - offset);
            byte[] block = new byte[remainingBytes];

            Array.Copy(firmwareData, offset, block, 0, remainingBytes);
            return block;
        }


        private async Task<bool> SendFirmwareBlock(uint pageNumber, byte[] block, uint pageSize)
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
                    Answer = await protocolManager.SendCommand(CMD_PROGRAM_BLOCK, Data, true);
                    if (Answer == true)
                    {
                        break;
                    }
                }
                return Answer;
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

