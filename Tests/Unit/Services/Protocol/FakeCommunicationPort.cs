using System.Collections.Immutable;
using Core.Interfaces;
using Core.Models;

namespace Tests.Unit.Services.Protocol;

/// <summary>
/// Mock manuale di <see cref="ICommunicationPort"/> per i test di
/// <c>ProtocolService</c>. Registra le SendAsync e consente di simulare
/// pacchetti ricevuti dal canale.
/// </summary>
internal sealed class FakeCommunicationPort : ICommunicationPort
{
    public FakeCommunicationPort(ChannelKind kind)
    {
        Kind = kind;
    }

    public ChannelKind Kind { get; }
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public bool IsConnected => State == ConnectionState.Connected;

    public event EventHandler<RawPacket>? PacketReceived;
    public event EventHandler<ConnectionState>? StateChanged;

    public List<byte[]> SentPayloads { get; } = [];

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        SentPayloads.Add(payload.ToArray());
        return Task.CompletedTask;
    }

    /// <summary>Simula un pacchetto in ingresso dal canale sottostante.</summary>
    public void RaisePacketReceived(byte[] payload, DateTime timestamp)
    {
        PacketReceived?.Invoke(this, new RawPacket(payload.ToImmutableArray(), timestamp));
    }

    public void Dispose()
    {
        // Il mock non possiede risorse reali; Dispose è no-op.
    }
}
