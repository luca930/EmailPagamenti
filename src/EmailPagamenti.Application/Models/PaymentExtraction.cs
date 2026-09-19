using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;

namespace EmailPagamenti.Application.Models;

/// <summary>Cosa la pipeline crede di aver capito di una email.</summary>
/// <param name="Confidence">Da 0 (sicuramente non un pagamento) a 1 (riconoscimento certo).</param>
public sealed record PaymentExtraction(
    double Confidence,
    PaymentDirection Direction,
    DocumentKind Kind,
    Money? Total,
    string? Merchant,
    string? Reference,
    string? MatchedRule)
{
    public static PaymentExtraction NotAPayment(string? reason = null) =>
        new(0d, PaymentDirection.Unknown, DocumentKind.Unknown, null, null, null, reason);
}
