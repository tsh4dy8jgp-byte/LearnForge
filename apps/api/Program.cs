using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LearnForge.Api;
using LearnForge.Api.Endpoints;
using LearnForge.Api.Startup;
using LearnForge.Core;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = ContentEngine.MaxSourceBytes);
if (builder.Configuration["Database:Provider"] == "Postgres")
{
    builder.Services.AddDbContext<PostgresDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
    builder.Services.AddScoped<AppDb>(s => s.GetRequiredService<PostgresDb>());
}
else builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=data/learnforge.db"));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.RespectNullableAnnotations = true;
    o.SerializerOptions.RespectRequiredConstructorParameters = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "learnforge-session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));
builder.Services.AddDataProtection().SetApplicationName("LearnForge").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data", "keys")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AttemptService>();
builder.Services.AddSingleton<ReleaseCache>();
builder.Services.AddHostedService<ExpiryWorker>();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    try { await next(context); }
    catch (DomainError e) { await Results.Problem(e.Message, statusCode: e.Status).ExecuteAsync(context); }
    catch (DbUpdateConcurrencyException) { await Results.Problem("A newer version exists. Reload and retry.", statusCode: 409).ExecuteAsync(context); }
    catch (DbUpdateException) { await Results.Problem("The operation conflicts with an existing record. Refresh and retry.", statusCode: 409).ExecuteAsync(context); }
    catch (BadHttpRequestException) { await Results.Problem("Invalid request body.", statusCode: 400).ExecuteAsync(context); }
    catch (Exception e)
    {
        app.Logger.LogError("Request {TraceId} failed: {ErrorType}", context.TraceIdentifier, e.GetType().Name);
        await Results.Problem("An unexpected error occurred.", statusCode: 500).ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { await Results.Problem("Security token expired. Refresh the page.", statusCode: 400).ExecuteAsync(context); return; }
    }
    await next(context);
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", async (AppDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new HealthResponse("healthy")) : Results.StatusCode(503));
app.MapAuth();
app.MapCatalog();
app.MapLearner();
app.MapAttempts();
app.MapAuthoring();

if (!await DatabaseInitializer.RunAsync(app, args)) return;
app.Run();
