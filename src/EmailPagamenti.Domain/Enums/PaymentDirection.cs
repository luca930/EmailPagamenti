namespace EmailPagamenti.Domain.Enums;

/// <summary>Verso del movimento rispetto al titolare della casella.</summary>
public enum PaymentDirection
{
    Unknown = 0,

    /// <summary>Soldi usciti: acquisto, addebito, canone.</summary>
    Outgoing = 1,

    /// <summary>Soldi entrati: rimborso, accredito, incasso.</summary>
    Incoming = 2,
}
