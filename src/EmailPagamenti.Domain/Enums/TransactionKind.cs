namespace EmailPagamenti.Domain.Enums;

/// <summary>Tipo di movimento comunicato dalla banca.</summary>
public enum TransactionKind
{
    Unknown = 0,

    /// <summary>Pagamento con carta, POS fisico o online.</summary>
    CardPayment = 1,

    /// <summary>Prelievo di contante allo sportello automatico.</summary>
    CashWithdrawal = 2,

    /// <summary>Bonifico, in entrata o in uscita.</summary>
    Transfer = 3,

    /// <summary>Addebito diretto ricorrente: utenze, abbonamenti, rate.</summary>
    DirectDebit = 4,

    /// <summary>Accredito ricorrente: stipendio, pensione, rimborsi.</summary>
    Income = 5,

    /// <summary>Commissione, canone o imposta di bollo applicata dalla banca.</summary>
    Fee = 6,

    /// <summary>Ricarica di una carta prepagata o di un conto collegato.</summary>
    TopUp = 7,

    /// <summary>Movimento rifiutato o non andato a buon fine.</summary>
    Declined = 8,
}
