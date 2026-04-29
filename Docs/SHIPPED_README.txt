================================================================================
					   STEM DEVICE MANAGER v0.3.0
							  STEM E.m.s.
================================================================================

DESCRIZIONE
-----------
Applicazione desktop per la gestione, diagnostica e comunicazione con i
dispositivi STEM tramite il protocollo proprietario multi-canale (CAN, BLE,
Seriale).

Funzioni principali:
- Comunicazione CAN (PCAN USB), BLE e Seriale
- Lettura/scrittura variabili e comandi via dizionari (API Azure + fallback Excel)
- Aggiornamento firmware (bootloader classico + batch SPARK)
- Telemetria in tempo reale (lenta + veloce) con grafici
- Generatore di codice (sp_config.h)


REQUISITI
---------
- Windows 10/11 (64-bit)
- PCAN USB (opzionale, solo per comunicazione CAN; richiede driver Peak)
- Bluetooth 4.0+ (opzionale, solo per BLE)
- Nessuna installazione richiesta (eseguibile self-contained)


AVVIO
-----
Doppio click su: GUI.Windows.exe

NOTA: al primo avvio Windows SmartScreen potrebbe segnalare l'eseguibile
come "non riconosciuto". Cliccare "Ulteriori informazioni" -> "Esegui comunque".


================================================================================
						SELEZIONE VARIANTE DISPOSITIVO
================================================================================

A partire dalla v0.3.0 la variante (TopLift / Eden / Egicon-SPARK / Generic)
si seleziona a runtime, senza ricompilare.

VARIANTI SUPPORTATE:
--------------------
- Generic    (default - tutti i dispositivi STEM standard)
- TopLift    (TopLift A2 - canale di default: CAN)
- Eden       (Eden XP)
- Egicon     (SPARK / Egicon - update firmware in batch via BLE)

PROCEDURA:
1. Aprire Windows PowerShell ed eseguire UNO dei comandi seguenti:
		[Environment]::SetEnvironmentVariable("Device__Variant", "TopLift", "User")
		[Environment]::SetEnvironmentVariable("Device__Variant", "Eden", "User")
		[Environment]::SetEnvironmentVariable("Device__Variant", "Egicon", "User")
		[Environment]::SetEnvironmentVariable("Device__Variant", "Generic", "User")

   In alternativa da Prompt dei comandi:
		setx Device__Variant "TopLift"

2. Chiudere e riaprire l'applicazione per applicare le modifiche.

3. La variante attiva e' visibile nel titolo della finestra principale.

NOTA: il doppio underscore "__" separa la sezione dalla chiave (sintassi
.NET Configuration). Se la variabile non e' impostata o contiene un valore
non valido, viene usata la variante "Generic".


================================================================================
					   CONFIGURAZIONE API DIZIONARI (AZURE)
================================================================================

L'applicazione scarica i dizionari di comandi/variabili dall'API Azure
"Stem.Dictionaries.Manager". I valori di default sono gia' inclusi e
funzionano senza configurazione aggiuntiva (la chiave di test e' embedded).

Per sovrascrivere la configurazione (chiave personale, endpoint diverso,
timeout piu' alto) impostare le seguenti variabili d'ambiente:

VARIABILI OPZIONALI:
--------------------
1. DictionaryApi__ApiKey
   Chiave API per l'autenticazione. Se non configurata viene usata quella
   di default embedded nel file appsettings.json.

2. DictionaryApi__BaseUrl
   URL base dell'API (default:
   https://app-dictionaries-manager-prod.azurewebsites.net/).

3. DictionaryApi__TimeoutSeconds
   Timeout in secondi per le chiamate HTTP (default: 30).

4. Device__SenderId
   Indirizzo STEM del mittente (default: 8). Cambiarlo solo se richiesto
   esplicitamente dal supporto.

PROCEDURA:
1. Richiedere la chiave API personale al contatto di supporto (se necessario).
2. Aprire Windows PowerShell ed eseguire:
		[Environment]::SetEnvironmentVariable("DictionaryApi__ApiKey", "<API_KEY>", "User")
   Oppure da Prompt dei comandi:
		setx DictionaryApi__ApiKey "<API_KEY>"
3. Chiudere e riaprire l'applicazione.

Nota: sostituire <API_KEY> con la chiave fornita.


================================================================================
						 USO OFFLINE
================================================================================

Se l'API Azure non e' raggiungibile (rete assente, chiave revocata, server
down), l'applicazione passa automaticamente al dizionario Excel embedded:

- I dizionari di comandi/variabili vengono letti dal file Excel incorporato
  nell'eseguibile (nessun file esterno richiesto).
- Tutte le funzionalita' di comunicazione (CAN/BLE/Serial), bootloader e
  telemetria continuano a funzionare normalmente.
- L'unica limitazione: i dizionari embedded potrebbero non includere le
  variabili piu' recenti aggiunte sul portale Azure.

Per forzare l'uso del fallback offline (debug): scollegare la rete o
impostare DictionaryApi__BaseUrl ad un indirizzo non valido.


================================================================================
					   SELEZIONE CANALE DI COMUNICAZIONE
================================================================================

Il canale (CAN / BLE / Seriale) si seleziona dal menu dell'applicazione.
Il canale di default dipende dalla variante:

- TopLift  -> CAN
- Generic / Eden / Egicon -> BLE

Per CAN: collegare il dongle PCAN USB e installare i driver Peak.
Per BLE: assicurarsi che il Bluetooth di Windows sia attivo.
Per Seriale: collegare il dispositivo via USB-Serial e selezionare la
porta COM dal menu.


================================================================================
							  FUNZIONALITA'
================================================================================

- Connessione multi-canale CAN / BLE / Seriale con switch a runtime
- Bootloader: aggiornamento firmware singolo + batch SPARK (multi-area)
- Telemetria lenta (lettura on-demand) e veloce (streaming continuo)
- Grafici telemetria in tempo reale (OxyPlot)
- Generatore di codice sp_config.h
- Dizionari aggiornati live dall'API Azure (con fallback Excel offline)
- Selezione runtime della variante dispositivo


================================================================================
						   RISOLUZIONE PROBLEMI
================================================================================

PROBLEMA: l'app non parte / errore "MSVCP140.dll mancante"
SOLUZIONE: installare il "Microsoft Visual C++ Redistributable x64".

PROBLEMA: PCAN non rilevato
SOLUZIONE: installare i driver Peak da www.peak-system.com, scollegare e
ricollegare il dongle.

PROBLEMA: BLE non trova il dispositivo
SOLUZIONE: verificare che il Bluetooth di Windows sia attivo, riavviare
l'adattatore Bluetooth, riavviare l'app.

PROBLEMA: variante / chiave API impostate ma non applicate
SOLUZIONE: chiudere COMPLETAMENTE l'app (anche dalla tray) e riavviarla.
Le variabili d'ambiente vengono lette solo all'avvio.

PROBLEMA: errore di download dizionari
SOLUZIONE: l'app ricade automaticamente sul dizionario embedded; verificare
in seguito la connettivita' di rete o la validita' della chiave API.


================================================================================
							   SUPPORTO
================================================================================

Per problemi o richieste: l.veronelli@stem.it


================================================================================
						 (c) 2026 STEM E.m.s.
					  Tutti i diritti riservati
================================================================================
