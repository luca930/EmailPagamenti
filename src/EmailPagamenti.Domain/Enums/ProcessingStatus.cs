namespace EmailPagamenti.Domain.Enums;

/// <summary>Esito dello smistamento di una email.</summary>
public enum ProcessingStatus
{
    /// <summary>Acquisita ma non ancora analizzata.</summary>
    Pending = 0,

    /// <summary>Riconosciuta come pagamento con confidenza sufficiente.</summary>
    Classified = 1,

    /// <summary>Probabile pagamento, ma i dati estratti vanno confermati a mano.</summary>
    NeedsReview = 2,

    /// <summary>Analizzata e scartata: non riguarda un pagamento.</summary>
    Ignored = 3,

    /// <summary>L'analisi e' fallita con un errore.</summary>
    Failed = 4,
}
