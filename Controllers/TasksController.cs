using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize(Roles = AppRoles.AdminOrDeanery + "," + AppRoles.Executor)]
public sealed class TasksController : AppController
{
    private readonly DeanFlowDbContext db;
    private readonly WorkflowService workflow;
    private readonly AuditService audit;
    private readonly NotificationService notifications;

    public TasksController(DeanFlowDbContext db, WorkflowService workflow, AuditService audit,
        NotificationService notifications) : base(notifications)
    {
        this.db = db; this.workflow = workflow; this.audit = audit; this.notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string range = "active", CancellationToken token = default)
    {
        IQueryable<ExecutorAssignment> query = db.DocumentExecutors.AsNoTracking();
        if (User.IsInRole(AppRoles.Executor)) query = query.Where(x => x.UserId == User.UserId());
        query = query.Include(x => x.User).Include(x => x.Document).ThenInclude(x => x!.DocumentType)
            .Include(x => x.Document).ThenInclude(x => x!.Status);
        var today = DateTime.Today;
        query = range switch
        {
            "today" => query.Where(x => x.CompletedAt == null && x.DueDate.HasValue && x.DueDate.Value.Date == today),
            "soon" => query.Where(x => x.CompletedAt == null && x.DueDate >= today && x.DueDate < today.AddDays(4)),
            "week" => query.Where(x => x.CompletedAt == null && x.DueDate >= today && x.DueDate < today.AddDays(8)),
            "overdue" => query.Where(x => x.CompletedAt == null && x.DueDate < today),
            "completed" => query.Where(x => x.CompletedAt != null),
            _ => query.Where(x => x.CompletedAt == null)
        };
        ViewBag.Range = range;
        ViewData["Title"] = User.IsInRole(AppRoles.Executor) ? "Менің тапсырмаларым" : "Орындаушылардың тапсырмалары";
        return View(await query.OrderBy(x => x.DueDate).ThenByDescending(x => x.AssignedAt).Take(300).ToListAsync(token));
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpGet]
    public async Task<IActionResult> Assign(int documentId, CancellationToken token)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == documentId && !x.IsArchived, token);
        if (document is null) return NotFound();
        ViewBag.Document = document;
        ViewBag.Executors = await db.Users.AsNoTracking().Where(x => x.IsActive && x.Role != null && x.Role.RoleName == AppRoles.Executor)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(token);
        ViewData["Title"] = "Орындаушы тағайындау";
        return View(new TaskAssignmentInput { DocumentId = documentId, DueDate = document.DueDate });
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(TaskAssignmentInput input, CancellationToken token)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.DocumentId == input.DocumentId && !x.IsArchived, token);
        if (document is null) return NotFound();
        var executor = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.UserId == input.UserId && x.IsActive && x.Role!.RoleName == AppRoles.Executor, token);
        if (executor is null) ModelState.AddModelError(nameof(input.UserId), "Белсенді орындаушыны таңдаңыз.");
        if (input.DueDate.HasValue && input.DueDate.Value.Date < DateTime.Today) ModelState.AddModelError(nameof(input.DueDate), "Мерзім бүгіннен ерте болмауы керек.");
        if (!ModelState.IsValid)
        {
            ViewBag.Document = document;
            ViewBag.Executors = await db.Users.AsNoTracking().Where(x => x.IsActive && x.Role != null && x.Role.RoleName == AppRoles.Executor).OrderBy(x => x.LastName).ToListAsync(token);
            return View(input);
        }
        var assignment = new ExecutorAssignment
        {
            DocumentId = document.DocumentId, UserId = executor!.UserId, AssignedByUserId = User.UserId(),
            TaskDescription = input.TaskDescription?.Trim(), AssignedAt = DateTime.Now,
            DueDate = input.DueDate?.Date, IsPrimary = input.IsPrimary
        };
        if (assignment.IsPrimary)
        {
            var previous = await db.DocumentExecutors.Where(x => x.DocumentId == document.DocumentId && x.IsPrimary).ToListAsync(token);
            foreach (var item in previous) item.IsPrimary = false;
        }
        db.DocumentExecutors.Add(assignment);
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = document.DocumentId, UserId = User.UserId(), ActionType = "ASSIGN", OldStatusId = document.StatusId, NewStatusId = document.StatusId, Description = $"Орындаушы тағайындалды: {executor.FullName}", ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("ASSIGN", "DocumentExecutors", assignment.DocumentExecutorId, $"Құжат {document.RegistrationNumber}; орындаушы {executor.FullName}", User.UserId());
        await notifications.AddAsync(executor.UserId, document.DocumentId, "Жаңа тапсырма", $"{document.RegistrationNumber}: {document.Title}", "Assignment", token);
        TempData["Success"] = $"{executor.FullName} орындаушы ретінде тағайындалды.";
        return RedirectToAction("Details", "Documents", new { id = document.DocumentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int assignmentId, string status, CancellationToken token)
    {
        var assignment = await db.DocumentExecutors.Include(x => x.Document).ThenInclude(x => x!.Status)
            .FirstOrDefaultAsync(x => x.DocumentExecutorId == assignmentId, token);
        if (assignment?.Document is null) return NotFound();
        var isManager = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.DeanerySpecialist);
        if (!isManager && assignment.UserId != User.UserId()) return Forbid();
        if (status == "start")
        {
            if (assignment.CompletedAt.HasValue) { TempData["Error"] = "Аяқталған тапсырманы бастауға болмайды."; return RedirectToAction(nameof(Index)); }
            var changed = await workflow.ChangeStatusAsync(assignment.Document, "inprogress", User.UserId(), token);
            TempData[changed.Success ? "Success" : "Error"] = changed.Message;
        }
        else if (status == "complete")
        {
            if (assignment.CompletedAt.HasValue) { TempData["Error"] = "Тапсырма бұрын аяқталған."; return RedirectToAction(nameof(Index)); }
            if (WorkflowService.Stage(assignment.Document.Status) != "inprogress")
            {
                TempData["Error"] = "Алдымен тапсырманы бастап, құжатты орындау күйіне ауыстырыңыз.";
                return RedirectToAction(nameof(Index));
            }
            assignment.CompletedAt = DateTime.Now;
            await db.SaveChangesAsync(token);
            await workflow.RecordAsync(assignment.Document, User.UserId(), "TASK_COMPLETED", "Орындаушы тапсырманы аяқтады.", token);
            await audit.WriteAsync("UPDATE", "DocumentExecutors", assignment.DocumentExecutorId, "Тапсырма аяқталды.", User.UserId());
            var allComplete = !await db.DocumentExecutors.AnyAsync(x => x.DocumentId == assignment.DocumentId && x.CompletedAt == null, token);
            if (allComplete && WorkflowService.Stage(assignment.Document.Status) == "inprogress")
            {
                var changed = await workflow.ChangeStatusAsync(assignment.Document, "completed", User.UserId(), token);
                TempData[changed.Success ? "Success" : "Error"] = changed.Message;
            }
            else TempData["Success"] = "Тапсырма аяқталды.";
        }
        else return BadRequest();
        return Redirect(Request.Headers.Referer.ToString().Contains("Documents/Details", StringComparison.OrdinalIgnoreCase)
            ? Url.Action("Details", "Documents", new { id = assignment.DocumentId })! : Url.Action(nameof(Index))!);
    }
}
