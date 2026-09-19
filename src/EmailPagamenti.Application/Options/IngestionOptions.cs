using System.ComponentModel.DataAnnotations;

namespace EmailPagamenti.Application.Options;

/// <summary>Come si comporta la pipeline di acquisizione.</summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Quanto indietro guardare alla primissima esecuzione, quando il database e' vuoto.</summary>
    public TimeSpan InitialLookback { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Sovrapposizione applicata all'ultima data acquisita: le email possono arrivare
    /// fuori ordine, ripescare qualche ora evita buchi. La deduplica gestisce i doppioni.
    /// </summary>
    public TimeSpan Overlap { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Intervallo tra due passate del worker.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Quante email salvare per volta.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>Confidenza minima per considerare la classificazione affidabile.</summary>
    [Range(0d, 1d)]
    public double MinConfidence { get; set; } = 0.6d;

    /// <summary>Sotto questa soglia l'email non viene nemmeno salvata.</summary>
    [Range(0d, 1d)]
    public double ReviewThreshold { get; set; } = 0.3d;

    /// <summary>Se vero salva anche le email scartate, utile per tarare le regole.</summary>
    public bool StoreIgnored { get; set; }

    /// <summary>Caratteri di corpo conservati. A 0 non si salva nulla del corpo.</summary>
    [Range(0, 8000)]
    public int BodySnippetLength { get; set; } = 500;
}
