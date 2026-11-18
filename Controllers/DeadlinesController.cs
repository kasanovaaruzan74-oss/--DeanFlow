using DeanFlow.Data;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize(Roles = AppRoles.AdminOrDeanery + "," + AppRoles.Management)]
public sealed class DeadlinesController(DeanFlowDbContext db, NotificationService notifications) : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(string range = "week", CancellationToken token = default)
    {
        var today = DateTime.Today;
        var query = db.Documents.AsNoTracking().Where(x => !x.IsArchived && x.DueDate != null);
        query = range switch
        {
            "today" => query.Where(x => x.CompletedAt == null && x.DueDate!.Value.Date == today),
            "tomorrow" => query.Where(x => x.CompletedAt == null && x.DueDate!.Value.Date == today.AddDays(1)),
            "three" => query.Where(x => x.CompletedAt == null && x.DueDate >= today && x.DueDate < today.AddDays(4)),
            "overdue" => query.Where(x => x.CompletedAt == null && x.DueDate < today),
            "completed" => query.Where(x => x.CompletedAt != null),
            _ => query.Where(x => x.CompletedAt == null && x.DueDate >= today && x.DueDate < today.AddDays(8))
        };
        ViewBag.Range = range;
        ViewData["Title"] = "Мерзімдер орталығы";
        return View(await query.OrderBy(x => x.DueDate).Include(x => x.DocumentType).Include(x => x.Status)
            .Include(x => x.Department).Include(x => x.Executors).ThenInclude(x => x.User).Take(300).ToListAsync(token));
    }
}
