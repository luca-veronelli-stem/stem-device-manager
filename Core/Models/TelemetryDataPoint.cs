using System.Collections.Immutable;

namespace Core.Models;

/// <summary>
/// Campione di telemetria raccolto da <c>ITelemetryService</c>.
/// Associa una <see cref="Variable"/> al suo valore raw e al timestamp di campionamento.
///
/// Il valore deve essere interpretato in base a <see cref="Variable.DataType"/>.
///
/// Vedi <c>Specs/Phase1/TelemetryDataPoint.lean</c> per la formalizzazione.
/// </summary>
/// <param name="Variable">Variabile dizionario a cui il campione si riferisce.</param>
/// <param name="RawValue">Valore grezzo. Equality strutturale garantita da <see cref="Equals(TelemetryDataPoint)"/>.</param>
/// <param name="Timestamp">Istante di campionamento.</param>
public sealed record TelemetryDataPoint(
    Variable Variable,
    ImmutableArray<byte> RawValue,
    DateTime Timestamp)
{
    /// <summary>Numero di byte del valore raw.</summary>
    public int RawValueLength => RawValue.IsDefault ? 0 : RawValue.Length;

    /// <inheritdoc/>
    public bool Equals(TelemetryDataPoint? other)
    {
        if (other is null) return false;
        return Variable == other.Variable
            && Timestamp == other.Timestamp
            && ImmutableArrayEquality.SequenceEqual(RawValue, other.RawValue);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Variable, Timestamp, ImmutableArrayEquality.SequenceHash(RawValue));
}
