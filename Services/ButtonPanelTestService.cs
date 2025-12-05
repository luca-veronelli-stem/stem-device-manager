using STEMPM.Core.ButtonPanelEnums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;

using Stem_Protocol;
using Stem_Protocol.PacketManager;
using StemPC;
using static StemPC.Form1;

namespace STEMPM.Services
{
    // Implementazione del servizio di test delle pulsantiere
    internal class ButtonPanelTestService : IButtonPanelTestService
    {
        // TODO : Estrarre questi comandi, variabili e valori in una repository
        private const ushort CMD_WRITE_VARIABLE = 0x0002;
        private const ushort VAR_GREEN_LED = 0x8002;
        private const ushort VAR_RED_LED = 0x8003;
        private const ushort VAR_BUZZER = 0x8004;
        private readonly byte[] VALUE_OFF = { 0x00, 0x00, 0x00, 0x00 };
        private readonly byte[] VALUE_ON = { 0x00, 0x00, 0x00, 0x80 };
        private readonly byte[] VALUE_SINGLE_BLINK = { 0x00, 0xFF, 0x80, 0x61 };

        private const int BUTTON_PRESS_TIMEOUT_MS = 10000;

        // Costruisce payload
        private byte[] BuildPayload(
            ushort command, 
            ushort variableId, 
            byte[]? value)
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
        private async Task<byte[]> SendCommandAsync(
            ushort command, 
            byte[] fullPayload, 
            bool waitAnswer, 
            int timeoutMs = 5000)
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
        private async Task HandleSendCommandInternal(
            ushort command, 
            byte[] payload, 
            bool waitAnswer)
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
        public async Task<bool> AwaitButtonPressEventAsync(
            byte[] expectedPayload, 
            int timeoutMs, 
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeoutMs);

            void OnAppLayerDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                if (e.Payload.SequenceEqual(expectedPayload))
                {
                    tcs.TrySetResult(true);
                }
            }

            FormRef.AppLayerCommandDecoded += OnAppLayerDecoded;

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                FormRef.AppLayerCommandDecoded -= OnAppLayerDecoded;
            }
        }

        // Esegue tutti i test disponibili per una pulsantiera specifica e restituisce i risultati
        public async Task<List<ButtonPanelTestResult>> TestAllAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default)
        {
            var panel = ButtonPanel.GetByType(panelType);
            var results = new List<ButtonPanelTestResult>
            {
                await TestButtonsAsync(panelType, userPrompt, onButtonStart, onButtonResult, cancellationToken)
            };

            if (panel.HasLed)
                results.Add(await TestLedAsync(panelType, userConfirm, cancellationToken));

            if (panel.HasBuzzer)
                results.Add(await TestBuzzerAsync(panelType, userConfirm, cancellationToken));

            return results;
        }

        // Esegue il collaudo dei pulsanti della pulsantiera
        public async Task<ButtonPanelTestResult> TestButtonsAsync(
            ButtonPanelType panelType,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default)
        {
            var panel = ButtonPanel.GetByType(panelType);
            bool allPassed = true;
            string message = "";

            for (int i = 0; i < panel.ButtonCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                onButtonStart?.Invoke(i);

                byte buttonCode = panel.ButtonMasks[i];
                byte[] expectedPayload = [0x00, 0x02, 0x80, 0x00, buttonCode];

                await userPrompt($"Premi il pulsante {panel.Buttons[i]}");

                cancellationToken.ThrowIfCancellationRequested();

                bool passed = await AwaitButtonPressEventAsync(expectedPayload, BUTTON_PRESS_TIMEOUT_MS, cancellationToken);

                onButtonResult?.Invoke(i, passed);

                allPassed &= passed;
                message += $"- Pulsante {panel.Buttons[i]}: {(passed ? "PASSATO" : "FALLITO")}\n";
            }

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Buttons,
                Passed = allPassed,
                Message = message.Trim()
            };
        }

        // Esegue il collaudo dei LED della pulsantiera
        public async Task<ButtonPanelTestResult> TestLedAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default)
        {
            bool passed = true;

            // Collaudo accensione LED verde
            var onPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_GREEN_LED, VALUE_ON);
            await SendCommandAsync(CMD_WRITE_VARIABLE, onPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            passed &= await userConfirm("Il LED verde è acceso?");

            // Collaudo spegnimento LED verde
            var offPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_GREEN_LED, VALUE_OFF);
            await SendCommandAsync(CMD_WRITE_VARIABLE, offPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            passed &= await userConfirm("Il LED verde è spento?");

            // Collaudo accensione LED rosso
            onPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_RED_LED, VALUE_ON);
            await SendCommandAsync(CMD_WRITE_VARIABLE, onPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            passed &= await userConfirm("Il LED rosso è acceso?");

            // Collaudo spegnimento LED rosso
            offPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_RED_LED, VALUE_OFF);
            await SendCommandAsync(CMD_WRITE_VARIABLE, offPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            passed &= await userConfirm("Il LED rosso è spento?");

            // Collaudo accensione simultanea LED verde e rosso
            onPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_GREEN_LED, VALUE_ON);
            await SendCommandAsync(CMD_WRITE_VARIABLE, onPayload, waitAnswer: true);
            onPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_RED_LED, VALUE_ON);
            await SendCommandAsync(CMD_WRITE_VARIABLE, onPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            passed &= await userConfirm("Entrambi i LED (verde e rosso) sono accesi?");

            // Spegni i LED alla fine del test
            offPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_RED_LED, VALUE_OFF);
            await SendCommandAsync(CMD_WRITE_VARIABLE, offPayload, waitAnswer: true);
            offPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_GREEN_LED, VALUE_OFF);
            await SendCommandAsync(CMD_WRITE_VARIABLE, offPayload, waitAnswer: true);

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Led,
                Passed = passed,
                Message = (passed ? "LED funziona correttamente" : "LED non rilevato") + Environment.NewLine
            };
        }

        // Esegue il test del buzzer della pulsantiera
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default)
        {
            // Attiva buzzer per mezzo secondo
            var onPayload = BuildPayload(CMD_WRITE_VARIABLE, VAR_BUZZER, VALUE_SINGLE_BLINK);
            await SendCommandAsync(CMD_WRITE_VARIABLE, onPayload, waitAnswer: true);
            cancellationToken.ThrowIfCancellationRequested();
            bool passed = await userConfirm("Hai sentito il buzzer suonare?");

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Buzzer,
                Passed = passed,
                Message = (passed ? "Buzzer funziona correttamente" : "Buzzer non rilevato")
            };
        }
    }
}
