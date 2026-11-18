using System.Data.Common;
using System.Security.Claims;
using DeanFlow.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Services;

public sealed class CookieClaimsValidationEvents(DeanFlowDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await Reject(context);
            return;
        }

        try
        {
            var user = await db.Users.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.IsActive, x.FirstName, x.LastName, x.MiddleName, x.Email, x.DepartmentId, RoleName = x.Role!.RoleName })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
            if (user is null || !user.IsActive)
            {
                await Reject(context);
                return;
            }

            var fullName = string.Join(' ', new[] { user.LastName, user.FirstName, user.MiddleName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            var currentName = identity.FindFirst(ClaimTypes.Name)?.Value;
            var currentEmail = identity.FindFirst(ClaimTypes.Email)?.Value;
            var currentRole = identity.FindFirst(ClaimTypes.Role)?.Value;
            var currentDepartment = identity.FindFirst("DepartmentId")?.Value;
            var department = user.DepartmentId?.ToString();
            if (currentName == fullName && currentEmail == user.Email && currentRole == user.RoleName && currentDepartment == department) return;

            var mutableClaimTypes = new HashSet<string>([ClaimTypes.Name, ClaimTypes.Email, ClaimTypes.Role, "FullName", "DepartmentId"]);
            foreach (var claim in identity.Claims.Where(claim => mutableClaimTypes.Contains(claim.Type)).ToList()) identity.RemoveClaim(claim);
            identity.AddClaim(new Claim(ClaimTypes.Name, fullName));
            identity.AddClaim(new Claim("FullName", fullName));
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
            identity.AddClaim(new Claim(ClaimTypes.Role, user.RoleName));
            if (department is not null) identity.AddClaim(new Claim("DepartmentId", department));
            context.ReplacePrincipal(new ClaimsPrincipal(identity));
            context.ShouldRenew = true;
        }
        catch (DbException)
        {
            await Reject(context);
        }
    }

    private static async Task Reject(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
