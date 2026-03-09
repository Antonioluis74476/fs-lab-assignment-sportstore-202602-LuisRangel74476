using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using Xunit;

namespace SportsStore.Tests
{
	public class OrderControllerTests
	{
		private static ILogger<OrderController> GetLogger()
			=> new Mock<ILogger<OrderController>>().Object;

		[Fact]
		public void Cannot_Checkout_Empty_Cart()
		{
			Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
			Cart cart = new Cart();
			Order order = new Order();
			OrderController target = new OrderController(mock.Object, cart, GetLogger());
			ViewResult? result = target.Checkout(order) as ViewResult;
			mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
			Assert.True(string.IsNullOrEmpty(result?.ViewName));
			Assert.False(result?.ViewData.ModelState.IsValid);
		}

		[Fact]
		public void Cannot_Checkout_Invalid_ShippingDetails()
		{
			Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
			Cart cart = new Cart();
			cart.AddItem(new Product(), 1);
			OrderController target = new OrderController(mock.Object, cart, GetLogger());
			target.ModelState.AddModelError("error", "error");
			ViewResult? result = target.Checkout(new Order()) as ViewResult;
			mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
			Assert.True(string.IsNullOrEmpty(result?.ViewName));
			Assert.False(result?.ViewData.ModelState.IsValid);
		}

		[Fact]
		public void Can_Checkout_And_Submit_Order()
		{
			Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
			Cart cart = new Cart();
			cart.AddItem(new Product(), 1);
			OrderController target = new OrderController(mock.Object, cart, GetLogger());

			RedirectToActionResult? result =
				target.Checkout(new Order()) as RedirectToActionResult;

			mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Once);
			Assert.Equal("Pay", result?.ActionName);
			Assert.Equal("Payment", result?.ControllerName);
		}
	}
}