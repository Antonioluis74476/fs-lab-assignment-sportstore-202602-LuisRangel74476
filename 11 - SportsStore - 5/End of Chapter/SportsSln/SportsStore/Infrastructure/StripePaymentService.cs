using Microsoft.Extensions.Options;
using Stripe;

namespace SportsStore.Infrastructure
{
	public class StripePaymentService : IPaymentService
	{
		private readonly StripeSettings _stripeSettings;
		private readonly ILogger<StripePaymentService> _logger;

		public StripePaymentService(
			IOptions<StripeSettings> stripeSettings,
			ILogger<StripePaymentService> logger)
		{
			_stripeSettings = stripeSettings.Value;
			_logger = logger;
			StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
		}

		public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
			decimal amount, string currency = "usd")
		{
			try
			{
				var options = new PaymentIntentCreateOptions
				{
					Amount = (long)(amount * 100), // Stripe uses cents
					Currency = currency,
					AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
					{
						Enabled = true,
					},
				};

				var service = new PaymentIntentService();
				var intent = await service.CreateAsync(options);

				_logger.LogInformation(
					"PaymentIntent {IntentId} created for amount {Amount} {Currency}",
					intent.Id, amount, currency);

				return new PaymentIntentResult
				{
					Success = true,
					ClientSecret = intent.ClientSecret,
					PaymentIntentId = intent.Id
				};
			}
			catch (StripeException ex)
			{
				_logger.LogError(ex,
					"Stripe error creating PaymentIntent: {Message}", ex.Message);
				return new PaymentIntentResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<bool> ConfirmPaymentAsync(string paymentIntentId)
		{
			try
			{
				var service = new PaymentIntentService();
				var intent = await service.GetAsync(paymentIntentId);

				_logger.LogInformation(
					"PaymentIntent {IntentId} status: {Status}",
					paymentIntentId, intent.Status);

				return intent.Status == "succeeded";
			}
			catch (StripeException ex)
			{
				_logger.LogError(ex,
					"Stripe error confirming payment {IntentId}: {Message}",
					paymentIntentId, ex.Message);
				return false;
			}
		}
	}
}