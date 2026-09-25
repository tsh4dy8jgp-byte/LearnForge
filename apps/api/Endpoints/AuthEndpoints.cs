using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace LearnForge.Api.Endpoints;

public static class AuthEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAuth()
        {
            var auth = app.MapGroup("/api/auth");
            auth.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) => new CsrfResponse(anti.GetAndStoreTokens(ctx).RequestToken!));
            auth.MapPost("/register", async (RegisterRequest request, UserManager<User> users, SignInManager<User> signIn, IConfiguration configuration) =>
            {
                if (!configuration.GetValue("Auth:AllowRegistration", true)) return Results.Problem("Registration is closed.", statusCode: 403);
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
        }
    }
}
