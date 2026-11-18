using DeanFlow.Data;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class NotificationsController(DeanFlowDbContext db, NotificationService notifications) : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var items = await db.Notifications.AsNoTracking().Where(x => x.UserId == User.UserId())
            .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(token);
        ViewData["Title"] = "Хабарламалар";
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken token)
    {
        var item = await db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == User.UserId(), token);
        if (item is null) return NotFound();
        if (!item.IsRead) { item.IsRead = true; item.ReadAt = DateTime.Now; await db.SaveChangesAsync(token); }
        return item.DocumentId.HasValue ? RedirectToAction("Details", "Documents", new { id = item.DocumentId }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken token)
    {
        var unread = await db.Notifications.Where(x => x.UserId == User.UserId() && !x.IsRead).ToListAsync(token);
        foreach (var item in unread) { item.IsRead = true; item.ReadAt = DateTime.Now; }
        if (unread.Count > 0) await db.SaveChangesAsync(token);
        TempData["Success"] = $"{unread.Count} хабарлама оқылды деп белгіленді.";
        return RedirectToAction(nameof(Index));
    }
}
