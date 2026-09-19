namespace EmailPagamenti.Domain.ValueObjects;

/// <summary>
/// Categoria di spesa. E' una stringa e non un enum perche' le categorie le decide chi usa
/// l'applicazione: aggiungerne una non deve costare una migrazione del database.
/// </summary>
public static class SpendingCategory
{
    /// <summary>Movimento non ancora categorizzato.</summary>
    public const string Uncategorized = "Da categorizzare";

    /// <summary>Categorie proposte di default, usate anche per l'ordine nei grafici.</summary>
    public static readonly IReadOnlyList<string> Defaults =
    [
        "Alimentari",
        "Ristoranti",
        "Carburante",
        "Trasporti",
        "Casa e bollette",
        "Salute",
        "Shopping",
        "Abbonamenti",
        "Svago",
        "Prelievi",
        "Entrate",
        Uncategorized,
    ];
}
