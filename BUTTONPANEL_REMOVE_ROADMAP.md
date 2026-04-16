# Roadmap: Rimozione Funzionalità Collaudo Pulsantiere

**Progetto:** Stem.Device.Manager  
**Data:** 16 aprile 2026  
**Autore:** Luca Veronelli  
**Branch target:** refactor/remove-buttonpanel-tester

## 1\. Contesto e Motivazione

La funzionalità di collaudo pulsantiere (ButtonPanel Test) è presente in questo repository come duplicazione: esiste già un'applicazione separata e dedicata, **ButtonPanelTester**, che svolge la medesima funzione.

Il codice attuale comprende un'implementazione completa con architettura MVP (l'unico modulo con architettura pulita nell'applicazione), ma la sua presenza crea ridondanza, aumenta la manutenzione e mantiene viva la configurazione di build BUTTONPANEL che altera il comportamento dell'applicazione principale.

**Obiettivo:** Eliminare completamente il codice ButtonPanel da questo repository, inclusa la configurazione di build BUTTONPANEL, senza impattare le altre funzionalità.

## 2\. Inventario Completo

### 2.1 File da Eliminare Integralmente (19 file)

**Core Layer**

- Core/Models/ButtonPanel.cs - factory con 4 tipi di pulsantiera, proprietà e button masks
- Core/Models/ButtonPanelTestResult.cs - DTO risultato test (PanelType, TestType, Passed, Message, Interrupted)
- Core/Enums/ButtonPanelEnums.cs - 6 enum: ButtonPanelType, ButtonPanelTestType, IndicatorState, EdenButtons, R3LXPButtons, OptimusButtons
- Core/Interfaces/IButtonPanelTestService.cs - contratto servizio con 4 metodi async e callback

**App Layer**

- App/Core/Interfaces/IButtonPanelTestTab.cs - contratto vista: 9 eventi, 11 metodi
- App/Core/Models/ButtonIndicator.cs - indicatore visuale pulsante (Bounds, State)
- App/Services/ButtonPanelTestService.cs - implementazione servizio (~250 LOC), comunica via protocollo STEM con comandi hardcoded (0x0002, 0x8002, 0x8003, 0x8004)
- App/GUI/Presenters/ButtonPanelTestPresenter.cs - presentatore MVP (157 LOC), orchestrazione eventi view → chiamate service
- App/GUI/Views/ButtonPanelTestTabControl.cs - UserControl WinForms (579 LOC), view con PictureBox, indicatori colorati, 3 RichTextBox
- App/GUI/Views/ButtonPanelTestTabControl.Designer.cs - codice auto-generato dal designer
- App/GUI/Views/ButtonPanelTestTabControl.resx - risorse WinForms

**Immagini**

- App/images/ButtonPanels/DIS0023789.jpg
- App/images/ButtonPanels/DIS0025205.jpg
- App/images/ButtonPanels/DIS0026166.jpg
- App/images/ButtonPanels/DIS0026182.jpg

**Test Layer**

- Tests/Unit/Core/Models/ButtonPanelTests.cs - 10 test unit (factory GetByType)
- Tests/Unit/Core/Models/ButtonPanelTestResultTests.cs - 3 test unit (DTO properties)
- Tests/Unit/Core/Enums/ButtonPanelEnumsTests.cs - 8 test unit (cardinalità enum)
- Tests/Unit/Core/Models/ButtonIndicatorTests.cs - 2 test unit (stati indicatore)
- Tests/Integration/Presenter/ButtonPanelTestPresenterTests.cs - 8+ test integrazione (dispatch test type → service method)
- Tests/Integration/Presenter/Mocks/MockButtonPanelTestTab.cs - mock view
- Tests/Integration/Presenter/Mocks/MockButtonPanelTestService.cs - mock service

### 2.2 File da Modificare (5 file)

| File                                                              | Modifica                                                                                                                                                                                            |
| ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| App/Form1.cs                                                      | Rimuovere: chiamata AddButtonPanelTestTab() (riga ~271), metodo AddButtonPanelTestTab() (righe ~365-376), metodo SetRecipientIdSilently() (righe ~378-381), blocco #if BUTTONPANEL (righe ~349-357) |
| App/Program.cs                                                    | Rimuovere registrazione DI: services.AddTransient&lt;IButtonPanelTestService, ButtonPanelTestService&gt;()                                                                                          |
| App/App.csproj                                                    | Rimuovere BUTTONPANEL da &lt;Configurations&gt;, rimuovere &lt;PropertyGroup Condition="...BUTTONPANEL..."&gt;                                                                                      |
| Tests/Integration/DependencyInjection/ServiceRegistrationTests.cs | Rimuovere i test relativi a IButtonPanelTestService (Resolve, IsTransient, UnregisteredService)                                                                                                     |
| CLAUDE.md                                                         | Aggiornare lista modelli Core (rimuovere ButtonPanel), aggiornare descrizione GUI module                                                                                                            |

## 3\. Piano di Esecuzione

La rimozione viene eseguita in 6 fasi ordinate per minimizzare i rischi di compilazione intermedia. Ogni fase termina con un dotnet build di verifica.

### Fase 1 - Rimozione Test (basso rischio)

**Obiettivo:** eliminare tutti i test ButtonPanel. Il progetto rimane compilabile perché i test non sono dipendenze di produzione.

**Azioni:**

- Eliminare i 4 file di test unit: ButtonPanelTests.cs, ButtonPanelTestResultTests.cs, ButtonPanelEnumsTests.cs, ButtonIndicatorTests.cs
- Eliminare i file di test integrazione: ButtonPanelTestPresenterTests.cs, MockButtonPanelTestTab.cs, MockButtonPanelTestService.cs
- Modificare ServiceRegistrationTests.cs: rimuovere i 3 metodi che testano IButtonPanelTestService

**Verifica:**

dotnet test Tests/Tests.csproj --framework net10.0

Nessun test ButtonPanel deve comparire nell'output. Tutti gli altri test devono passare.

### Fase 2 - Disconnessione da Form1 e Program.cs

**Obiettivo:** scollegare il codice ButtonPanel dall'entry point e dal God Object prima di eliminarne i file, così il build continua a funzionare passo dopo passo.

**Azioni in Form1.cs:**

- Rimuovere la chiamata AddButtonPanelTestTab() nel costruttore o nel Load handler
- Rimuovere il metodo privato AddButtonPanelTestTab()
- Rimuovere il metodo pubblico SetRecipientIdSilently(uint recipientId)
- Rimuovere il blocco #if BUTTONPANEL (che eliminava tutti i tab non-ButtonPanel)

**Azioni in Program.cs:**

- Rimuovere la riga services.AddTransient&lt;IButtonPanelTestService, ButtonPanelTestService&gt;()
- Rimuovere eventuali using relativi a ButtonPanelTestService

**Verifica:**

dotnet build Stem.Device.Manager.slnx

Il progetto deve compilare senza errori in configurazione Debug standard.

### Fase 3 - Rimozione GUI e Services

**Obiettivo:** eliminare i file applicativi che implementano la funzionalità.

**Azioni:**

- Eliminare App/Services/ButtonPanelTestService.cs
- Eliminare App/GUI/Presenters/ButtonPanelTestPresenter.cs
- Eliminare App/GUI/Views/ButtonPanelTestTabControl.cs
- Eliminare App/GUI/Views/ButtonPanelTestTabControl.Designer.cs
- Eliminare App/GUI/Views/ButtonPanelTestTabControl.resx
- Eliminare App/Core/Interfaces/IButtonPanelTestTab.cs
- Eliminare App/Core/Models/ButtonIndicator.cs

**Nota:** Se dopo questa fase la cartella App/GUI/Presenters/ o App/GUI/Views/ rimane vuota, valutare se mantenere le cartelle (in previsione della Fase 3 del piano di modernizzazione) o rimuoverle.

**Verifica:**

dotnet build Stem.Device.Manager.slnx

### Fase 4 - Rimozione Core Layer

**Obiettivo:** eliminare i modelli, gli enum e le interfacce dal layer Core.

**Azioni:**

- Eliminare Core/Models/ButtonPanel.cs
- Eliminare Core/Models/ButtonPanelTestResult.cs
- Eliminare Core/Enums/ButtonPanelEnums.cs
- Eliminare Core/Interfaces/IButtonPanelTestService.cs

**Verifica:**

dotnet build Stem.Device.Manager.slnx  
dotnet test Tests/Tests.csproj --framework net10.0

Tutti i test rimanenti (non-ButtonPanel) devono passare.

### Fase 5 - Pulizia Configurazione Build

**Obiettivo:** rimuovere la configurazione BUTTONPANEL dal file di progetto.

**Azioni in App/App.csproj:**

- Rimuovere BUTTONPANEL dalla lista &lt;Configurations&gt; (riga ~13)
- Rimuovere il &lt;PropertyGroup Condition="'\$(Configuration)' == 'BUTTONPANEL'"&gt; con i relativi &lt;DefineConstants&gt;

**Nota:** Verificare che Visual Studio / Rider non abbia ancora referenze alla configurazione BUTTONPANEL nelle impostazioni locali (.vs/ o .idea/). Questi file sono in .gitignore e non richiedono azione sul repository.

**Verifica:**

dotnet build Stem.Device.Manager.slnx --configuration Release -p:EnableWindowsTargeting=true

La configurazione BUTTONPANEL non deve più esistere come target valido.

### Fase 6 - Pulizia Immagini e Documentazione

**Obiettivo:** rimuovere le risorse residue e aggiornare la documentazione.

**Azioni:**

- Eliminare la directory App/images/ButtonPanels/ e le 4 immagini JPG
- Aggiornare CLAUDE.md: rimuovere ButtonPanel dalla lista modelli Core, aggiornare la sezione GUI (il modulo MVP non è più "il modello da seguire" ma è stato rimosso - indicare che il pattern MVP sarà da ricreare in Fase 3 del piano di modernizzazione)
- Aggiornare App/README.md se presente: rimuovere sezioni ButtonPanel (~righe 37-47, ~89-90)
- Aggiornare la tabella del piano di modernizzazione in CLAUDE.md se necessario

**Verifica finale:**

dotnet build Stem.Device.Manager.slnx  
dotnet test Tests/Tests.csproj --framework net10.0  
grep -r "ButtonPanel" --include="\*.cs" --include="\*.csproj" .

L'ultimo comando non deve restituire risultati al di fuori di file di documentazione o history.

## 4\. Riepilogo Quantitativo

| Categoria                                | Numero          |
| ---------------------------------------- | --------------- |
| File eliminati (totale)                  | 23              |
| - di cui file C# sorgente                | 11              |
| - di cui file test                       | 7               |
| - di cui file WinForms (Designer + resx) | 2               |
| - di cui immagini                        | 4               |
| File modificati                          | 5               |
| Linee di codice rimosse (stima)          | ~1.200          |
| Test eliminati                           | ~34             |
| Configurazioni di build rimosse          | 1 (BUTTONPANEL) |

## 5\. Dipendenze e Rischi

**Dipendenze esterne al repository:** nessuna. Il codice ButtonPanel non è referenziato da altri repository o pacchetti NuGet.

**Rischio: Form1.cs** - È un God Object (~55k LOC). La rimozione dei metodi in Fase 2 richiede attenzione per non intaccare codice adiacente. Procedere con Edit mirato sulle righe specifiche; eseguire build dopo ogni modifica.

**Rischio: ServiceRegistrationTests.cs** - Questo file contiene anche test per IDictionaryProvider. Rimuovere solo i metodi ButtonPanel, non l'intero file.

**Rischio: cartelle vuote** - Dopo la Fase 3, App/GUI/Presenters/ e App/GUI/Views/ potrebbero rimanere vuote. Mantenere le cartelle se si prevede di usarle nella Fase 3 del piano di modernizzazione (refactoring Form1), altrimenti eliminarle.

**Branch strategy consigliata:** eseguire tutta la rimozione su un branch dedicato (refactor/remove-buttonpanel-tester), con un commit per fase. Questo consente una review chiara e un rollback granulare se necessario.

## 6\. Criterio di Completamento

La rimozione è completa quando:

- dotnet build passa senza warning relativi a ButtonPanel
- dotnet test Tests/Tests.csproj --framework net10.0 passa al 100%
- grep -r "ButtonPanel" --include="\*.cs" --include="\*.csproj" . restituisce zero risultati
- La configurazione BUTTONPANEL non compare più in App.csproj
- Nessuna immagine in App/images/ButtonPanels/ è presente sul repository