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
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.Services.AiTools;
namespace Tourist_Project_MVC
{
    public class Program
    {
        private const string InitialMigrationProductVersion = "10.0.10";

        private static void BaselineExistingSchemaIfNeeded(TouristContext context)
        {
            // Some older startup paths created the schema without recording an EF
            // migration. In that state Migrate() tries to recreate AspNetRoles and
            // crashes with PostgreSQL 42P07. Baseline only when the complete initial
            // schema is already present; never drop or alter existing user data.
            context.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """);

            var appliedMigrations = context.Database.GetAppliedMigrations().ToHashSet();
            var initialMigration = context.Database.GetMigrations().FirstOrDefault();
            if (initialMigration == null || appliedMigrations.Count > 0)
            {
                return;
            }

            var existingInitialSchemaTableCount = context.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name = ANY (ARRAY[
                      'AspNetRoles', 'AspNetUsers', 'AspNetRoleClaims',
                      'AspNetUserClaims', 'AspNetUserLogins', 'AspNetUserRoles',
                      'AspNetUserTokens', 'Badges', 'Branches', 'ChatSessions',
                      'Destinations', 'Favorites', 'MenuItems', 'Missions',
                      'Notifications', 'Redemptions', 'Reviews', 'RewardBranches',
                      'RewardViews', 'Rewards', 'SiteReviews',
                      'SponsorApprovalRequests', 'Sponsors', 'SupportTickets',
                      'Tourists', 'TripDestinations', 'TripPlans', 'UserBadges',
                      'UserMissions', 'UserProgress'
                  ]);
                """).Single();

            const int initialSchemaTableCount = 30;
            if (existingInitialSchemaTableCount != initialSchemaTableCount)
            {
                return;
            }

            context.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({initialMigration}, {InitialMigrationProductVersion})
                ON CONFLICT ("MigrationId") DO NOTHING;
                """);

            Console.WriteLine($"[Program] Existing schema detected; baselined EF migration '{initialMigration}'.");
        }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddUserSecrets<Program>();

            // Add services to the container.
            builder.Services.AddControllersWithViews().AddViewLocalization();

            builder.Services.AddHttpClient(); // registers IHttpClientFactory generally

            builder.Services.AddScoped<IArcGISSyncService, ArcGISSyncService>();

            // AI chat widget (Gemini-backed role-aware agent). The typed HttpClient
            // lives on the orchestrator with a generous timeout — the Gemini call
            // can take several seconds, especially with tool calling involved.
            builder.Services.AddHttpClient<IAiAgentOrchestrator, AiAgentOrchestrator>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(90);
            });

            // OpenAI fallback used ONLY when the primary provider (Gemini) reports
            // quota/credit exhaustion. Receives the same context the Gemini request
            // would have received; disabled while OpenAI:ApiKey is empty.
            builder.Services.AddHttpClient<IOpenAiFallbackService, OpenAiFallbackService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(90);
            });

            // Role-aware AI agent services:
            //  - AiIdentityResolver: server-side auth state (user/role/tourist/sponsor)
            //  - AiPendingActionStore: in-memory confirmation store (singleton)
            //  - AiToolRegistry + per-role tool sets: role-filtered, ownership-checked actions
            //  - IChatHistoryService: chat-session persistence (tourists)
            //  - IAiStarterQuestionsService: role-based starter questions
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IAiIdentityResolver, AiIdentityResolver>();
            builder.Services.AddSingleton<AiPendingActionStore>();
            builder.Services.AddScoped<GuestAiTools>();
            builder.Services.AddScoped<TouristAiTools>();
            builder.Services.AddScoped<SponsorAiTools>();
            builder.Services.AddScoped<AdminAiTools>();
            builder.Services.AddScoped<IAiToolRegistry, AiToolRegistry>();
            builder.Services.AddScoped<IChatHistoryService, ChatHistoryService>();
            builder.Services.AddScoped<IAiStarterQuestionsService, AiStarterQuestionsService>();
            builder.Services.AddScoped<IAiChatService, AiChatService>();

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
            // External OAuth login providers (Google / Facebook) for the website.
            // SignInScheme must be the Identity external cookie so SignInManager's
            // ExternalLogin* flow can read the provider result and link it to an
            // ApplicationUser. Credentials come from appsettings.json or user-secrets
            // ("Authentication:Google:*" / "Authentication:Facebook:*").
            //
            // The schemes are ALWAYS registered so the buttons stay live. ASP.NET Core
            // validates OAuth options eagerly on the first request (the auth middleware
            // initializes every handler), so an empty ClientId would crash the whole
            // site — hence the "not-configured" fallback placeholder when user-secrets
            // are missing: validation passes, the app runs, and the moment real
            // credentials are added they take effect without any code change. The
            // AccountController additionally short-circuits the challenge with a
            // friendly message when a provider is not yet configured.
            bool ProviderConfigured(string provider) =>
                !string.IsNullOrWhiteSpace(builder.Configuration[$"Authentication:{provider}:ClientId"]) &&
                !string.IsNullOrWhiteSpace(builder.Configuration[$"Authentication:{provider}:ClientSecret"]);

            // Reads an OAuth credential, treating null AND empty/whitespace values
            // as "not configured". A bare `?? "not-configured"` fallback is NOT
            // enough: appsettings.json contains literal "" placeholders, and `??`
            // only catches null — an empty string would flow into the OAuth
            // options and fail eager validation with the same ClientId error.
            string OAuthValue(string provider, string key)
            {
                var value = builder.Configuration[$"Authentication:{provider}:{key}"];
                return string.IsNullOrWhiteSpace(value) ? "not-configured" : value;
            }

            builder.Services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = OAuthValue("Google", "ClientId");
                    options.ClientSecret = OAuthValue("Google", "ClientSecret");
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddFacebook(options =>
                {
                    options.ClientId = OAuthValue("Facebook", "ClientId");
                    options.ClientSecret = OAuthValue("Facebook", "ClientSecret");
                    options.SignInScheme = IdentityConstants.ExternalScheme;
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
                    BaselineExistingSchemaIfNeeded(context);
                    context.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                // Do not continue into Identity/data seeding when the schema is
                // unavailable. Continuing here produces misleading errors such as
                // "relation AspNetUsers does not exist" later in the request pipeline.
                Console.Error.WriteLine($"[Program] Migration failed: {ex}");
                throw;
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
