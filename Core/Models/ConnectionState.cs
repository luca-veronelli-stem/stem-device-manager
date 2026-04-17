namespace Core.Models;

/// <summary>
/// Stato di una <c>ICommunicationPort</c>.
/// Vedi <c>Specs/Phase1/ConnectionState.lean</c> per la formalizzazione.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
