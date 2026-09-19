using EmailPagamenti.Application.Models;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>
/// Sorgente delle email. Oggi la implementa IMAP; domani Gmail API o Microsoft Graph
/// senza toccare la pipeline.
/// </summary>
public interface IEmailSource
{
    /// <summary>Nome leggibile della sorgente, usato nei log.</summary>
    string Name { get; }

    /// <summary>
    /// Restituisce le email ricevute a partire da <paramref name="since"/>, in streaming:
    /// non carica l'intera casella in memoria.
    /// </summary>
    IAsyncEnumerable<RawEmail> FetchAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
