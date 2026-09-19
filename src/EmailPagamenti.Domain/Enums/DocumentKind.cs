namespace EmailPagamenti.Domain.Enums;

/// <summary>Tipo di documento riconosciuto nell'email.</summary>
public enum DocumentKind
{
    Unknown = 0,
    Receipt = 1,
    Invoice = 2,
    Refund = 3,
    Reminder = 4,
    Subscription = 5,
    Failed = 6,
}
