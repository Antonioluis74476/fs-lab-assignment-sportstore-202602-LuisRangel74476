using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SportsStore.Infrastructure;
using SportsStore.Models;

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Information()
	//suppress microsoft logs
	.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.Hosting", Serilog.Events.LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.System", Serilog.Events.LogEventLevel.Warning)
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
	.WriteTo.Seq("http://localhost:5341")
	.CreateLogger();

try
{
	Log.Information("SportsStore application starting up");

	var builder = WebApplication.CreateBuilder(args);

	// Now reads MinimumLevel overrides from appsettings.json
	builder.Host.UseSerilog((ctx, lc) => lc
		.ReadFrom.Configuration(ctx.Configuration)
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
		.WriteTo.Seq("http://localhost:5341"));

	builder.Services.AddControllersWithViews();
	builder.Services.AddDbContext<StoreDbContext>(opts => {
		opts.UseSqlServer(
	builder.Configuration["ConnectionStrings:SportsStoreConnection"] ?? "");

	});
	builder.Services.AddScoped<IStoreRepository, EFStoreRepository>();
	builder.Services.AddScoped<IOrderRepository, EFOrderRepository>();
	builder.Services.AddRazorPages();
	builder.Services.AddDistributedMemoryCache();
	builder.Services.AddSession();
	builder.Services.AddScoped<Cart>(sp => SessionCart.GetCart(sp));
	builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
	builder.Services.AddServerSideBlazor();
	builder.Services.AddDbContext<AppIdentityDbContext>(options =>
		options.UseSqlServer(
	builder.Configuration["ConnectionStrings:IdentityConnection"] ?? ""));
	builder.Services.AddIdentity<IdentityUser, IdentityRole>()
		.AddEntityFrameworkStores<AppIdentityDbContext>();

	// Stripe
	builder.Services.Configure<StripeSettings>(
		builder.Configuration.GetSection("Stripe"));
	builder.Services.AddScoped<IPaymentService, StripePaymentService>();

	var app = builder.Build();

	if (app.Environment.IsProduction())
	{
		app.UseExceptionHandler("/error");
	}
	app.UseRequestLocalization(opts => {
		opts.AddSupportedCultures("en-US")
		.AddSupportedUICultures("en-US")
		.SetDefaultCulture("en-US");
	});
	app.UseStaticFiles();
	app.UseSession();
	app.UseAuthentication();
	app.UseAuthorization();
	app.MapControllerRoute("catpage",
		"{category}/Page{productPage:int}",
		new { Controller = "Home", action = "Index" });
	app.MapControllerRoute("page", "Page{productPage:int}",
		new { Controller = "Home", action = "Index", productPage = 1 });
	app.MapControllerRoute("category", "{category}",
		new { Controller = "Home", action = "Index", productPage = 1 });
	app.MapControllerRoute("pagination",
		"Products/Page{productPage}",
		new { Controller = "Home", action = "Index", productPage = 1 });
	app.MapDefaultControllerRoute();
	app.MapRazorPages();
	app.MapBlazorHub();
	app.MapFallbackToPage("/admin/{*catchall}", "/Admin/Index");

	SeedData.EnsurePopulated(app);
	IdentitySeedData.EnsurePopulated(app);

	Log.Information("SportsStore application started successfully");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "SportsStore application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}