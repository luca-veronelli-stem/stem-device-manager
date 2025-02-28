using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Linq;
using System.Diagnostics;
using PCAN_Handler;
using CanDataLayer;

namespace BLE_Handler;

// Classe per la gestione degli eventi di ricezione pacchetti
public class BLEPacketEventArgs : EventArgs
{
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public BLEPacketEventArgs(byte[] data, DateTime timestamp)
    {
        Data = data;
        Timestamp = timestamp;
    }
}

public class BLEManager
{
    /// <summary>
    /// Evento che viene sollevato quando viene scoperto un nuovo dispositivo BLE.
    /// Il parametro è il nome del dispositivo.
    /// </summary>
    public event Action<string> OnDeviceDiscovered;
    public event Action<BluetoothLEDevice> OnConnectionEstablished;
    public event Action OnScanCompleted;
    public event EventHandler<BLEPacketEventArgs> PacketReceived;
    public event EventHandler<bool> ConnectionStatusChanged;

    private BluetoothLEAdvertisementWatcher watcher;
    //Lista dei dispositivi scoperti
    private Dictionary<ulong, string> discoveredDevices = new Dictionary<ulong, string>();
    ulong btAddress=1;
    private BluetoothLEDevice connectedDevice;
    private GattCharacteristic rxCharacteristic;

    public BLEManager()
    {
        // discoveredDevices = new HashSet<string>();
        discoveredDevices = new Dictionary<ulong, string>();

        // Inizializza il watcher per il BLE in modalità Active
        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += Watcher_Received;

        watcher.Stopped += Watcher_Stopped; // Evento per il termine della scansione

    }


//    // Usa l'indirizzo Bluetooth per identificare univocamente il dispositivo
//    ulong btAddress = args.BluetoothAddress;

//// Se non abbiamo ancora questo dispositivo, lo aggiungiamo
//if (!discoveredDevices.ContainsKey(btAddress))
//{
//    // Se il nome è ancora vuoto, lo inseriamo comunque (per aggiornamenti futuri)
//    discoveredDevices.Add(btAddress, deviceName);

//    // Se il nome è disponibile, notifico subito
//    if (!string.IsNullOrEmpty(deviceName))
//    {
//        OnDeviceDiscovered?.Invoke(deviceName);
//}
//}
//else
//{
//    // Se abbiamo già registrato il dispositivo ma il nome era vuoto,
//    // e ora è disponibile (da scan response), aggiornalo e notifica l'interfaccia
//    if (string.IsNullOrEmpty(discoveredDevices[btAddress]) && !string.IsNullOrEmpty(deviceName))
//    {
//        discoveredDevices[btAddress] = deviceName;
//        OnDeviceDiscovered?.Invoke(deviceName);
//    }
//}

/// <summary>
/// Gestore dell'evento che viene chiamato ad ogni pubblicità ricevuta.
/// Se il dispositivo pubblicizza un nome (LocalName), lo trasmette tramite l’evento.
/// </summary>
        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args.IsScanResponse==false)
            {
                // Definisci l'UUID del Nordic UART Service
                Guid nordicUARTUuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
                // Filtra: esci se il dispositivo non pubblicizza l'UUID specificato
                if (!args.Advertisement.ServiceUuids.Contains(nordicUARTUuid))
                {
                    return;
                }
                else
                {
                    // Usa l'indirizzo Bluetooth per identificare univocamente il dispositivo
                    btAddress = args.BluetoothAddress;

                    // Se non abbiamo ancora questo dispositivo, lo aggiungiamo
                    if (!discoveredDevices.ContainsKey(btAddress))
                    {
                        // Se il nome è ancora vuoto, lo inseriamo comunque (per aggiornamenti futuri)
                        discoveredDevices.Add(btAddress, "");
                    }
                }
            }
            else
            {
                // Recupera il nome dal pacchetto pubblicitario
                var deviceName = args.Advertisement.LocalName;

                // Considera solo i dispositivi che hanno un nome non vuoto
                if (!string.IsNullOrEmpty(deviceName))
                {
                    // Se abbiamo già registrato il dispositivo ma il nome era vuoto,
                    // e ora è disponibile (da scan response), aggiornalo e notifica l'interfaccia
                    if (string.IsNullOrEmpty(discoveredDevices[btAddress]) && !string.IsNullOrEmpty(deviceName))
                    {
                        discoveredDevices[btAddress] = deviceName;
                        OnDeviceDiscovered?.Invoke(deviceName);
                    }
                }
                //// Se il nome non è già presente, lo aggiunge all'insieme e solleva l'evento
                //if (discoveredDevices.Add(deviceName))
                //    {
                //        OnDeviceDiscovered?.Invoke(deviceName);
                //    }
                //}
            }
        }   

        /// <summary>
        /// Avvia la scansione dei dispositivi BLE.
        /// </summary>
        public void StartScanning()
        {
            // Pulisce i dispositivi scoperti in precedenza
            discoveredDevices.Clear();
            try
            {
                watcher.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BLE not enabled: enable it and retry. {ex.Message}", "BLE Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Ferma la scansione dei dispositivi BLE.
        /// </summary>
        public void StopScanning()
        {
            watcher.Stop();
        }

        private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            OnScanCompleted?.Invoke(); // Notifica la fine della scansione
        }

    //SEZIONE CONNESIONE GATT
    public async Task ConnectTo(string deviceName)
    {
        StopScanning();

        var entry = discoveredDevices.FirstOrDefault(d => d.Value == deviceName);
        if (entry.Key != 0)
        {
            ulong btAddress = entry.Key;
            // Connetti al dispositivo BLE
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(btAddress);
            if (device == null)
            {
                MessageBox.Show("Impossibile connettersi al dispositivo BLE.");
                return;
            }

            // Aggiungi dopo la creazione del dispositivo
            device.ConnectionStatusChanged += (sender, args) =>
            {
                Debug.WriteLine($"Stato connessione: {device.ConnectionStatus}");
            };

            //// Imposta la sessione di riconnessione automatica
            //await device.GetDeviceAccessAsync();

  //          await Task.Delay(1000); // aspetta 1 s

            // Aggiungi dopo aver ottenuto il dispositivo
            var services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (services.Status != GattCommunicationStatus.Success)
            {
                MessageBox.Show("Impossibile ottenere i servizi GATT.");
                return;
            }

            // Polling della connessione per un massimo di 10 secondi (20 tentativi da 500 ms)
            int retry = 0;
            while (device.ConnectionStatus != BluetoothConnectionStatus.Connected && retry < 10)
            {
                await Task.Delay(100); // aspetta 500 ms
                retry++;
            }
            if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                MessageBox.Show("La connessione non è stata stabilita in tempo.");
                return;
            }

            

            // Ottieni il Nordic UART Service
            Guid nordicUartServiceGuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
            var servicesResult = await device.GetGattServicesForUuidAsync(nordicUartServiceGuid);
            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                MessageBox.Show("Servizio Nordic UART non trovato sul dispositivo.");
                return;
            }

            var uartService = servicesResult.Services.First();


            // Recupera le caratteristiche del servizio
            var characteristicsResult = await uartService.GetCharacteristicsAsync();
            if (characteristicsResult.Status != GattCommunicationStatus.Success)
            {
                MessageBox.Show("Impossibile recuperare le caratteristiche del servizio.");
                return;
            }

            // UUID delle caratteristiche tipiche
            Guid rxCharacteristicGuid = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E"); // scrittura
            Guid txCharacteristicGuid = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E"); // notifica

            var rxCharacteristic = characteristicsResult.Characteristics.FirstOrDefault(c => c.Uuid == rxCharacteristicGuid);
            var txCharacteristic = characteristicsResult.Characteristics.FirstOrDefault(c => c.Uuid == txCharacteristicGuid);

            // Nel metodo ConnectTo, modifica la parte finale:
            if (txCharacteristic != null && rxCharacteristic != null)
            {
                // Abilita le notifiche per ricevere dati
                txCharacteristic.ValueChanged += TxCharacteristic_ValueChanged;
                var status = await txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (status != GattCommunicationStatus.Success)
                {
                    MessageBox.Show("Errore nell'abilitare le notifiche.");
                }

                // Memorizza il dispositivo e la caratteristica RX per uso futuro
                StoreConnectedDevice(device, rxCharacteristic);

                // La connessione è stabilita: genera l'evento
                //OnConnectionEstablished?.Invoke(device);
                //public event EventHandler<bool> ConnectionStatusChanged;
                ConnectionStatusChanged?.Invoke(this, true);
}

            //if (txCharacteristic != null)
            //{
            //    // Abilita le notifiche per ricevere dati
            //    txCharacteristic.ValueChanged += TxCharacteristic_ValueChanged;
            //    var status = await txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            //    if (status != GattCommunicationStatus.Success)
            //    {
            //        MessageBox.Show("Errore nell'abilitare le notifiche.");
            //    }
            //    // La connessione è stabilita: genera l'evento
            //    OnConnectionEstablished?.Invoke(device);
            //}

            // Ora puoi inviare dati al dispositivo utilizzando rxCharacteristic.WriteValueAsync
        }
        else
        {
            MessageBox.Show("Dispositivo non trovato.");
        }     
    }

    private void TxCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        // Gestisci i dati ricevuti (ad esempio, converti gli bytes in stringa)
        DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
        byte[] input = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(input);

        // Esegui l'interpretazione dei dati ricevuti...
        var packetEvent = new BLEPacketEventArgs(
            input,
            DateTime.Now
        );

        PacketReceived?.Invoke(this, packetEvent);
    }

    // Memorizza il dispositivo connesso e la caratteristica RX
    public void StoreConnectedDevice(BluetoothLEDevice device, GattCharacteristic rx)
    {
        connectedDevice = device;
        rxCharacteristic = rx;
    }

    /// <summary>
    /// Invia un messaggio al dispositivo BLE connesso.
    /// </summary>
    /// <param name="data">Array di byte da inviare</param>
    /// <returns>True se l'invio è riuscito, False altrimenti</returns>
    
    public async Task<bool> SendMessageAsync(byte[] data)
    {
        // Verifica se il dispositivo è connesso
        if (connectedDevice == null || connectedDevice.ConnectionStatus != BluetoothConnectionStatus.Connected)
        {
            Debug.WriteLine("Impossibile inviare dati: dispositivo non connesso");
            return false;
        }

        // Verifica se abbiamo una caratteristica RX valida
        if (rxCharacteristic == null)
        {
            Debug.WriteLine("Impossibile inviare dati: caratteristica RX non disponibile");
            return false;
        }

        try
        {
            // Crea un DataWriter per inviare i dati
            using (DataWriter writer = new DataWriter())
            {
                // Scrivi i dati nel buffer
                writer.WriteBytes(data);

                // Invia i dati
                GattCommunicationStatus status =
                    await rxCharacteristic.WriteValueAsync(writer.DetachBuffer());

                // Controlla lo stato dell'invio
                bool success = (status == GattCommunicationStatus.Success);

                // Log del risultato
                Debug.WriteLine($"Invio dati {(success ? "riuscito" : "fallito")}: {data.Length} bytes");

                return success;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Errore durante l'invio dei dati: {ex.Message}");
            return false;
        }
    }
}

