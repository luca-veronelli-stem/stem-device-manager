# SPARK

## Procedura aggiornamento firmware del sistema

| Data | Descrizione revisione | Rev. | Name |
|---|---|---|---|
| 13/01/2026 | Prima emissione | Rev0 | Danilo Schiavi (EGICON S.r.l.) |

Si elenca la sequenza e i vari passi per l'aggiornamento firmware dell'intero sistema: delle centraline (ECU) e del display (HMI); da questo elenco è escluso il modulo Gateway.

1. applicativo Bluetooth (via OTA)
2. boot-loader HMI (via Bluetooth/USB)
3. applicativo HMI (via Bluetooth/USB)
4. immagini HMI (via Bluetooth/USB)
5. applicativo ECU Motori Destra, Sinistra e Rostro (via Bluetooth/USB)
6. calibrazione e settaggio parametri
7. Parametri

---

### FASE 1: Applicativo Bluetooth

Utilizzare un cellulare e installare l'applicazione "Simplicity Connect BLE mobile app", si può trovare all'indirizzo web: <https://www.silabs.com/software-and-tools/simplicity-connect-mobile-app?tab=downloads>. Una volta avviata l'applicazione e accesa la barella, collegarsi tramite app all'HMI cercando il nome del dispositivo bluetooth "SPARK" (1) e selezionando "CONNECT" nella sezione relativa (2). Quando la connessione è completata apparirà la finestra relativa a SPARK, ora selezionare "OTA Firmware" (3).

![Schermata app - connessione SPARK](media/image1.png)
![Schermata app - selezione OTA](media/image2.png)

Ora selezionare il file sorgente "&lt;nome_file&gt;.gbl" per l'aggiornamento dell'applicativo del modulo bluetooth (4). Verrà aperta una finestra per la ricerca del file, dipende dalle impostazioni del telefono e da dove si è deciso di salvare il sorgente. Trovato e selezionato il file, è possibile avviare l'aggiornamento OTA selezionando "Upload" (5).

Al termine dell'operazione selezionare "End" (6). Il modulo bluetooth verrà resettato per poi farlo ripartire. Ora comparirà un nuovo nome del dispositivo "SPARK_STEM" visibile in modo completo nella pagina "BLE Device".

![Schermata app - selezione file .gbl](media/image3.png)
![Schermata app - upload OTA](media/image4.png)
![Schermata app - fine aggiornamento](media/image5.png)

---

### FASE 2: boot-loader HMI

Per eseguire questa fase, se è installata l'applicazione del modulo bluetooth che implementa il protocollo STEM (FASE 1), sarà possibile utilizzare l'app STEM; oppure, se disponibile, anche la porta USB, quest'ultima è più veloce.

#### Utilizzo USB

Caricare nella directory principale della penna USB il file `WORKCODE.hex` specifico per l'aggiornamento del boot-loader HMI. A barella spenta, inserire la penna nella relativa porta USB della barella. Avviare la barella e seguire le indicazioni che compaiono sul display dell'HMI. Terminato il processo con esito positivo riavviare la barella:

a) spegnere la barella rimuovendo la batteria,
b) rimuovere la penna USB,
c) rimettere la batteria e
d) riavviare la barella tramite pulsanti carico o scarico.

> Ora avviene l'aggiornamento del boot-loader. Attendere il completamento delle operazioni quindi terminato l'aggiornamento con esito positivo riavviare la barella. L'HMI rimarrà nello stato di boot attendendo un nuovo applicativo.

#### Utilizzo App STEM

Si rimanda al manuale dell'applicazione STEM che descrive l'aggiornamento dell'applicativo.

---

### FASE 3: Applicativo HMI

Per eseguire questa fase, se è installata l'applicazione del modulo bluetooth che implementa il protocollo STEM (FASE 1), sarà possibile utilizzare l'app STEM; oppure, se disponibile, anche la porta USB, quest'ultima è più veloce.

#### Utilizzo USB

Caricare nella directory principale della penna USB il file `WORKCODE.hex` specifico per l'aggiornamento dell'applicativo HMI. A barella spenta, inserire la penna nella relativa porta USB della barella. Avviare la barella e seguire le indicazioni che compaiono sul display dell'HMI. Terminato il processo con esito positivo riavviare la barella:

a) spegnere la barella rimuovendo la batteria
b) rimuovere la penna USB
c) rimettere la batteria
d) riavviare la barella tramite pulsanti carico o scarico

Ora l'HMI dopo aver verificato l'integrità del codice applicativo lo eseguirà.

> **Nota:** ora le immagini mostrate a display potrebbero non essere corrette se il nuovo applicativo utilizza una versione differente di immagini, occorre aggiornare anche il database delle immagini.

#### Utilizzo App STEM

Si rimanda al manuale dell'applicazione STEM che descrive l'aggiornamento dell'applicativo.

---

### FASE 4: Immagini HMI

Per eseguire questa fase, se è installata l'applicazione del modulo bluetooth che implementa il protocollo STEM (FASE 1), sarà possibile utilizzare l'app STEM; oppure, se disponibile, anche la porta USB, quest'ultima è più veloce.

#### Utilizzo USB

Caricare nella directory principale della penna USB il file `IMAGES.hex` per l'aggiornamento delle immagini HMI. A barella spenta, inserire la penna nella relativa porta USB della barella. Avviare la barella e seguire le indicazioni che compaiono sul display dell'HMI. Terminato il processo con esito positivo riavviare la barella:

a) spegnere la barella rimuovendo la batteria
b) rimuovere la penna USB
c) rimettere la batteria
d) riavviare la barella tramite pulsanti carico o scarico

Ora l'HMI ha aggiornato il proprio database delle immagini necessarie.

---

### FASE 5: applicativi ECU motori e rostro

Per eseguire questa fase, se è installata l'applicazione del modulo bluetooth che implementa il protocollo STEM (FASE 1), sarà possibile utilizzare l'app STEM; oppure, se disponibile, anche la porta USB, quest'ultima è più veloce.

#### Utilizzo USB

Caricare nella directory principale della penna USB i files:

a) `WEMOTOR1.hex` per l'aggiornamento dell'applicativo ECU Motori destra.
b) `WEMOTOR2.hex` per l'aggiornamento dell'applicativo ECU Motori sinistra.
c) `WEROSTRO.hex` per l'aggiornamento dell'applicativo ECU Rostro.

A barella spenta, inserire la penna nella relativa porta USB della barella. Avviare la barella e seguire le indicazioni che compaiono sul display dell'HMI. Terminato il processo con esito positivo riavviare la barella:

a) spegnere la barella rimuovendo la batteria
b) rimuovere la penna USB
c) rimettere la batteria
d) riavviare la barella tramite pulsanti carico o scarico

Ora le ECU dopo aver verificato l'integrità del codice applicativo lo eseguiranno.

#### Utilizzo App STEM

Si rimanda al manuale dell'applicazione STEM che descrive l'aggiornamento dell'applicativo.

---

### FASE 6: calibrazioni

Questa fase permette alle ECU e all'HMI di memorizzare in modo automatico determinati valori che permettono di convertire correttamente:

angolo gambe lato testa e lato piedi, posizione ruote piedi e pressione olio idraulico lato testa e piedi.

Se non è già stato fatto prima dell'avvio degli aggiornamenti (fasi precedenti), occorre abbassare la barella fino a raggiungere il suolo in modo che l'angolo gambe sia il minimo, ovvero le gambe lato testa e piedi raggiungono il loro finecorsa. <u>Questa operazione è da farsi utilizzando i comandi manuali a barella spenta.</u> Ora, entrati nel menu di calibrazione (password di fabbrica) occorre ritrarre portando a finecorsa gli attuatori che controllano la posizione delle ruote. Avviare la calibrazione tenendo premuto il tasto calibrazione fino al segnale sonoro prolungato che conferma la fine dell'operazione.

---

### FASE 7: parametri

Occorre impostare il modello del sensore di angolo gamba:

Entrati nel menu dei parametri (password di fabbrica), selezionare il parametro `LEG POSITION SENS MODEL` e indicare quello corretto, <u>anche se non è variato confermare con il tasto "salva"</u>; in questo modo le ECU Motori apprenderanno quello corretto.
