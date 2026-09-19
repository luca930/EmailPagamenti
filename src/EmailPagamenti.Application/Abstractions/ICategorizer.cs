using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>Assegna una categoria di spesa a un movimento.</summary>
public interface ICategorizer
{
    /// <param name="merchant">Esercente o controparte, quando riconosciuto.</param>
    /// <param name="text">Testo della notifica, usato quando l'esercente non basta.</param>
    /// <param name="kind">Tipo di movimento: prelievi e stipendi hanno una categoria propria.</param>
    string Categorize(string? merchant, string text, TransactionKind kind);
}
