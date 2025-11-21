using Stem_Protocol;
using Stem_Protocol.PacketManager;
using StemPC;
using STEMPM.Core.Enums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;
using static StemPC.Form1;


namespace STEMPM.Services
{
    // Implementazione del servizio di test delle pulsantiere
    internal class ButtonPanelTestService : IButtonPanelTestService
    {

        private const int BUTTON_PRESS_TIMEOUT_MS = 10000;

        // Costruisce payload
        private byte[] BuildPayload(ushort command, ushort variableId, byte[]? value)
        {
            var payload = new List<byte>
            {
                (byte)(command >> 8), (byte)command,
                (byte)(variableId >> 8), (byte)variableId
            };
            if (value != null)
            {
                payload.AddRange(value);
            }
            return payload.ToArray();
        }

        // Invia comando via protocollo STEM
        private async Task<byte[]> SendCommandAsync(ushort command, byte[] fullPayload, bool waitAnswer, int timeoutMs = 5000)
        {
            if (!waitAnswer)
            {
                await HandleSendCommandInternal(command, fullPayload, false);
                return Array.Empty<byte>();
            }

            var tcs = new TaskCompletionSource<byte[]>();
            var cts = new CancellationTokenSource(timeoutMs);

            void OnAppLayerDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                byte cmdInit = (byte)(command >> 8);
                byte cmdOpt = (byte)command;
                if (e.Payload.Length >= 2 && e.Payload[0] == (0x80 | cmdInit) && e.Payload[1] == cmdOpt)
                {
                    tcs.TrySetResult(e.Payload.Skip(2).ToArray());
                }
            }

            try
            {
                Form1.FormRef.AppLayerCommandDecoded += OnAppLayerDecoded;

                await HandleSendCommandInternal(command, fullPayload, true);

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == tcs.Task)
                {
                    cts.Cancel();
                    return await tcs.Task;
                }
                else
                {
                    throw new TimeoutException("Timeout attesa risposta dal dispositivo.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Errore invio comando: {ex.Message}");
            }
            finally
            {
                Form1.FormRef.AppLayerCommandDecoded -= OnAppLayerDecoded;
                cts.Dispose();
            }
        }

        // Gestisce l'invio del comando
        private async Task HandleSendCommandInternal(ushort command, byte[] payload, bool waitAnswer)
        {
            byte cmdInit = (byte)(command >> 8);
            byte cmdOpt = (byte)command;
            byte cryptFlag = 0x00; // Nessuna crittografia
            string interfaceType = Form1.FormRef.CommunicationPort; // "can", "ble", "serial"
            int version = 1;
            uint recipientId = Form1.FormRef.RecipientId;
            uint senderId = Form1.FormRef.senderId;

            // Transport data: crypt + sender + len + app (ma usa tuo constructor)
            var transportData = new byte[] { cryptFlag }
                .Concat(BitConverter.GetBytes(senderId).Reverse()) // Big-endian?
                .Concat(new byte[2]) // Placeholder len (NetworkLayer lo calcola)
                .Concat(new byte[] { cmdInit, cmdOpt })
                .Concat(payload.Skip(2).ToArray()) // Skip command se già in payload? No, payload include command
                .ToArray();

            var networkLayer = new NetworkLayer(interfaceType, version, recipientId, transportData, true);

            var packetManager = new PacketManager(senderId);
            var networkPackets = networkLayer.NetworkPackets;

            // Aggiungi canale CAN
            packetManager.Add_CAN_Channel(Form1.FormRef._CDL);

            // Validator per ACK (da tuo codice)
            Func<byte[], bool> responseValidator = data =>
                data.Length >= 2 && data[0] == (0x80 | cmdInit) && data[1] == cmdOpt;

            bool sent;
            sent = await packetManager.SendCANAndWaitForResponseAsync(networkPackets, responseValidator, 5000);

            if (!sent)
            {
                throw new Exception("Fallito invio comando.");
            }
        }

        // Gestisce la rilevazione della prezzione del pulsante
        private async Task<bool> AwaitButtonPressEventAsync(byte[] expectedPayload, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(timeoutMs);

            void OnAppLayerDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                if (e.Payload.SequenceEqual(expectedPayload))
                {
                    tcs.TrySetResult(true);
        }
        }

            Form1.FormRef.AppLayerCommandDecoded += OnAppLayerDecoded;

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == tcs.Task)
                {
                    return true;
        }
                return false;
            }
            finally
            {
                Form1.FormRef.AppLayerCommandDecoded -= OnAppLayerDecoded;
                cts.Dispose();
            }
        }

        // Esegue tutti i test disponibili per una pulsantiera specifica e restituisce i risultati
        public async Task<List<ButtonPanelTestResult>> TestAllAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm, Func<string, Task> userPrompt)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);

            List<ButtonPanelTestResult> results = new List<ButtonPanelTestResult>
            {
                await TestButtonsAsync(panelType, userPrompt)
            };

            if (panel.HasLed)
            {
                results.Add(await TestLedAsync(panelType, userConfirm));
            }

            if (panel.HasBuzzer)
            {
                results.Add(await TestBuzzerAsync(panelType, userConfirm));
            }

            return results;
        }

        // Esegue il test dei pulsanti della pulsantiera
        public async Task<ButtonPanelTestResult> TestButtonsAsync(ButtonPanelType panelType, Func<string, Task> userPrompt)
        {
            var panel = ButtonPanel.GetByType(panelType);
            bool allPassed = true;
            string message = "";

            for (int i = 1; i <= panel.ButtonCount; i++)
            {
                // Calcola expected payload (da tuo esempio: [00 02 80 00 XX] dove XX = 1 << (i-1))
                byte buttonCode = (byte)(1 << (i - 1)); // 0x01, 0x02, 0x04, 0x08, ...
                byte[] expectedPayload = new byte[] { 0x00, 0x02, 0x80, 0x00, buttonCode };

                // Prompt utente
                await userPrompt($"Premi il pulsante {i}");

                // Await event con matching payload
                bool passed = await AwaitButtonPressEventAsync(expectedPayload, BUTTON_PRESS_TIMEOUT_MS);

                allPassed &= passed;
                message += $"Pulsante {i}: {(passed ? "OK" : "Fallito")};";
            }

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Buttons,
                Passed = allPassed,
                Message = message.TrimEnd(';')
            };
        }

        // Esegue il collaudo dei LED della pulsantiera
        public async Task<ButtonPanelTestResult> TestLedAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del buzzer della pulsantiera
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm)
        {
            throw new NotImplementedException();
        }
    }
}
