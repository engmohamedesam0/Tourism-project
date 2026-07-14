using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
namespace Tourist_Project_MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews().AddViewLocalization();
            builder.Services.AddHttpClient();

            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.Configure<RequestLocalizationOptions>(o =>
            {
                o.SetDefaultCulture("en");
                o.AddSupportedCultures("en", "ar", "es", "de", "zh");
                o.AddSupportedUICultures("en", "ar", "es", "de", "zh");
                o.RequestCultureProviders = new[] { new CookieRequestCultureProvider() };
            });

            builder.Services.AddScoped<IDestinationRepository, DestinationRepository>();
            builder.Services.AddScoped<ITouristRepository, TouristRepository>();
            builder.Services.AddScoped<IMissionRepository, MissionRepository>();
            builder.Services.AddScoped<IBranchRepository, BranchRepository>();
            builder.Services.AddScoped<IRewardRepository, RewardRepository>();
            builder.Services.AddScoped<ISponsorRepository, SponsorRepository>();
            builder.Services.AddScoped<ITripPlanRepository, TripPlanRepository>();
            builder.Services.AddScoped<ISiteReviewRepository, SiteReviewRepository>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
            builder.Services.AddDbContext<TouristContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("CS"),
                    o => o.UseNetTopologySuite()));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.User.RequireUniqueEmail = true;
            }).AddEntityFrameworkStores<TouristContext>();

            var app = builder.Build();

            // JSON-driven, idempotent sample-data seeding (see Services/DbInitializer.cs
            // and the SeedData/ folder). Safe to run on every startup: each table is
            // only populated when empty.
            DbInitializer.Initialize(app.Services);

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseRouting();

            app.UseRequestLocalization();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
