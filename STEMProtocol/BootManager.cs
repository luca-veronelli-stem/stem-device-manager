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
        public event EventHandler<SendCanCommandEventArgs>? SendCanCommandRequest;


        // Comandi CAN proprietari per il bootloader
        private const ushort CMD_START_PROCEDURE = 0x0005;
        private const ushort CMD_PROGRAM_BLOCK = 0x0007;
        private const ushort CMD_END_PROCEDURE = 0x0006;
        private const ushort CMD_RESTART_MACHINE = 0x000A;

        // Dimensione blocco firmware
        private const int FIRMWARE_BLOCK_SIZE = 1024;

        //Path del firmware
        private string _firmwareName;
        private uint _recipientId;
        public int currentOffset;
        public int totalLength;
        private byte[] firmwareData;

        //variabili varie
        uint pageNum;
        ushort fwType = 5;

        public BootManager(uint RecipientId, string FirmwarePath)
        {
            _recipientId=RecipientId;
            _firmwareName=FirmwarePath;

            // Legge il firmware
            firmwareData = File.ReadAllBytes(_firmwareName);
            totalLength = firmwareData.Length;
            currentOffset = 0;
            // Aggiorna il progresso
            OnProgressChanged(currentOffset, totalLength);

        }

        //public async void btnSelectFirmware_Click(object sender, EventArgs e)
        //{
        //    using (OpenFileDialog openFileDialog = new OpenFileDialog())
        //    {
        //        openFileDialog.Filter = "Binary Files (*.bin)|*.bin|All files (*.*)|*.*";

        //        if (openFileDialog.ShowDialog() == DialogResult.OK)
        //        {
        //            try
        //            {
        //                await UploadFirmware(openFileDialog.FileName);
        //            }
        //            catch (Exception ex)
        //            {
        //                MessageBox.Show($"Errore durante l'upload: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            }
        //        }
        //    }
        //}

        public async Task UploadFirmware()
        {
            // 1. Avvio procedura
            for (int i = 0; i < 2; i++)
            {
                SendCanCommand(CMD_START_PROCEDURE, Array.Empty<byte>(), true);
                await Task.Delay(100); // attesa
            }
     
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

                for (int i = 0; i < 3; i++)
                {
                    // Invia il blocco
                    await SendFirmwareBlock(pageNum, currentBlock, (uint)FIRMWARE_BLOCK_SIZE);

                    await Task.Delay(450); // attesa tra un comando e il successivo

                    Form1.FormRef.UpdateTerminal($"{DateTime.Now:HH:mm:ss.fff} - Page={pageNum:X}");
                }

                currentOffset = offset;
                pageNum++;

                // Aggiorna progress bar
                OnProgressChanged(currentOffset, totalLength);
            }

            // 3. Comando di fine procedura
            for (int i = 0; i < 2; i++)
            {
                SendCanCommand(CMD_END_PROCEDURE, Array.Empty<byte>(), true);
                await Task.Delay(1000); // attesa tra un comando e il successivo
            }
            //await Task.Delay(1000); // attesa

            // 4. Comando di reset
            for (int i = 0; i < 2; i++)
            {
                SendCanCommand(CMD_RESTART_MACHINE, Array.Empty<byte>(), false);
                await Task.Delay(3000); // attesa tra un comando e il successivo
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
                byte[] Data= new byte[]{ 
                    (byte)(fwType >> 8), (byte)fwType, 
                    (byte)(pageNumber >> 24), (byte)(pageNumber >> 16), (byte)(pageNumber >> 8), (byte)(pageNumber),
                    (byte)(pageSize>> 24), (byte)(pageSize >> 16), (byte)(pageSize >> 8), (byte)(pageSize),
                    0x00, 0x00, 0x00, 0x00
                }.Concat(block).ToArray();

                // Invia blocco firmware al dispositivo CAN
                SendCanCommand(CMD_PROGRAM_BLOCK, Data, true);

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
        public void SendCanCommand(ushort command, byte[] payload, bool waitAnswer)
        {
            // Controlla se ci sono iscritti all'evento prima di invocarlo
            SendCanCommandRequest?.Invoke(this, new SendCanCommandEventArgs(command, payload, waitAnswer));
        }

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

