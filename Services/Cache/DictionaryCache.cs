using Core.Interfaces;
using Core.Models;

namespace Services.Cache;

/// <summary>
/// Cache centralizzata del dizionario STEM. Sostituisce le tre liste sparse
/// in <c>App.Form1</c> (<c>IndirizziProtocollo</c>, <c>Comandi</c>,
/// <c>Dizionario</c>) con un servizio singleton che espone liste read-only e
/// notifica l'aggiornamento via <see cref="DictionaryUpdated"/>.
///
/// <para><b>Responsabilità:</b></para>
/// <list type="bullet">
/// <item><description><see cref="LoadAsync"/> — carica comandi + indirizzi protocollo all'avvio (chiamata all'evento <c>Load</c> di Form1).</description></item>
/// <item><description><see cref="SelectByRecipientAsync"/> — aggiorna <see cref="CurrentRecipientId"/> + variabili della scheda scelta.</description></item>
/// <item><description><see cref="SelectByDeviceBoardAsync"/> — risolve l'indirizzo cercando <see cref="ProtocolAddress.DeviceName"/>/<see cref="ProtocolAddress.BoardName"/>, poi delega a <see cref="SelectByRecipientAsync"/>.</description></item>
/// </list>
///
/// <para>Al termine di ogni operazione riuscita aggiorna lo snapshot di
/// <see cref="IPacketDecoder"/> (via <c>UpdateDictionary</c>) per mantenerlo
/// allineato con la cache corrente, poi emette <see cref="DictionaryUpdated"/>
/// per i consumer (tab, servizi).</para>
///
/// <para><b>Parità legacy:</b> niente cache LRU delle variabili per
/// <c>RecipientId</c> — ogni <c>SelectBy...</c> chiama direttamente
/// <see cref="IDictionaryProvider.LoadVariablesAsync"/>. La chiamata HTTP è
/// rara (solo al cambio scheda utente).</para>
///
/// <para><b>Thread-safety:</b> <see cref="Lock"/> su ogni mutazione dello stato;
/// getter serializzati; event <see cref="DictionaryUpdated"/> fired fuori dal
/// lock per evitare reentry.</para>
/// </summary>
public sealed class DictionaryCache
{
    private readonly IDictionaryProvider _provider;
    private readonly IPacketDecoder _decoder;
    private readonly Lock _stateLock = new();
    private IReadOnlyList<Command> _commands = [];
    private IReadOnlyList<ProtocolAddress> _addresses = [];
    private IReadOnlyList<Variable> _variables = [];
    private uint _currentRecipientId;

    public DictionaryCache(IDictionaryProvider provider, IPacketDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(decoder);
        _provider = provider;
        _decoder = decoder;
    }

    /// <summary>
    /// Notifica aggiornamento di una qualsiasi delle 4 parti di stato
    /// (commands, addresses, variables, recipientId).
    /// </summary>
    public event EventHandler? DictionaryUpdated;

    /// <summary>Comandi noti caricati dal provider.</summary>
    public IReadOnlyList<Command> Commands
    {
        get { lock (_stateLock) return _commands; }
    }

    /// <summary>Indirizzi protocollo (device + board + address).</summary>
    public IReadOnlyList<ProtocolAddress> Addresses
    {
        get { lock (_stateLock) return _addresses; }
    }

    /// <summary>
    /// Variabili della scheda attualmente selezionata. Vuota finché il
    /// consumer non chiama un <c>SelectBy...</c>.
    /// </summary>
    public IReadOnlyList<Variable> Variables
    {
        get { lock (_stateLock) return _variables; }
    }

    /// <summary>
    /// RecipientId della scheda attualmente selezionata. 0 se nessuna
    /// selezione o dopo <see cref="LoadAsync"/>.
    /// </summary>
    public uint CurrentRecipientId
    {
        get { lock (_stateLock) return _currentRecipientId; }
    }

    /// <summary>
    /// Carica comandi e indirizzi protocollo dal provider. Le variabili
    /// restano vuote finché non viene chiamato un <c>SelectBy...</c>.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var data = await _provider.LoadProtocolDataAsync(ct).ConfigureAwait(false);
        lock (_stateLock)
        {
            _commands = [.. data.Commands];
            _addresses = [.. data.Addresses];
            _variables = [];
            _currentRecipientId = 0;
        }
        _decoder.UpdateDictionary(_commands, _variables, _addresses);
        RaiseUpdated();
    }

    /// <summary>
    /// Carica le variabili per il <paramref name="recipientId"/> indicato e
    /// aggiorna lo stato corrente.
    /// </summary>
    public async Task SelectByRecipientAsync(uint recipientId, CancellationToken ct = default)
    {
        var variables = await _provider.LoadVariablesAsync(recipientId, ct).ConfigureAwait(false);
        lock (_stateLock)
        {
            _variables = [.. variables];
            _currentRecipientId = recipientId;
        }
        _decoder.UpdateDictionary(_commands, _variables, _addresses);
        RaiseUpdated();
    }

    /// <summary>
    /// Aggiorna SOLO <see cref="CurrentRecipientId"/>, senza chiamate al
    /// provider e senza emettere <see cref="DictionaryUpdated"/>.
    ///
    /// <para>Usato in scenari in cui il consumer cambia il "destinatario
    /// corrente" del protocollo per la prossima operazione (es. multi-board
    /// boot loop in <c>Boot_Smart_WF_Tab</c>) senza voler ricaricare il
    /// dizionario delle variabili — comportamento legacy era
    /// <c>Form1.FormRef.RecipientId = X</c>, semplice assegnazione di campo.</para>
    /// </summary>
    public void SetCurrentRecipientId(uint recipientId)
    {
        lock (_stateLock) _currentRecipientId = recipientId;
    }

    /// <summary>
    /// Risolve il <see cref="ProtocolAddress"/> cercando per
    /// <paramref name="deviceName"/> + <paramref name="boardName"/>, poi
    /// carica le variabili della scheda risolta.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se non esiste alcun indirizzo con device/board richiesti.
    /// </exception>
    public async Task SelectByDeviceBoardAsync(
        string deviceName, string boardName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentNullException.ThrowIfNull(boardName);
        ProtocolAddress? match;
        lock (_stateLock)
        {
            match = FindAddress(deviceName, boardName, _addresses);
        }
        if (match is null)
            throw new InvalidOperationException(
                $"Nessun ProtocolAddress per device='{deviceName}', board='{boardName}'.");
        var recipientId = ParseRecipientId(match.Address);
        await SelectByRecipientAsync(recipientId, ct).ConfigureAwait(false);
    }

    private static ProtocolAddress? FindAddress(
        string deviceName, string boardName, IReadOnlyList<ProtocolAddress> addresses)
    {
        foreach (var a in addresses)
        {
            if (a.DeviceName == deviceName && a.BoardName == boardName) return a;
        }
        return null;
    }

    /// <summary>
    /// Parsa un indirizzo STEM in formato stringa (es. "0x00080381") a uint.
    /// Supporta prefisso "0x"/"0X" opzionale.
    /// </summary>
    private static uint ParseRecipientId(string address)
    {
        var raw = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? address[2..]
            : address;
        return Convert.ToUInt32(raw, 16);
    }

    private void RaiseUpdated() => DictionaryUpdated?.Invoke(this, EventArgs.Empty);
}
