using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eqfal.API.Services
{
    public record PaymentInvoiceResult(string PaymentId, string PaymentUrl);
    
    public record PaymentStatusResult(string Id, string Status, decimal Amount, string Currency);

    public interface IPaymentGatewayService
    {
        bool IsConfigured { get; }
        
        Task<PaymentInvoiceResult?> CreateInvoiceAsync(
            decimal amount,
            string currency,
            string description,
            IReadOnlyDictionary<string, string>? invoiceParams,
            CancellationToken ct = default);

        Task<PaymentStatusResult?> GetPaymentAsync(string paymentId, CancellationToken ct = default);

        bool VerifyWebhookSignature(string rawBody, string? signatureHeader);
    }
}
