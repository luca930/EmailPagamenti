namespace EmailPagamenti.Web.Security;

/// <summary>
/// Accesso all'interfaccia. Qui dentro ci sono i movimenti del conto: l'applicazione si
/// rifiuta di partire senza password, a meno che non le si dica esplicitamente il contrario.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Password di accesso. Va impostata da variabile d'ambiente (Security__Password) o da
    /// user-secrets, mai in appsettings.json.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Disattiva l'autenticazione. Da usare solo per provare l'applicazione in locale:
    /// su Proxmox, anche in LAN, lasciarla spenta.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>Durata della sessione prima di dover reinserire la password.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Richiede HTTPS per il cookie di sessione. Da tenere acceso dietro un reverse proxy
    /// con certificato, che e' il modo consigliato di esporre l'applicazione.
    /// </summary>
    public bool RequireHttps { get; set; }
}
