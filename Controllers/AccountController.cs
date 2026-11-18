using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

public sealed class AccountController(DeanFlowDbContext db, PasswordHasher<AppUser> passwordHasher, AuditService audit,
    NotificationService notifications, IConfiguration configuration, IWebHostEnvironment environment)
    : AppController(notifications)
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(input);
        try
        {
            var email = input.Email.Trim();
            var user = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Email == email, token);
            if (user is null || !user.IsActive || user.Role is null)
            {
                ModelState.AddModelError(string.Empty, "Электрондық пошта немесе құпиясөз қате.");
                return View(input);
            }

            var initializedDemoPassword = false;
            PasswordVerificationResult verification;
            if (user.PasswordHash.StartsWith("TEMP_PASSWORD_HASH", StringComparison.OrdinalIgnoreCase))
            {
                var setupEnabled = environment.IsDevelopment() && configuration.GetValue<bool>("DeanFlow:DevelopmentDemoLogin:Enabled");
                var configuredPassword = configuration["DeanFlow:DevelopmentDemoLogin:SharedInitialPassword"];
                var submitted = Encoding.UTF8.GetBytes(input.Password);
                var expected = Encoding.UTF8.GetBytes(configuredPassword ?? "");
                var matches = submitted.Length == expected.Length && submitted.Length > 0 && CryptographicOperations.FixedTimeEquals(submitted, expected);
                if (!setupEnabled || !matches)
                {
                    ModelState.AddModelError(string.Empty, "Электрондық пошта немесе құпиясөз қате.");
                    return View(input);
                }
                user.PasswordHash = passwordHasher.HashPassword(user, input.Password);
                initializedDemoPassword = true;
                verification = PasswordVerificationResult.Success;
            }
            else
            {
                try { verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.Password); }
                catch (FormatException) { verification = PasswordVerificationResult.Failed; }
                catch (ArgumentException) { verification = PasswordVerificationResult.Failed; }
            }
            if (verification == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Электрондық пошта немесе құпиясөз қате.");
                return View(input);
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = passwordHasher.HashPassword(user, input.Password);
            user.LastLoginAt = DateTime.Now;
            await db.SaveChangesAsync(token);
            if (initializedDemoPassword)
                await audit.WriteAsync("PASSWORD_INITIALIZE", "Users", user.UserId, "Development placeholder replaced with a one-way password hash.", user.UserId);
            await audit.WriteAsync("LOGIN", "Users", user.UserId, "Пайдаланушы жүйеге кірді.", user.UserId);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new("UserId", user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new("FullName", user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.RoleName)
            };
            if (user.DepartmentId.HasValue) claims.Add(new Claim("DepartmentId", user.DepartmentId.Value.ToString()));
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = input.RememberMe,
                AllowRefresh = true
            });

            return Url.IsLocalUrl(input.ReturnUrl) ? Redirect(input.ReturnUrl!) : RedirectToAction("Index", "Home");
        }
        catch (DbException)
        {
            ModelState.AddModelError(string.Empty, "Дерекқорға қосылу мүмкін болмады. SQL Server және қосылым баптауларын тексеріңіз.");
            return View(input);
        }
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken token)
    {
        var userId = User.UserId();
        try { await audit.WriteAsync("LOGOUT", "Users", userId, "Пайдаланушы жүйеден шықты.", userId); }
        catch (DbException) { /* Allow logout even when the database is temporarily unavailable. */ }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Profile(CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.Role).Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.UserId == User.UserId(), token);
        if (user is null) return NotFound();
        ViewBag.Email = user.Email;
        ViewBag.Role = user.Role?.RoleName;
        ViewBag.Department = user.Department?.DepartmentName;
        return View(new ProfileInput { FirstName = user.FirstName, LastName = user.LastName, MiddleName = user.MiddleName, Phone = user.Phone });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileInput input, CancellationToken token)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.UserId == User.UserId(), token);
        if (user is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(input.NewPassword))
        {
            var validCurrent = !string.IsNullOrWhiteSpace(input.CurrentPassword) &&
                !user.PasswordHash.StartsWith("TEMP_", StringComparison.OrdinalIgnoreCase) &&
                passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) != PasswordVerificationResult.Failed;
            if (!validCurrent) ModelState.AddModelError(nameof(input.CurrentPassword), "Қазіргі құпиясөз дұрыс емес.");
        }
        else if (!string.IsNullOrEmpty(input.CurrentPassword) || !string.IsNullOrEmpty(input.ConfirmPassword))
        {
            ModelState.AddModelError(nameof(input.NewPassword), "Құпиясөзді өзгерту үшін жаңа құпиясөзді енгізіңіз.");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.Email = user.Email;
            ViewBag.Role = (await db.Users.AsNoTracking().Where(x => x.UserId == user.UserId).Select(x => x.Role!.RoleName).FirstOrDefaultAsync(token));
            return View(input);
        }

        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        user.MiddleName = input.MiddleName?.Trim();
        user.Phone = input.Phone?.Trim();
        if (!string.IsNullOrWhiteSpace(input.NewPassword)) user.PasswordHash = passwordHasher.HashPassword(user, input.NewPassword);
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("UPDATE", "Users", user.UserId, "Профиль жаңартылды.", user.UserId);
        TempData["Success"] = "Профиль сақталды.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() { Response.StatusCode = StatusCodes.Status403Forbidden; return View(); }
}
