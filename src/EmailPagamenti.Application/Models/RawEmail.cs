namespace EmailPagamenti.Application.Models;

/// <summary>Email cosi' come arriva dal server, prima di qualunque interpretazione.</summary>
public sealed record RawEmail(
    string MessageId,
    string Folder,
    string Sender,
    string? SenderDisplayName,
    string Subject,
    DateTimeOffset SentAt,
    string? TextBody,
    IReadOnlyList<RawAttachment> Attachments)
{
    /// <summary>Testo su cui girano le regole: oggetto piu' corpo, gia' concatenati.</summary>
    public string SearchableText => string.IsNullOrEmpty(TextBody) ? Subject : $"{Subject}\n{TextBody}";
}

/// <summary>Metadati di un allegato, senza il contenuto del file.</summary>
public sealed record RawAttachment(string FileName, string ContentType);
