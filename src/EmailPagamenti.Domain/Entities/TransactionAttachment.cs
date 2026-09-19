namespace EmailPagamenti.Domain.Entities;

/// <summary>
/// Metadati di un allegato, ad esempio la contabile in PDF di un bonifico. Il file non viene
/// scaricato ne' salvato: si tiene traccia di cosa c'era, non del contenuto.
/// </summary>
public sealed class TransactionAttachment
{
    private TransactionAttachment()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public static TransactionAttachment Create(string fileName, string contentType) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            ContentType = contentType,
        };
}
