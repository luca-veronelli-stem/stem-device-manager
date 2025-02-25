using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace BLE_Handler;

public class BLEManager
    {
        /// <summary>
        /// Evento che viene sollevato quando viene scoperto un nuovo dispositivo BLE.
        /// Il parametro è il nome del dispositivo.
        /// </summary>
    public event Action<string> OnDeviceDiscovered;
    public event Action OnScanCompleted;

    private BluetoothLEAdvertisementWatcher watcher;
        // Utilizzato per evitare duplicati nella lista dei dispositivi scoperti
       // private HashSet<string> discoveredDevices;
        private Dictionary<ulong, string> discoveredDevices = new Dictionary<ulong, string>();

        ulong btAddress=1;

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
}

