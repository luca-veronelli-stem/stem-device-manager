using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Diagnostics;
using Infrastructure.Protocol.Hardware;

namespace Infrastructure.Protocol.Legacy
{
    /// <summary>
    /// Implementazione <see cref="IBleDriver"/> basata su Plugin.BLE (Xamarin/MAUI).
    /// Vive in Legacy/ perché è un driver di transizione: sarà rimpiazzato
    /// quando Stem.Communication NuGet sarà disponibile. Nessuna dipendenza da UI.
    /// </summary>
    public class BLEManager : IBleDriver
    {
        /// <summary>
        /// Evento che viene sollevato quando viene scoperto un nuovo dispositivo BLE.
        /// Il parametro è il nome del dispositivo.
        /// </summary>
        public event Action<string>? OnDeviceDiscovered;
        public event Action<IDevice>? OnConnectionEstablished;
        public event Action? OnScanCompleted;
        public event EventHandler<BlePacketEventArgs>? PacketReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>
        /// Evento per messaggi di log destinati al terminale UI. Consente al driver di
        /// restare disaccoppiato dal Form1 (sostituisce il precedente Form1.FormRef.UpdateTerminal).
        /// </summary>
        public event Action<string>? LogMessageEmitted;

        /// <summary>
        /// Errori non-fatali propagati al consumer UI (es. BLE non abilitato, dispositivo
        /// non trovato, servizio/caratteristiche mancanti). Parametri: (titolo, messaggio).
        /// </summary>
        public event Action<string, string>? ErrorOccurred;

        // Adapter BLE
        private IBluetoothLE ble;
        private IAdapter adapter;

        // Lista dei dispositivi scoperti
        private Dictionary<Guid, string> discoveredDevices = new Dictionary<Guid, string>();

        // Dispositivo e caratteristiche connesse
        private IDevice connectedDevice;
        private IService nordicUartService;
        private ICharacteristic rxCharacteristic;
        private ICharacteristic txCharacteristic;

        // UUID del servizio Nordic UART e delle caratteristiche
        private static readonly Guid NordicUartServiceUuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid RxCharacteristicUuid = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E"); // scrivere al dispositivo
        private static readonly Guid TxCharacteristicUuid = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E"); // ricevere dal dispositivo

        public BLEManager()
        {
            // Inizializza l'adapter BLE
            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;

            // Registra gli eventi dell'adapter
            adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
            adapter.ScanTimeoutElapsed += Adapter_ScanTimeoutElapsed;
            ble.StateChanged += Ble_StateChanged;

            //// Sottoscrivi il cambiamento dello stato Bluetooth globale
            //ble.StateChanged += (s, e) =>
            //{
            //    Debug.WriteLine($"Stato BLE cambiato globalmente: {e.NewState}");
            //};
        }

        private void Ble_StateChanged(object sender, BluetoothStateChangedArgs e)
        {
            Debug.WriteLine($"Stato BLE cambiato: {e.NewState}");
            if (e.NewState != BluetoothState.On)
            {
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        private void Adapter_DeviceDiscovered(object sender, DeviceEventArgs e)
        {
            // Verifica se il dispositivo ha un nome valido
            if (!string.IsNullOrEmpty(e.Device.Name))
            {
                // Verifica se il dispositivo supporta il servizio Nordic UART
                // Nota: Plugin.BLE può non esporre immediatamente i servizi durante la scansione
                // quindi potremmo dover controllare questo durante la connessione

                // Aggiungi il dispositivo alla lista se non è già presente
                if (!discoveredDevices.ContainsKey(e.Device.Id))
                {
                    discoveredDevices.Add(e.Device.Id, e.Device.Name);
                    OnDeviceDiscovered?.Invoke(e.Device.Name);
                }
            }
        }

        private void Adapter_ScanTimeoutElapsed(object sender, EventArgs e)
        {
            OnScanCompleted?.Invoke();
        }

        /// <summary>
        /// Avvia la scansione dei dispositivi BLE.
        /// </summary>
        public async Task StartScanningAsync(int timeoutMilliseconds = 10000)
        {
            if (ble.State != BluetoothState.On)
            {
                ErrorOccurred?.Invoke("Errore BLE", "Bluetooth non abilitato: abilitalo e riprova.");
                return;
            }

            // Pulisci la lista dei dispositivi scoperti
            discoveredDevices.Clear();

            try
            {
                // Imposta un filtro per il servizio Nordic UART (se supportato dalla piattaforma)
                var options = new Plugin.BLE.Abstractions.ScanFilterOptions
                {
                    ServiceUuids = new[] { NordicUartServiceUuid }
                };

                // Avvia la scansione con timeout
                // await adapter.StartScanningForDevicesAsync((timeoutMilliseconds: timeoutMilliseconds);

                await adapter.StartScanningForDevicesAsync(options); //scansiona solo i dispositivi con il servizio Nordic UART
                // await adapter.StartScanningForDevicesAsync(); //scansiona tutti i dispositivi senza filtro
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Errore BLE", $"Errore durante la scansione BLE: {ex.Message}");
            }
        }

        /// <summary>
        /// Ferma la scansione dei dispositivi BLE.
        /// </summary>
        public async Task StopScanningAsync()
        {
            try
            {
                await adapter.StopScanningForDevicesAsync();
                OnScanCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore nell'arresto della scansione: {ex.Message}");
            }
        }

        /// <summary>
        /// Connette al dispositivo BLE specificato per nome.
        /// </summary>
        public async Task ConnectToAsync(string deviceName, bool ConnectWithResponse)
        {
            try
            {
                // Ferma la scansione
                await StopScanningAsync();

                // Trova il dispositivo per nome
                var deviceEntry = discoveredDevices.FirstOrDefault(d => d.Value == deviceName);
                if (deviceEntry.Key == Guid.Empty)
                {
                    ErrorOccurred?.Invoke("Errore Connessione", "Dispositivo non trovato.");
                    return;
                }

                // Ottieni il dispositivo
                IDevice device = await adapter.ConnectToKnownDeviceAsync(deviceEntry.Key);

                if (device == null || device.State != DeviceState.Connected)
                {
                    ErrorOccurred?.Invoke("Errore Connessione", "Impossibile connettersi al dispositivo BLE.");
                    return;
                }

                // Registra l'evento di disconnessione
                //  device.ConnectionStatusChanged += Device_ConnectionStatusChanged;

                // Cerca il servizio Nordic UART
                nordicUartService = await device.GetServiceAsync(NordicUartServiceUuid);
                if (nordicUartService == null)
                {
                    ErrorOccurred?.Invoke("Errore Servizio", "Servizio Nordic UART non trovato sul dispositivo.");
                    await adapter.DisconnectDeviceAsync(device);
                    return;
                }

                // Ottieni le caratteristiche RX e TX
                rxCharacteristic = await nordicUartService.GetCharacteristicAsync(RxCharacteristicUuid);
                txCharacteristic = await nordicUartService.GetCharacteristicAsync(TxCharacteristicUuid);

                if (rxCharacteristic == null || txCharacteristic == null)
                {
                    ErrorOccurred?.Invoke("Errore Caratteristiche", "Caratteristiche UART necessarie non trovate.");
                    await adapter.DisconnectDeviceAsync(device);
                    return;
                }

                // Verifica che le caratteristiche abbiano le proprietà necessarie
                if (!rxCharacteristic.CanWrite)
                {
                    ErrorOccurred?.Invoke("Errore Caratteristiche", "La caratteristica RX non supporta la scrittura.");
                    await adapter.DisconnectDeviceAsync(device);
                    return;
                }

                if (!txCharacteristic.CanUpdate)
                {
                    ErrorOccurred?.Invoke("Errore Caratteristiche", "La caratteristica TX non supporta le notifiche.");
                    await adapter.DisconnectDeviceAsync(device);
                    return;
                }

                // Abilita le notifiche sulla caratteristica TX
                txCharacteristic.ValueUpdated += TxCharacteristic_ValueUpdated;
                await txCharacteristic.StartUpdatesAsync();

                if (ConnectWithResponse == false)
                {
                    // Abilita la scrittura della caratterisitca rx senza risposta per accelerare il flusso
                    rxCharacteristic.WriteType = CharacteristicWriteType.WithoutResponse;
                }
                else
                    rxCharacteristic.WriteType = CharacteristicWriteType.WithResponse;

                // Memorizza il dispositivo per uso futuro
                connectedDevice = device;

                // Notifica la connessione stabilita
                OnConnectionEstablished?.Invoke(device);
                ConnectionStatusChanged?.Invoke(this, true);

                Debug.WriteLine($"Connesso al dispositivo: {device.Name} ({device.Id})");
                LogMessageEmitted?.Invoke($"Connesso al dispositivo: {device.Name} ({device.Id})");

                MonitorDeviceConnection();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Errore Connessione", $"Errore durante la connessione: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        //private void Device_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        //{
        //    if (e.Status == ConnectionStatus.Disconnected)
        //    {
        //        Debug.WriteLine("Dispositivo disconnesso");
        //        ConnectionStatusChanged?.Invoke(this, false);
        //    }
        //}

        private void MonitorDeviceConnection()
        {
            Task.Run(async () =>
            {
                while (connectedDevice != null && connectedDevice.State == DeviceState.Connected)
                {
                    await Task.Delay(1000); // Controlla ogni secondo
                }

                if (connectedDevice != null && connectedDevice.State != DeviceState.Connected)
                {
                    Debug.WriteLine("Dispositivo disconnesso");
                    ConnectionStatusChanged?.Invoke(this, false);
                }
            });
        }

        private void TxCharacteristic_ValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
        {
            try
            {
                // Leggi i dati dalla caratteristica
                byte[] data = e.Characteristic.Value;

                // Crea l'evento per i dati ricevuti
                var packetEvent = new BlePacketEventArgs(data, DateTime.Now);

                Debug.WriteLine($"Dati ble ricevuti: {data.Length} bytes");
                Debug.WriteLine("Bytes: " + BitConverter.ToString(data));

                // Solleva l'evento
                PacketReceived?.Invoke(this, packetEvent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore nella gestione dei dati ricevuti: {ex.Message}");
            }
        }

        /// <summary>
        /// Invia un messaggio al dispositivo BLE connesso.
        /// </summary>
        /// <param name="data">Array di byte da inviare</param>
        /// <returns>True se l'invio è riuscito, False altrimenti</returns>
        public async Task<bool> SendMessageAsync(byte[] data)
        {
            // Verifica se il dispositivo è connesso
            if (connectedDevice == null || connectedDevice.State != DeviceState.Connected)
            {
                ErrorOccurred?.Invoke("Errore BLE TX", "Dispositivo BLE non connesso.");
                return false;
            }

            // Verifica se abbiamo una caratteristica RX valida
            if (rxCharacteristic == null || !rxCharacteristic.CanWrite)
            {
                ErrorOccurred?.Invoke("Errore BLE TX",
                    "Caratteristica RX non disponibile o non scrivibile.");
                return false;
            }

            try
            {
                await rxCharacteristic.WriteAsync(data);
                Debug.WriteLine($"Invio dati riuscito: {data.Length} bytes");
                return true;
            }
            catch (Exception ex)
            {
                // Propaga il messaggio di errore reale al consumer UI (BlePort → ProtocolService)
                // invece di silenziarlo: è essenziale per diagnosticare problemi di device
                // specifici (MTU, caratteristiche, permissioni).
                ErrorOccurred?.Invoke("Errore BLE TX",
                    $"WriteAsync fallita ({data.Length} byte, WriteType={rxCharacteristic.WriteType}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnette dal dispositivo BLE corrente.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (connectedDevice != null)
                {
                    // Disabilita le notifiche
                    if (txCharacteristic != null)
                    {
                        txCharacteristic.ValueUpdated -= TxCharacteristic_ValueUpdated;
                        await txCharacteristic.StopUpdatesAsync();
                    }

                    // Disconnetti dal dispositivo
                    await adapter.DisconnectDeviceAsync(connectedDevice);

                    // Pulisci i riferimenti
                    connectedDevice = null;
                    nordicUartService = null;
                    rxCharacteristic = null;
                    txCharacteristic = null;

                    ConnectionStatusChanged?.Invoke(this, false);
                    Debug.WriteLine("Dispositivo disconnesso con successo");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante la disconnessione: {ex.Message}");
            }
        }

        /// <summary>
        /// Ottiene lo stato attuale della connessione BLE.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return connectedDevice != null && connectedDevice.State == DeviceState.Connected;
            }
        }

        /// <summary>
        /// Ottiene lo stato del Bluetooth.
        /// </summary>
        public string BluetoothStateString
        {
            get
            {
                return ble.State.ToString();
            }
        }
    }
}
