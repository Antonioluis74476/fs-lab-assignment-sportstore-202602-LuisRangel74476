namespace SportsStore.Infrastructure
{
	public interface IPaymentService
	{
		Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amount, string currency = "usd");
		Task<bool> ConfirmPaymentAsync(string paymentIntentId);
	}

	public class PaymentIntentResult
	{
		public bool Success { get; set; }
		public string? ClientSecret { get; set; }
		public string? PaymentIntentId { get; set; }
		public string? ErrorMessage { get; set; }
	}
}