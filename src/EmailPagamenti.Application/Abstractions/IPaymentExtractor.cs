using EmailPagamenti.Application.Models;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>Decide se una email parla di un pagamento e cosa contiene.</summary>
public interface IPaymentExtractor
{
    PaymentExtraction Extract(RawEmail email);
}
