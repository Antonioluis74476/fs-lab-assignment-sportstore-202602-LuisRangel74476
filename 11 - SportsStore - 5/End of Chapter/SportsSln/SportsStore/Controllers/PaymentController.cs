using Microsoft.AspNetCore.Mvc;
using SportsStore.Infrastructure;
using SportsStore.Models;

namespace SportsStore.Controllers
{
	public class PaymentController : Controller
	{
		private readonly IPaymentService _paymentService;
		private readonly IOrderRepository _orderRepository;
		private readonly Cart _cart;
		private readonly ILogger<PaymentController> _logger;
		private readonly IConfiguration _configuration;

		public PaymentController(
			IPaymentService paymentService,
			IOrderRepository orderRepository,
			Cart cart,
			ILogger<PaymentController> logger,
			IConfiguration configuration)
		{
			_paymentService = paymentService;
			_orderRepository = orderRepository;
			_cart = cart;
			_logger = logger;
			_configuration = configuration;
		}

		// Called after Order form is submitted — shows payment page
		[HttpGet]
		public IActionResult Pay(int orderId)
		{
			var order = _orderRepository.Orders
				.FirstOrDefault(o => o.OrderID == orderId);

			if (order == null)
			{
				_logger.LogWarning("Pay page: Order {OrderId} not found", orderId);
				return RedirectToAction("Index", "Home");
			}

			ViewBag.PublishableKey = _configuration["Stripe:PublishableKey"];
			ViewBag.OrderId = orderId;
			ViewBag.Amount = _cart.ComputeTotalValue();
			return View(order);
		}

		// Creates a PaymentIntent and returns the client secret to the frontend
		[HttpPost]
		public async Task<IActionResult> CreatePaymentIntent([FromBody] PaymentRequest request)
		{
			_logger.LogInformation(
				"Creating PaymentIntent for order {OrderId}, amount {Amount}",
				request.OrderId, request.Amount);

			var result = await _paymentService.CreatePaymentIntentAsync(request.Amount);

			if (!result.Success)
			{
				_logger.LogError(
					"Failed to create PaymentIntent for order {OrderId}: {Error}",
					request.OrderId, result.ErrorMessage);
				return BadRequest(new { error = result.ErrorMessage });
			}

			// Store PaymentIntentId on order
			var order = _orderRepository.Orders
				.FirstOrDefault(o => o.OrderID == request.OrderId);
			if (order != null)
			{
				order.PaymentIntentId = result.PaymentIntentId;
				order.PaymentStatus = "Pending";
				_orderRepository.SaveOrder(order);
			}

			return Ok(new { clientSecret = result.ClientSecret });
		}

		// Called after Stripe.js confirms payment on the frontend
		[HttpPost]
		public async Task<IActionResult> ConfirmPayment(int orderId, string paymentIntentId)
		{
			_logger.LogInformation(
				"Confirming payment for order {OrderId}, intent {IntentId}",
				orderId, paymentIntentId);

			var success = await _paymentService.ConfirmPaymentAsync(paymentIntentId);
			var order = _orderRepository.Orders
				.FirstOrDefault(o => o.OrderID == orderId);

			if (order == null)
				return RedirectToAction("Index", "Home");

			if (success)
			{
				order.PaymentStatus = "Paid";
				_orderRepository.SaveOrder(order);
				_cart.Clear();
				_logger.LogInformation(
					"Order {OrderId} payment confirmed successfully", orderId);
				return RedirectToPage("/Completed", new { orderId });
			}
			else
			{
				order.PaymentStatus = "Failed";
				_orderRepository.SaveOrder(order);
				_logger.LogWarning(
					"Order {OrderId} payment failed for intent {IntentId}",
					orderId, paymentIntentId);
				return RedirectToAction("PaymentFailed", new { orderId });
			}
		}

		public IActionResult PaymentFailed(int orderId)
		{
			_logger.LogWarning("Payment failed view for order {OrderId}", orderId);
			ViewBag.OrderId = orderId;
			return View();
		}

		public IActionResult PaymentCancelled()
		{
			_logger.LogWarning("Payment was cancelled by user");
			return View();
		}
	}

	public class PaymentRequest
	{
		public int OrderId { get; set; }
		public decimal Amount { get; set; }
	}
}