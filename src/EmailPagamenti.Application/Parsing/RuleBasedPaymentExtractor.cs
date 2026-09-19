using System.Text.RegularExpressions;
using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Domain.Enums;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Classificatore a regole e punteggio. Deterministico e ispezionabile: ogni riga salvata
/// porta con se' la regola che l'ha prodotta, cosi' un falso positivo si spiega e si corregge.
/// </summary>
public sealed partial class RuleBasedPaymentExtractor : IPaymentExtractor
{
    private readonly IOptionsMonitor<ClassificationOptions> _options;

    public RuleBasedPaymentExtractor(IOptionsMonitor<ClassificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    [GeneratedRegex(
        // Il lookahead impone almeno una cifra: senza, la regex si aggancerebbe alla parola
        // che segue ("Ordine Confermato") invece che al numero d'ordine.
        @"(?:ordine|order|fattura|invoice|transazione|transaction|rif(?:erimento)?|ref)\W{0,3}(?:n(?:umero|\.|°)?\W{0,3})?(?<ref>(?=[A-Z0-9\-/_]*\d)[A-Z0-9][A-Z0-9\-/_]{4,31})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex ReferenceRegex();

    public PaymentExtraction Extract(RawEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var options = _options.CurrentValue;
        var subject = email.Subject;
        var body = email.SearchableText;

        if (ContainsAny(body, options.ExcludeKeywords, out var excluded))
        {
            return PaymentExtraction.NotAPayment($"exclude:{excluded}");
        }

        var merchantRule = MatchMerchant(email, options.Merchants);
        var trustedSender = options.TrustedSenders.Any(
            s => email.Sender.Contains(s, StringComparison.OrdinalIgnoreCase));
        var hasKeyword = ContainsAny(body, options.PaymentKeywords, out var keyword);
        var hasAmount = AmountParser.TryFindTotal(body, out var total);

        // Punteggio: mittente noto e importo pesano piu' delle parole chiave, che da sole
        // fanno scattare troppi falsi positivi sulle newsletter commerciali.
        var confidence = 0d;
        if (merchantRule is not null)
        {
            confidence += 0.45d;
        }

        if (trustedSender)
        {
            confidence += 0.30d;
        }

        if (hasKeyword)
        {
            confidence += 0.25d;
        }

        if (hasAmount)
        {
            confidence += 0.30d;
        }

        if (email.Attachments.Any(a => a.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)))
        {
            confidence += 0.10d;
        }

        if (confidence <= 0d)
        {
            return PaymentExtraction.NotAPayment("no-signal");
        }

        var direction = merchantRule?.Direction ?? PaymentDirection.Outgoing;
        if (ContainsAny(body, options.IncomingKeywords, out var incomingKeyword))
        {
            direction = PaymentDirection.Incoming;
            keyword ??= incomingKeyword;
        }

        var kind = merchantRule?.Kind ?? InferKind(subject, direction);

        return new PaymentExtraction(
            Math.Clamp(confidence, 0d, 1d),
            direction,
            kind,
            total,
            merchantRule?.Name ?? InferMerchant(email),
            ExtractReference(body),
            merchantRule is not null ? $"merchant:{merchantRule.Name}" : $"keyword:{keyword ?? "none"}");
    }

    private static MerchantRule? MatchMerchant(RawEmail email, List<MerchantRule> rules)
    {
        foreach (var rule in rules)
        {
            var senderOk = string.IsNullOrEmpty(rule.SenderContains)
                || email.Sender.Contains(rule.SenderContains, StringComparison.OrdinalIgnoreCase);

            var subjectOk = string.IsNullOrEmpty(rule.SubjectContains)
                || email.Subject.Contains(rule.SubjectContains, StringComparison.OrdinalIgnoreCase);

            // Una regola senza alcun criterio matcherebbe tutto: si scarta.
            var hasCriteria = !string.IsNullOrEmpty(rule.SenderContains) || !string.IsNullOrEmpty(rule.SubjectContains);

            if (hasCriteria && senderOk && subjectOk)
            {
                return rule;
            }
        }

        return null;
    }

    private static DocumentKind InferKind(string subject, PaymentDirection direction)
    {
        if (direction == PaymentDirection.Incoming)
        {
            return DocumentKind.Refund;
        }

        if (subject.Contains("fattura", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("invoice", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Invoice;
        }

        if (subject.Contains("abbonamento", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("rinnovo", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("subscription", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Subscription;
        }

        if (subject.Contains("scaduta", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("sollecito", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("reminder", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Reminder;
        }

        if (subject.Contains("non riuscito", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("rifiutato", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("declined", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Failed;
        }

        return DocumentKind.Receipt;
    }

    /// <summary>Senza una regola dedicata, il dominio del mittente e' l'approssimazione migliore.</summary>
    private static string? InferMerchant(RawEmail email)
    {
        if (!string.IsNullOrWhiteSpace(email.SenderDisplayName))
        {
            return email.SenderDisplayName.Trim();
        }

        var at = email.Sender.IndexOf('@');
        if (at < 0 || at == email.Sender.Length - 1)
        {
            return null;
        }

        var domain = email.Sender[(at + 1)..];
        var firstDot = domain.IndexOf('.');
        return firstDot > 0 ? domain[..firstDot] : domain;
    }

    private static string? ExtractReference(string text)
    {
        var match = ReferenceRegex().Match(text);
        return match.Success ? match.Groups["ref"].Value : null;
    }

    private static bool ContainsAny(string haystack, List<string> needles, out string? matched)
    {
        foreach (var needle in needles)
        {
            if (!string.IsNullOrEmpty(needle) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                matched = needle;
                return true;
            }
        }

        matched = null;
        return false;
    }
}
