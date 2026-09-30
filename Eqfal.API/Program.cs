using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Initialize Firebase Admin safely
var serviceAccountPath = Path.Combine(AppContext.BaseDirectory, "serviceAccountKey.json");
if (File.Exists(serviceAccountPath) && FirebaseApp.DefaultInstance == null)
{
    try
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(serviceAccountPath)
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine("Firebase init warning: " + ex.Message);
    }
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// Configure CORS for Flutter Web App & SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddSignalR();
// Register AppDbContext with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register custom services
builder.Services.AddScoped<Eqfal.API.Services.IKeywordSeederService, Eqfal.API.Services.KeywordSeederService>();
builder.Services.AddScoped<Eqfal.API.Services.GeminiAnalysisService>();
builder.Services.AddScoped<Eqfal.API.Services.IMessageAnalysisService, Eqfal.API.Services.MessageAnalysisService>();
builder.Services.AddScoped<Eqfal.API.Services.IEmailService, Eqfal.API.Services.EmailService>();
builder.Services.AddScoped<Eqfal.API.Services.IPaymentGatewayService, Eqfal.API.Services.EzonePayGatewayService>();
builder.Services.AddScoped<Eqfal.API.Services.ISubscriptionRenewalService, Eqfal.API.Services.SubscriptionRenewalService>();
builder.Services.AddHostedService<Eqfal.API.Services.AuditLogCleanupHostedService>();
builder.Services.AddHostedService<Eqfal.API.Services.SubscriptionMonitorHostedService>();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, CustomUserIdProvider>();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "EqfalSuperSecretKeyForJWTAuth_1234567890";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "EqfalAPI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "EqfalApp",
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };

        // For SignalR WebSockets Authentication
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Authentication failed: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine("OnChallenge error: " + context.Error + ", " + context.ErrorDescription);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"[SignalR Auth] Token Validated for User: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs/operations") || (path.HasValue && path.Value.Contains("/hubs/operations"))))
                {
                    context.Token = accessToken;
                }

                if (string.IsNullOrEmpty(context.Token))
                {
                    var customAuth = context.Request.Headers["X-Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(customAuth) && customAuth.StartsWith("Bearer "))
                    {
                        context.Token = customAuth.Substring("Bearer ".Length).Trim();
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddOpenApi();

var app = builder.Build();

Eqfal.API.Services.AuditLogger.Initialize(app.Services);

// Automatic seeding of initial plans & subscriptions
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await Eqfal.API.Data.SubscriptionSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Subscription Seeder Warning] {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapGet("/setup-db", (AppDbContext context) => 
    {
        try 
        {
            context.Database.Migrate();
            if (!context.Users.Any())
            {
                context.Users.Add(new Eqfal.API.Models.User 
                { 
                    FullName = "Admin User", 
                    UsernameEmail = "admin@eqfal.com", 
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    IsActive = true
                });
                context.SaveChanges();
            }

            var inactiveUsers = context.Users.Where(u => !u.IsActive).ToList();
            foreach (var user in inactiveUsers)
            {
                user.IsActive = true;
            }
            if (inactiveUsers.Any())
            {
                context.SaveChanges();
            }

            return Microsoft.AspNetCore.Http.Results.Ok("Database created and migrated successfully! You can now login.");
        }
        catch
        {
            return Microsoft.AspNetCore.Http.Results.Problem("ط­ط¯ط« ط®ط·ط£ ط£ط«ظ†ط§ط، ط¥ط¹ط¯ط§ط¯ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ. ظٹط±ط¬ظ‰ ظ…ط±ط§ط¬ط¹ط© ط§ظ„ط³ط¬ظ„ط§طھ.");
        }
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// فحص اشتراك الحساب والوضع المقيد (Read-Only Mode) للحسابات المنتهية
app.UseMiddleware<Eqfal.API.Helpers.SubscriptionAccessMiddleware>();

app.MapControllers();

// Map SignalR Hub to both standard routes
app.MapHub<Eqfal.API.Hubs.OperationsHub>("/hubs/operations");
app.MapHub<Eqfal.API.Hubs.OperationsHub>("/api/hubs/operations");

app.Run();

public class CustomUserIdProvider : Microsoft.AspNetCore.SignalR.IUserIdProvider
{
    public string? GetUserId(Microsoft.AspNetCore.SignalR.HubConnectionContext connection)
    {
        return connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("nameid")?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
