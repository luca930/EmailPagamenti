namespace EmailPagamenti.Domain.Entities;

/// <summary>
/// Metadati di un allegato. Il file non viene scaricato ne' salvato dalla pipeline
/// di base: si tiene traccia di cosa c'era, non del contenuto.
/// </summary>
public sealed class PaymentAttachment
{
    private PaymentAttachment()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid PaymentEmailId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public static PaymentAttachment Create(string fileName, string contentType) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            ContentType = contentType,
        };
}
