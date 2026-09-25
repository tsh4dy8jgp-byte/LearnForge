using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LearnForge.Api;
using LearnForge.Core;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

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
var auth = app.MapGroup("/api/auth");
auth.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) => new CsrfResponse(anti.GetAndStoreTokens(ctx).RequestToken!));
auth.MapPost("/register", async (RegisterRequest request, UserManager<User> users, SignInManager<User> signIn) =>
{
    if (!builder.Configuration.GetValue("Auth:AllowRegistration", true)) return Results.Problem("Registration is closed.", statusCode: 403);
    if (request.Email.Length > 254 || request.Password.Length > 128 || request.DisplayName.Length is < 1 or > 80 || !System.Net.Mail.MailAddress.TryCreate(request.Email, out var address) || address.Address != request.Email) return Results.BadRequest(new ApiErrorResponse("Enter a valid email, a display name, and a 12–128 character password."));
    var user = new User { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded) return Results.BadRequest(new ApiErrorResponse("Registration failed. Use a unique email and a 12+ character password with upper/lowercase letters and a number."));
    await signIn.SignInAsync(user, false);
    return Results.Ok(new RegisterResponse(user.DisplayName, user.Email!));
}).RequireRateLimiting("auth");
auth.MapPost("/login", async (LoginRequest request, SignInManager<User> signIn) =>
{
    if (request.Email.Length > 254 || request.Password.Length > 128) return Results.Unauthorized();
    var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, false, true);
    return result.Succeeded ? Results.Ok() : Results.Json(new ApiErrorResponse("Sign-in failed. Check your details or try again later."), statusCode: 401);
}).RequireRateLimiting("auth");
auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<User> users) =>
{
    var user = await users.GetUserAsync(principal);
    return user is null ? Results.Unauthorized() : Results.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Email!, await users.IsInRoleAsync(user, "Publisher")));
}).RequireAuthorization();
auth.MapPost("/logout", async (SignInManager<User> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
auth.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn) =>
{
    var user = (await users.GetUserAsync(principal))!;
    if (request.NewPassword.Length > 128 || request.CurrentPassword.Length > 128) return Results.BadRequest();
    var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded) return Results.BadRequest(new ApiErrorResponse("Password change failed. Check the current password and the new password requirements."));
    await users.UpdateSecurityStampAsync(user); await signIn.RefreshSignInAsync(user); return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("auth");

app.MapGet("/api/catalog", async (AppDb db) => (await db.Packs.OrderByDescending(p => p.PublishedAt).ToListAsync()).DistinctBy(p => p.PackId).Select(release =>
{
    var p = Json.Read<Pack>(release.ContentJson);
    return new CatalogSummaryDto(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length);
}).ToArray());
app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ClaimsPrincipal user) =>
{
    var release = await db.Packs.Where(p => p.PackId == id).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
    if (release is null) return Results.NotFound();
    var p = Json.Read<Pack>(release.ContentJson);
    var completed = user.Identity?.IsAuthenticated == true ? await db.Completions.Where(c => c.UserId == UserId(user) && c.ReleaseId == release.Id).Select(c => c.LessonId).ToArrayAsync() : [];
    return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
        p.Lessons, p.Blueprints, p.Sources, p.Readiness, completed, p.Questions.Length));
});
var me = app.MapGroup("/api/me").RequireAuthorization();
me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, AppDb db) =>
{
    var release = await db.Packs.Where(p => p.PackId == id).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
    if (release is null || !Json.Read<Pack>(release.ContentJson).Lessons.Any(l => l.Id == lessonId)) return Results.NotFound();
    if (await db.Completions.FindAsync(UserId(user), release.Id, lessonId) is null) { db.Completions.Add(new() { UserId = UserId(user), ReleaseId = release.Id, LessonId = lessonId }); await db.SaveChangesAsync(); }
    return Results.NoContent();
});
me.MapGet("/dashboard", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
{
    foreach (var a in await db.Attempts.Where(a => a.UserId == UserId(user) && a.Status == AttemptStatus.InProgress).ToListAsync()) await service.Expire(a);
    return await Analytics.Dashboard(db, UserId(user), service.Now);
});
me.MapGet("/attempts", async (ClaimsPrincipal user, AppDb db) => (await db.Attempts.Where(a => a.UserId == UserId(user)).OrderByDescending(a => a.StartedAt).ToListAsync()).Select(Analytics.Summary));
me.MapPost("/attempts", async (StartRequest request, ClaimsPrincipal user, AttemptService service) => service.View(await service.Start(UserId(user), request)));
me.MapGet("/attempts/{id}", async (string id, ClaimsPrincipal user, AttemptService service) => service.View(await service.Find(UserId(user), id)));
me.MapPut("/attempts/{id}/responses", async (string id, ResponseRequest request, ClaimsPrincipal user, AttemptService service) =>
{
    var a = await service.Find(UserId(user), id); await service.Save(a, request); return service.View(a);
});
me.MapPost("/attempts/{id}/submit", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
{
    var a = await service.Find(UserId(user), id); await service.Submit(a, request.Revision); return service.View(a);
});
me.MapPost("/attempts/{id}/section", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
{
    var a = await service.Find(UserId(user), id); await service.NextSection(a, request.Revision); return service.View(a);
});
me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
{
    var attempts = await db.Attempts.Where(a => a.UserId == UserId(user)).ToListAsync();
    var export = new LearnerExportDto(service.Now, await Analytics.Dashboard(db, UserId(user), service.Now), attempts.Select(service.View).ToArray());
    return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
});
me.MapDelete("/account", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn, AppDb db) =>
{
    var user = (await users.GetUserAsync(principal))!;
    if (request.Password.Length > 128 || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
    var audit = await db.Audit.Where(a => a.ActorId == user.Id).ToListAsync(); db.Audit.RemoveRange(audit);
    await db.SaveChangesAsync();
    var deleted = await users.DeleteAsync(user);
    if (!deleted.Succeeded) return Results.Problem("Account deletion failed.");
    await signIn.SignOutAsync(); return Results.NoContent();
});
var authoring = app.MapGroup("/api/authoring").RequireAuthorization(policy => policy.RequireRole("Publisher"));
authoring.MapPost("/validate", (JsonElement source) =>
{
    var result = ContentEngine.Compile(source.GetRawText());
    return new ValidationResponseDto(result.Success, result.Hash, result.Diagnostics, result.Pack?.Questions.Length, result.Pack?.Lessons.Length);
});
authoring.MapPost("/publish", async (JsonElement source, AppDb db, ClaimsPrincipal user) =>
{
    var result = ContentEngine.Compile(source.GetRawText());
    if (!result.Success) return Results.BadRequest(new ApiErrorResponse("Content validation failed."));
    var p = result.Pack!;
    if (await db.Packs.AnyAsync(r => r.PackId == p.Id && r.Version == p.Version)) return Results.Conflict(new ApiErrorResponse("This release already exists. Publish a new version."));
    var release = new PackRelease { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = result.Hash };
    db.Packs.Add(release); db.Audit.Add(new() { ActorId = UserId(user), Action = "pack.published", ResourceId = release.Id }); await db.SaveChangesAsync();
    return Results.Ok(new PublishResponseDto(p.Id, p.Version));
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    // Separate migrations preserve each provider's native types and identity columns.
    if (builder.Configuration.GetValue("Database:AutoMigrate", true) || args.Contains("--migrate"))
        await db.Database.MigrateAsync();
    var packDirectory = Path.Combine(AppContext.BaseDirectory, "packs");
    if (Directory.Exists(packDirectory))
        foreach (var path in Directory.GetFiles(packDirectory, "*.json", SearchOption.AllDirectories))
        {
            var compiled = ContentEngine.Compile(await File.ReadAllTextAsync(path));
            if (!compiled.Success) throw new InvalidOperationException($"Invalid seed pack {Path.GetFileName(path)}: {Json.Write(compiled.Diagnostics)}");
            var p = compiled.Pack!;
            if (!await db.Packs.AnyAsync(x => x.PackId == p.Id && x.Version == p.Version)) db.Packs.Add(new() { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = compiled.Hash });
        }
    await db.SaveChangesAsync();
    if (args.Contains("--migrate")) return;
    var grantIndex = Array.IndexOf(args, "--grant-publisher");
    if (grantIndex >= 0)
    {
        if (args.Length <= grantIndex + 1) throw new InvalidOperationException("Supply an existing account email.");
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await manager.FindByEmailAsync(args[grantIndex + 1]) ?? throw new InvalidOperationException("Account must be registered first.");
        if (!await roles.RoleExistsAsync("Publisher")) await roles.CreateAsync(new("Publisher"));
        var result = await manager.AddToRoleAsync(user, "Publisher");
        if (!result.Succeeded && !await manager.IsInRoleAsync(user, "Publisher")) throw new InvalidOperationException("Role grant failed.");
        app.Logger.LogInformation("Publisher role granted. Sign out and back in to refresh the session.");
        return;
    }
}
app.Run();
