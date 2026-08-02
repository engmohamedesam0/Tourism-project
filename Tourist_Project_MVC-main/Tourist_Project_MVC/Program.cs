using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Tourist_Project_MVC.Controllers.HubNotifications;
using Tourist_Project_MVC.Controllers.Middlewares;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
namespace Tourist_Project_MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddUserSecrets<Program>();

            // Add services to the container.
            builder.Services.AddControllersWithViews().AddViewLocalization();

            builder.Services.AddHttpClient(); // registers IHttpClientFactory generally

            builder.Services.AddScoped<IArcGISSyncService, ArcGISSyncService>();

            // AI chat widget (Gemini-backed). A typed HttpClient with a sane
            // timeout — the Gemini call can take a few seconds, especially
            // with tool calling involved.
            builder.Services.AddHttpClient<IAiChatService, AiChatService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            // Explicit header name so [ValidateAntiForgeryToken] accepts the token sent
            // via the "RequestVerificationToken" header on JSON fetch() calls (used by
            // the AI chat widget and the notification panel) — without this, only
            // form-encoded posts would validate.
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });

            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.Configure<RequestLocalizationOptions>(o =>
             {
                 o.SetDefaultCulture("en");
                 o.DefaultRequestCulture = new RequestCulture("en");
                 o.AddSupportedCultures("en", "ar", "es");
                 o.AddSupportedUICultures("en", "ar", "es");
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
            builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
            builder.Services.AddScoped<IUserBadgeRepository, UserBadgeRepository>();
            builder.Services.AddScoped<IUserProgressRepository, UserProgressRepository>();
            builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
            builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
            builder.Services.AddScoped<IGamificationService, GamificationService>();
            builder.Services.AddSingleton<IDocContentProvider, DocsService>();
            builder.Services.AddDbContext<TouristContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("CS"),
                    o => o.UseNetTopologySuite()));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.User.RequireUniqueEmail = true;
            }).AddEntityFrameworkStores<TouristContext>()
            .AddDefaultTokenProviders();

            // JWT bearer scheme for the React Native mobile app (POST /api/auth/login
            // issues the token). This is ADDITIVE — AddIdentity above already set the
            // cookie scheme as the default for the website, and calling
            // AddAuthentication() again here (with no arguments) does not change that
            // default; it only registers "Bearer" as an extra scheme that controllers
            // can authenticate against explicitly (see AiChatController.Send).
            builder.Services.AddAuthentication()
                .AddJwtBearer(options =>
                {
                    var jwtKey = builder.Configuration["Jwt:Key"];
                    // If Jwt:Key hasn't been configured yet, use a random throwaway
                    // key instead of an empty/invalid one — this just means every
                    // real token fails validation (a clean 401) rather than the
                    // options binding itself throwing on the first mobile request.
                    var keyBytes = string.IsNullOrWhiteSpace(jwtKey)
                        ? RandomNumberGenerator.GetBytes(32)
                        : Encoding.UTF8.GetBytes(jwtKey);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                        ClockSkew = TimeSpan.Zero,
                        NameClaimType = ClaimTypes.NameIdentifier,
                        RoleClaimType = ClaimTypes.Role
                    };
                    // ADD THIS BLOCK:
                    // This tells the auth middleware to read the token from the query string 
                    // when a client connects to the SignalR hub.
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!String.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/notificationHub"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MobileApp", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true)  // dev only — accepts any origin
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
            builder.Services.AddSignalR();

            var app = builder.Build();

            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<TouristContext>();
                    context.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Program] Migration failed: {ex.Message}");
            }

            // JSON-driven, idempotent sample-data seeding (see Services/DbInitializer.cs
            // and the SeedData/ folder). Safe to run on every startup: each table is
            // only populated when empty.
            DbInitializer.Initialize(app.Services);

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors("MobileApp");

            app.UseRequestLocalization();
            app.UseAuthentication();  // ← add this, before UseAuthorization
            app.UseAuthorization();
            app.UseMiddleware<UserExistsMiddleware>();

            app.UseStaticFiles();
            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.MapHub<NotificationHub>("/notificationHub");
            app.Run();
        }
    }
}
