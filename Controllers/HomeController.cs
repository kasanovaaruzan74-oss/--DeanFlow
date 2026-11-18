using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class HomeController(DeanFlowDbContext db, DocumentAccessService access, NotificationService notifications)
    : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken token)
    {
        var userId = User.UserId();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var displayName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";
        var nameParts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var greetingName = nameParts.Length > 1 ? nameParts[1] : nameParts.FirstOrDefault() ?? "Қолданушы";
        var visible = access.VisibleTo(userId, role).Where(x => !x.IsArchived);
        var today = DateTime.Today;
        var requestedYear = year is >= 2000 and <= 2100 ? year.Value : today.Year;
        var requestedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        var calendarMonth = new DateTime(requestedYear, requestedMonth, 1);
        var mondayOffset = ((int)calendarMonth.DayOfWeek + 6) % 7;
        var calendarStart = calendarMonth.AddDays(-mondayOffset);
        var calendarEnd = calendarStart.AddDays(42);
        var model = new DashboardViewModel
        {
            RoleLabel = role ?? "",
            GreetingName = greetingName,
            CalendarMonth = calendarMonth,
            CalendarGridStart = calendarStart
        };
        ViewData["Title"] = "Жұмыс кеңістігі";
        ViewData["Section"] = "Басты бет";
        try
        {
            model.TotalDocuments = await visible.CountAsync(token);
            model.TodayRegistered = await visible.CountAsync(x => x.RegisteredAt >= DateTime.Today && x.RegisteredAt < DateTime.Today.AddDays(1), token);
            model.InProgress = await visible.CountAsync(x => x.Status != null && (x.Status.StatusCode.Contains("PROGRESS") || x.Status.StatusName.Contains("орындалуда")), token);
            model.Completed = await visible.CountAsync(x => x.CompletedAt != null, token);
            model.Overdue = await visible.CountAsync(x => x.DueDate < DateTime.Today && x.CompletedAt == null, token);
            model.PendingApprovals = role is AppRoles.Administrator or AppRoles.DeanerySpecialist or AppRoles.Management
                ? await db.DocumentApprovals.AsNoTracking().CountAsync(x => x.DecisionAt == null, token) : 0;
            model.MyTasks = role == AppRoles.Executor
                ? await db.DocumentExecutors.AsNoTracking().CountAsync(x => x.UserId == userId && x.CompletedAt == null, token)
                : 0;
            model.RecentDocuments = await visible.OrderByDescending(x => x.RegisteredAt).Take(7)
                .Include(x => x.DocumentType).Include(x => x.Status).Include(x => x.Department).ToListAsync(token);
            model.RecentActivity = await visible.SelectMany(x => x.History).OrderByDescending(x => x.ActionDate).Take(5)
                .Include(x => x.User).Include(x => x.Document).ToListAsync(token);
            model.UpcomingTasks = await db.DocumentExecutors.AsNoTracking().Where(x => x.UserId == userId && x.CompletedAt == null
                    && x.Document != null && !x.Document.IsArchived && visible.Any(d => d.DocumentId == x.DocumentId))
                .OrderBy(x => x.DueDate).Take(6).Include(x => x.Document).ThenInclude(x => x!.Status).ToListAsync(token);
            model.RecentNotifications = await db.Notifications.AsNoTracking().Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt).Take(5).ToListAsync(token);
            var documentEvents = await visible.Where(x => x.DueDate.HasValue && x.DueDate >= calendarStart && x.DueDate < calendarEnd)
                .Select(x => new DashboardCalendarEvent
                {
                    DueDate = x.DueDate!.Value,
                    DocumentId = x.DocumentId,
                    Title = x.Title,
                    RegistrationNumber = x.RegistrationNumber,
                    Kind = "document",
                    IsCompleted = x.CompletedAt != null
                }).ToListAsync(token);
            var taskEvents = await db.DocumentExecutors.AsNoTracking().Where(x => x.UserId == userId && x.CompletedAt == null
                    && x.DueDate.HasValue && x.DueDate >= calendarStart && x.DueDate < calendarEnd
                    && x.Document != null && !x.Document.IsArchived && visible.Any(d => d.DocumentId == x.DocumentId))
                .Select(x => new DashboardCalendarEvent
                {
                    DueDate = x.DueDate!.Value,
                    DocumentId = x.DocumentId,
                    Title = x.Document!.Title,
                    RegistrationNumber = x.Document.RegistrationNumber,
                    Kind = "task",
                    IsCompleted = false
                }).ToListAsync(token);
            model.CalendarEvents = documentEvents.Concat(taskEvents).OrderBy(x => x.DueDate).ToList();
            var statuses = await visible.GroupBy(x => x.Status!.StatusName).Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync(token);
            model.StatusSummary = statuses.Select(x => (x.Label, x.Count)).ToList();
            var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            var monthly = await visible.Where(x => x.RegisteredAt >= firstOfMonth).GroupBy(x => new { x.RegisteredAt.Year, x.RegisteredAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync(token);
            model.MonthlySummary = Enumerable.Range(0, 6).Select(i => firstOfMonth.AddMonths(i))
                .Select(date => (date.ToString("MMM", CultureInfo.GetCultureInfo("kk-KZ")), monthly.FirstOrDefault(x => x.Year == date.Year && x.Month == date.Month)?.Count ?? 0)).ToList();
        }
        catch (DbException)
        {
            model.DatabaseAvailable = false;
            model.DatabaseMessage = "Дерекқорға қосылу мүмкін болмады. ConnectionStrings:DefaultConnection және SQL Server қызметін тексеріңіз.";
        }
        catch (InvalidOperationException ex) when (ex.InnerException is DbException)
        {
            model.DatabaseAvailable = false;
            model.DatabaseMessage = "Дерекқор схемасын оқу мүмкін болмады. Кестелердің құрылымы нұсқаулықтағы DeanFlowDB схемасына сай екенін тексеріңіз.";
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult SiteMap()
    {
        ViewData["Title"] = "Сайт картасы";
        ViewData["Section"] = "Навигация";
        return View();
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Privacy() => RedirectToAction("Login", "Account");

    [AllowAnonymous, HttpGet]
    public IActionResult StatusCodeError(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;
        return View("StatusCode");
    }

    [AllowAnonymous, HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
