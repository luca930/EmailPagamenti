using EmailPagamenti.Application.Models;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>Ricostruisce un movimento a partire dal testo di una notifica bancaria.</summary>
public interface ITransactionExtractor
{
    TransactionExtraction Extract(RawEmail email);
}
