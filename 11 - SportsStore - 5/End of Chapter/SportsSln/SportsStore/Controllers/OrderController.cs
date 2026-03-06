using Microsoft.AspNetCore.Mvc;
using SportsStore.Models;

namespace SportsStore.Controllers
{
	public class OrderController : Controller
	{
		private IOrderRepository repository;
		private Cart cart;
		private readonly ILogger<OrderController> _logger;

		public OrderController(IOrderRepository repoService, Cart cartService,
			ILogger<OrderController> logger)
		{
			repository = repoService;
			cart = cartService;
			_logger = logger;
		}

		public ViewResult Checkout()
		{
			_logger.LogInformation("Checkout page accessed");
			return View(new Order());
		}

		[HttpPost]
		public IActionResult Checkout(Order order)
		{
			_logger.LogInformation(
				"Checkout submitted for customer {Name}, {Line1}, {City}",
				order.Name, order.Line1, order.City);

			if (!cart.Lines.Any())
			{
				_logger.LogWarning("Checkout attempted with empty cart by {Name}", order.Name);
				ModelState.AddModelError("", "Sorry, your cart is empty!");
			}

			if (!ModelState.IsValid)
			{
				_logger.LogWarning("Order checkout failed validation for {Name}", order.Name);
				return View(order); // keep validation messages + user input
			}

			try
			{
				order.Lines = cart.Lines.ToArray();
				order.PaymentStatus = "Pending"; // track until Stripe confirms payment

				repository.SaveOrder(order);

				_logger.LogInformation(
					"Order {OrderId} created for {Name} with {ItemCount} items. Redirecting to payment.",
					order.OrderID, order.Name, order.Lines.Count);

				// IMPORTANT: don't clear cart yet — clear only after successful payment confirmation
				return RedirectToAction("Pay", "Payment", new { orderId = order.OrderID });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to save order for {Name}", order.Name);
				ModelState.AddModelError("", "There was an error processing your order. Please try again.");
				return View(order);
			}
		}
	}
}