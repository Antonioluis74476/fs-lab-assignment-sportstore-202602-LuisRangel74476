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

				var cartSummary = _cart.Lines          // ← BEFORE Clear() ✅
					.Select(l => new {
						l.Product.ProductID,
						l.Product.Name,
						l.Quantity,
						l.Product.Price
					})
					.ToList();

				_logger.LogInformation(               // ← BEFORE Clear() ✅
					"Order {OrderId} payment confirmed successfully. Items: {@CartSummary}",
					orderId, cartSummary);

				_cart.Clear();                        // ← AFTER logging ✅
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

		[HttpPost]
		public IActionResult LogPaymentFailure([FromBody] PaymentFailureRequest request)
		{
			var cartSummary = _cart.Lines              // ← cart still has items ✅
				.Select(l => new {
					l.Product.ProductID,
					l.Product.Name,
					l.Quantity,
					l.Product.Price
				})
				.ToList();

			_logger.LogWarning(
				"Payment declined for order {OrderId}: {ErrorCode} - {ErrorMessage}. Items: {@CartSummary}",
				request.OrderId, request.ErrorCode, request.ErrorMessage, cartSummary);

			return Ok();
		}
	}

	public class PaymentRequest
	{
		public int OrderId { get; set; }
		public decimal Amount { get; set; }
	}

	public class PaymentFailureRequest
	{
		public int OrderId { get; set; }
		public string? ErrorCode { get; set; }
		public string? ErrorMessage { get; set; }
	}
}