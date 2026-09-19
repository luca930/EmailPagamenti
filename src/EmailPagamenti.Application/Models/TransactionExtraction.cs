using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;

namespace EmailPagamenti.Application.Models;

/// <summary>Il movimento ricostruito da una notifica bancaria.</summary>
/// <param name="Confidence">Da 0 (non e' una notifica di movimento) a 1 (riconoscimento certo).</param>
public sealed record TransactionExtraction(
    double Confidence,
    PaymentDirection Direction,
    TransactionKind Kind,
    Money? Total,
    string? Merchant,
    string Category,
    string? CardLast4,
    long? BalanceAfter,
    DateTime? ValueDate,
    string? Reference,
    string? MatchedRule)
{
    public static TransactionExtraction NotATransaction(string? reason = null) =>
        new(
            0d,
            PaymentDirection.Unknown,
            TransactionKind.Unknown,
            null,
            null,
            SpendingCategory.Uncategorized,
            null,
            null,
            null,
            null,
            reason);
}
