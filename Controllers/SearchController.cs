using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class SearchController(DocumentAccessService access, NotificationService notifications) : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken token)
    {
        var term = q?.Trim();
        ViewBag.Query = term;
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            ViewData["Title"] = "Құжат іздеу";
            return View(Array.Empty<DeanFlow.Data.Entities.DeanDocument>());
        }
        if (term.Length > 120) term = term[..120];
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var documents = await access.VisibleTo(User.UserId(), role).Where(x => !x.IsArchived && (
                x.RegistrationNumber.Contains(term) || x.Title.Contains(term) ||
                (x.SenderName != null && x.SenderName.Contains(term)) || (x.RecipientName != null && x.RecipientName.Contains(term))))
            .OrderByDescending(x => x.RegisteredAt).Take(50).Include(x => x.DocumentType).Include(x => x.Status).Include(x => x.Department).ToListAsync(token);
        ViewData["Title"] = "Іздеу нәтижелері";
        return View(documents);
    }
}
