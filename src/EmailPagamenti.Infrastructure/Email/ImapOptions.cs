using System.ComponentModel.DataAnnotations;

namespace EmailPagamenti.Infrastructure.Email;

/// <summary>
/// Connessione alla casella. La password non va mai messa in appsettings.json:
/// si passa da variabile d'ambiente o da user-secrets (vedi README).
/// </summary>
public sealed class ImapOptions
{
    public const string SectionName = "Imap";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 993;

    [Required]
    [EmailAddress]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Password applicazione o token OAuth. Solo da segreto esterno.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Cartelle da leggere. Vuoto significa solo la posta in arrivo.</summary>
    public List<string> Folders { get; set; } = [];

    /// <summary>
    /// TLS implicito sulla porta 993. Lasciare acceso: la modalita' in chiaro
    /// esiste solo per i server di test in locale.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Timeout della singola operazione IMAP.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Tetto di email lette in una passata, per non bloccarsi su una casella enorme.</summary>
    [Range(1, 100_000)]
    public int MaxMessagesPerRun { get; set; } = 500;
}
