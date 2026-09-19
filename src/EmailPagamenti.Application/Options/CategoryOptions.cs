namespace EmailPagamenti.Application.Options;

/// <summary>
/// Regole di categorizzazione. Le parole chiave stanno in configurazione: aggiungere il
/// supermercato sotto casa deve costare una riga di JSON, non una ricompilazione.
/// </summary>
public sealed class CategoryOptions
{
    public const string SectionName = "Categories";

    /// <summary>
    /// Se vero, alle regole configurate si aggiungono quelle incorporate per le catene
    /// piu' diffuse in Italia. Le regole configurate vengono comunque valutate per prime.
    /// </summary>
    public bool UseBuiltInRules { get; set; } = true;

    /// <summary>Categoria per esercente: la chiave e' il nome della categoria.</summary>
    public Dictionary<string, List<string>> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
