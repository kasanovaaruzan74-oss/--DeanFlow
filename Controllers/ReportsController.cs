using System.Text;
using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize(Roles = AppRoles.AdminDeaneryManagement)]
public sealed class ReportsController(DeanFlowDbContext db, NotificationService notifications) : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken token)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) ModelState.AddModelError(string.Empty, "Басталу күні аяқталу күнінен кейін болмауы керек.");
        var query = Filter(from, to);
        var model = new ReportViewModel { From = from, To = to };
        model.Total = await query.CountAsync(token);
        model.Incoming = await query.CountAsync(x => x.Direction == "Кіріс", token);
        model.Outgoing = await query.CountAsync(x => x.Direction == "Шығыс", token);
        model.Completed = await query.CountAsync(x => x.CompletedAt != null, token);
        model.Overdue = await query.CountAsync(x => x.CompletedAt == null && x.DueDate < DateTime.Today, token);
        var completedQuery = query.Where(x => x.CompletedAt != null);
        if (await completedQuery.AnyAsync(token)) model.AverageCompletionDays = await completedQuery.AverageAsync(x => EF.Functions.DateDiffDay(x.RegisteredAt, x.CompletedAt!.Value), token);
        var byStatus = await query.GroupBy(x => x.Status!.StatusName).Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync(token);
        model.ByStatus = byStatus.Select(x => (x.Label, x.Count)).ToList();
        var byType = await query.GroupBy(x => x.DocumentType!.TypeName).Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(8).ToListAsync(token);
        model.ByType = byType.Select(x => (x.Label, x.Count)).ToList();
        var byDepartment = await query.GroupBy(x => x.Department == null ? "Бөлім көрсетілмеген" : x.Department.DepartmentName).Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(8).ToListAsync(token);
        model.ByDepartment = byDepartment.Select(x => (x.Label, x.Count)).ToList();
        var byExecutor = await db.DocumentExecutors.AsNoTracking().Where(x => query.Any(d => d.DocumentId == x.DocumentId))
            .GroupBy(x => new { x.User!.FirstName, x.User.LastName, x.User.MiddleName })
            .Select(g => new { Label = g.Key.LastName + " " + g.Key.FirstName + " " + (g.Key.MiddleName ?? ""), Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync(token);
        model.ByExecutor = byExecutor.Select(x => (x.Label.Trim(), x.Count)).ToList();
        var months = await query.GroupBy(x => new { x.RegisteredAt.Year, x.RegisteredAt.Month }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync(token);
        var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        model.ByMonth = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i)).Select(d => (d.ToString("MMM yyyy"), months.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month)?.Count ?? 0)).ToList();
        ViewData["Title"] = "Есептер мен аналитика";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(DateTime? from, DateTime? to, CancellationToken token)
    {
        var rows = await Filter(from, to).OrderByDescending(x => x.RegisteredAt).Take(20000)
            .Select(x => new { x.RegistrationNumber, x.Title, Type = x.DocumentType!.TypeName, x.Direction, Status = x.Status!.StatusName,
                x.Priority, Department = x.Department == null ? "" : x.Department.DepartmentName, x.SenderName, x.RecipientName,
                x.RegisteredAt, x.DueDate, x.CompletedAt }).ToListAsync(token);
        var csv = new StringBuilder();
        csv.AppendLine("Тіркеу нөмірі;Атауы;Түрі;Бағыты;Мәртебесі;Басымдығы;Бөлім;Жіберуші;Алушы;Тіркелген күні;Мерзімі;Аяқталған күні");
        foreach (var row in rows)
            csv.AppendLine(string.Join(';', new[] { row.RegistrationNumber, row.Title, row.Type, row.Direction, row.Status, row.Priority, row.Department,
                row.SenderName ?? "", row.RecipientName ?? "", row.RegisteredAt.ToString("yyyy-MM-dd HH:mm"), row.DueDate?.ToString("yyyy-MM-dd") ?? "", row.CompletedAt?.ToString("yyyy-MM-dd") ?? "" }.Select(CsvCell)));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"deanflow-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    private IQueryable<DeanDocument> Filter(DateTime? from, DateTime? to)
    {
        var query = db.Documents.AsNoTracking().Where(x => !x.IsArchived);
        if (from.HasValue) query = query.Where(x => x.RegisteredAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.RegisteredAt < to.Value.Date.AddDays(1));
        return query;
    }

    private static string CsvCell(string value)
    {
        var safe = value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
