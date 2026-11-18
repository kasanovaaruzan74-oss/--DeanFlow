using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class DocumentsController : AppController
{
    private readonly DeanFlowDbContext db;
    private readonly DocumentAccessService access;
    private readonly WorkflowService workflow;
    private readonly AuditService audit;
    private readonly NotificationService notifications;

    public DocumentsController(DeanFlowDbContext db, DocumentAccessService access, WorkflowService workflow,
        AuditService audit, NotificationService notifications) : base(notifications)
    {
        this.db = db; this.access = access; this.workflow = workflow; this.audit = audit; this.notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? direction, string? statusCode, string? priority, int? departmentId,
        int? documentTypeId, DateTime? from, DateTime? to, int page = 1, CancellationToken token = default)
    {
        if (page < 1) page = 1;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var query = access.VisibleTo(User.UserId(), role).Where(d => !d.IsArchived);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(d => d.RegistrationNumber.Contains(term) || d.Title.Contains(term) ||
                (d.SenderName != null && d.SenderName.Contains(term)) || (d.RecipientName != null && d.RecipientName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(direction)) query = query.Where(d => d.Direction == direction);
        if (!string.IsNullOrWhiteSpace(statusCode)) query = query.Where(d => d.Status != null && d.Status.StatusCode == statusCode);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(d => d.Priority == priority);
        if (departmentId.HasValue) query = query.Where(d => d.DepartmentId == departmentId);
        if (documentTypeId.HasValue) query = query.Where(d => d.DocumentTypeId == documentTypeId);
        if (from.HasValue) query = query.Where(d => d.RegisteredAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(d => d.RegisteredAt < to.Value.Date.AddDays(1));
        var model = new DocumentListViewModel { Query = q, Direction = direction, StatusCode = statusCode, Priority = priority, DepartmentId = departmentId, DocumentTypeId = documentTypeId, From = from, To = to, Page = page };
        model.TotalCount = await query.CountAsync(token);
        model.Documents = await query.OrderByDescending(d => d.RegisteredAt).ThenByDescending(d => d.DocumentId)
            .Skip((page - 1) * model.PageSize).Take(model.PageSize).Include(d => d.DocumentType).Include(d => d.Status)
            .Include(d => d.Department).Include(d => d.Executors).ThenInclude(x => x.User).ToListAsync(token);
        await LoadLookups(model, token);
        ViewData["Title"] = string.IsNullOrWhiteSpace(direction) ? "Құжаттар" : direction + " құжаттар";
        return View(model);
    }

    [Authorize(Roles = AppRoles.Student), HttpGet]
    public async Task<IActionResult> My(CancellationToken token)
    {
        var query = access.VisibleTo(User.UserId(), AppRoles.Student).Where(d => !d.IsArchived);
        var items = await query.OrderByDescending(d => d.RegisteredAt).Include(d => d.DocumentType).Include(d => d.Status).ToListAsync(token);
        ViewData["Title"] = "Өтініштерім";
        return View("My", items);
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpGet]
    public async Task<IActionResult> Archive(string? q, int page = 1, CancellationToken token = default)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.IsArchived);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(d => d.RegistrationNumber.Contains(q) || d.Title.Contains(q));
        var model = new DocumentListViewModel { Query = q, Page = Math.Max(1, page) };
        model.TotalCount = await query.CountAsync(token);
        model.Documents = await query.OrderByDescending(d => d.ArchivedAt).Skip((model.Page - 1) * model.PageSize).Take(model.PageSize)
            .Include(d => d.DocumentType).Include(d => d.Status).Include(d => d.Department).ToListAsync(token);
        ViewData["Title"] = "Құжат архиві";
        return View("Archive", model);
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpGet]
    public async Task<IActionResult> Create(CancellationToken token)
    {
        var model = new DocumentFormInput();
        await LoadFormLookups(token);
        ViewData["Title"] = "Құжат тіркеу";
        return View(model);
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DocumentFormInput input, CancellationToken token)
        => await SaveNew(input, false, token);

    [Authorize(Roles = AppRoles.Student), HttpGet]
    public async Task<IActionResult> CreateRequest(CancellationToken token)
    {
        await LoadFormLookups(token);
        ViewData["Title"] = "Жаңа өтініш";
        return View("CreateRequest", new DocumentFormInput { Direction = "Кіріс", DocumentDate = DateTime.Today });
    }

    [Authorize(Roles = AppRoles.Student), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRequest(DocumentFormInput input, CancellationToken token)
        => await SaveNew(input, true, token);

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken token)
    {
        var document = await db.Documents.Include(x => x.Incoming).Include(x => x.Outgoing).FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsArchived, token);
        if (document is null) return NotFound();
        await LoadFormLookups(token);
        ViewData["Title"] = "Құжатты өңдеу";
        return View(new DocumentFormInput
        {
            DocumentId = document.DocumentId, Title = document.Title, Description = document.Description, DocumentTypeId = document.DocumentTypeId,
            Direction = document.Direction, SenderName = document.SenderName, RecipientName = document.RecipientName, DepartmentId = document.DepartmentId,
            Priority = document.Priority, DocumentDate = document.DocumentDate.ToDateTime(TimeOnly.MinValue), DueDate = document.DueDate,
            ExternalNumber = document.Incoming?.ExternalNumber, ExternalDate = document.Incoming?.ExternalDate?.ToDateTime(TimeOnly.MinValue),
            SenderOrganization = document.Incoming?.SenderOrganization, ReceivedMethod = document.Incoming?.ReceivedMethod,
            RecipientOrganization = document.Outgoing?.RecipientOrganization, DeliveryMethod = document.Outgoing?.DeliveryMethod
        });
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentFormInput input, CancellationToken token)
    {
        if (input.DocumentId is null) return BadRequest();
        if (input.DueDate.HasValue && input.DueDate.Value.Date < input.DocumentDate.Date) ModelState.AddModelError(nameof(input.DueDate), "Орындау мерзімі құжат күнінен ерте болмауы керек.");
        if (!ValidValues(input)) ModelState.AddModelError(string.Empty, "Құжат бағыты немесе басымдығы дұрыс таңдалмаған.");
        if (!await db.DocumentTypes.AnyAsync(x => x.DocumentTypeId == input.DocumentTypeId && x.IsActive, token)) ModelState.AddModelError(nameof(input.DocumentTypeId), "Белсенді құжат түрін таңдаңыз.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.DepartmentId == input.DepartmentId && x.IsActive, token)) ModelState.AddModelError(nameof(input.DepartmentId), "Белсенді бөлімді таңдаңыз.");
        var document = await db.Documents.Include(x => x.Incoming).Include(x => x.Outgoing).FirstOrDefaultAsync(x => x.DocumentId == input.DocumentId && !x.IsArchived, token);
        if (document is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await LoadFormLookups(token);
            ViewData["Title"] = "Құжатты өңдеу";
            return View(input);
        }
        var before = $"{document.Title}; {document.Priority}; {document.DueDate:yyyy-MM-dd}";
        MapInput(document, input);
        if (input.Direction == "Кіріс")
        {
            if (document.Outgoing is not null) { db.OutgoingDocuments.Remove(document.Outgoing); document.Outgoing = null; }
            document.Incoming ??= new IncomingDocument { DocumentId = document.DocumentId, ReceivedAt = DateTime.Now };
            document.Incoming.ExternalNumber = input.ExternalNumber;
            document.Incoming.ExternalDate = input.ExternalDate.HasValue ? DateOnly.FromDateTime(input.ExternalDate.Value) : null;
            document.Incoming.SenderOrganization = input.SenderOrganization; document.Incoming.ReceivedMethod = input.ReceivedMethod;
        }
        else if (input.Direction == "Шығыс")
        {
            if (document.Incoming is not null) { db.IncomingDocuments.Remove(document.Incoming); document.Incoming = null; }
            document.Outgoing ??= new OutgoingDocument { DocumentId = document.DocumentId };
            document.Outgoing.RecipientOrganization = input.RecipientOrganization; document.Outgoing.DeliveryMethod = input.DeliveryMethod;
        }
        else
        {
            if (document.Incoming is not null) db.IncomingDocuments.Remove(document.Incoming);
            if (document.Outgoing is not null) db.OutgoingDocuments.Remove(document.Outgoing);
            document.Incoming = null; document.Outgoing = null;
        }
        await db.SaveChangesAsync(token);
        await workflow.RecordAsync(document, User.UserId(), "UPDATE", "Құжат өңделді.", token);
        await audit.WriteAsync("UPDATE", "Documents", document.DocumentId, before, User.UserId());
        TempData["Success"] = "Құжат жаңартылды.";
        return RedirectToAction(nameof(Details), new { id = document.DocumentId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var document = await access.FindVisibleAsync(id, User.UserId(), role, token);
        if (document is null) return NotFound();
        ViewData["Title"] = document.RegistrationNumber;
        ViewBag.CanManage = role is AppRoles.Administrator or AppRoles.DeanerySpecialist;
        ViewBag.CanComment = role is AppRoles.Administrator or AppRoles.DeanerySpecialist or AppRoles.Executor or AppRoles.Management or AppRoles.Student;
        ViewBag.CanAssign = role is AppRoles.Administrator or AppRoles.DeanerySpecialist;
        ViewBag.CanApprove = role is AppRoles.Administrator or AppRoles.Management;
        return View(document);
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string targetStage, CancellationToken token)
    {
        var document = await db.Documents.Include(x => x.Status).FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsArchived, token);
        if (document is null) return NotFound();
        if (targetStage == "completed" && await db.DocumentApprovals.AnyAsync(x => x.DocumentId == id && x.DecisionAt == null, token))
        {
            TempData["Error"] = "Құжатта шешілмеген келісім бар. Алдымен келісу кезегін аяқтаңыз.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var result = await workflow.ChangeStatusAsync(document, targetStage, User.UserId(), token);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveDocument(int id, CancellationToken token)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsArchived, token);
        if (document is null) return NotFound();
        document.IsArchived = true; document.ArchivedAt = DateTime.Now;
        await db.SaveChangesAsync(token);
        await workflow.RecordAsync(document, User.UserId(), "ARCHIVE", "Құжат архивке жіберілді.", token);
        await audit.WriteAsync("ARCHIVE", "Documents", id, document.RegistrationNumber, User.UserId());
        TempData["Success"] = "Құжат архивке жіберілді.";
        return RedirectToAction(nameof(Archive));
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken token)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.DocumentId == id && x.IsArchived, token);
        if (document is null) return NotFound();
        document.IsArchived = false; document.ArchivedAt = null;
        await db.SaveChangesAsync(token);
        await workflow.RecordAsync(document, User.UserId(), "RESTORE", "Құжат архивтен қалпына келтірілді.", token);
        await audit.WriteAsync("RESTORE", "Documents", id, document.RegistrationNumber, User.UserId());
        TempData["Success"] = "Құжат қалпына келтірілді.";
        return RedirectToAction(nameof(Archive));
    }

    private async Task<IActionResult> SaveNew(DocumentFormInput input, bool student, CancellationToken token)
    {
        if (student) input.Direction = "Кіріс";
        if (student) input.DepartmentId = User.DepartmentId();
        if (input.DueDate.HasValue && input.DueDate.Value.Date < input.DocumentDate.Date) ModelState.AddModelError(nameof(input.DueDate), "Орындау мерзімі құжат күнінен ерте болмауы керек.");
        if (!ValidValues(input)) ModelState.AddModelError(string.Empty, "Құжат бағыты немесе басымдығы дұрыс таңдалмаған.");
        var type = await db.DocumentTypes.FirstOrDefaultAsync(x => x.DocumentTypeId == input.DocumentTypeId && x.IsActive, token);
        if (type is null) ModelState.AddModelError(nameof(input.DocumentTypeId), "Құжат түрін таңдаңыз.");
        if (!student && input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.DepartmentId == input.DepartmentId && x.IsActive, token)) ModelState.AddModelError(nameof(input.DepartmentId), "Белсенді бөлімді таңдаңыз.");
        if (!ModelState.IsValid)
        {
            await LoadFormLookups(token);
            ViewData["Title"] = student ? "Жаңа өтініш" : "Құжат тіркеу";
            return View(student ? "CreateRequest" : "Create", input);
        }
        var statuses = await db.DocumentStatuses.AsNoTracking().ToListAsync(token);
        var initialStatus = statuses.FirstOrDefault(x => WorkflowService.Stage(x) == "draft") ?? statuses.FirstOrDefault(x => WorkflowService.Stage(x) == "registered");
        if (initialStatus is null)
        {
            ModelState.AddModelError(string.Empty, "Дерекқорда бастапқы құжат мәртебесі жоқ.");
            await LoadFormLookups(token);
            return View(student ? "CreateRequest" : "Create", input);
        }

        if (!student && !await db.DocumentTypes.AnyAsync(x => x.DocumentTypeId == input.DocumentTypeId, token)) return BadRequest();
        var userId = User.UserId();
        var year = DateTime.Now.Year;
        var prefix = $"{NormalizeCode(type!.Code)}-{year}-";
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var existingNumbers = await db.Documents.Where(x => x.RegistrationNumber.StartsWith(prefix)).Select(x => x.RegistrationNumber).ToListAsync(token);
        var nextNumber = existingNumbers.Select(x => int.TryParse(x[(x.LastIndexOf('-') + 1)..], out var n) ? n : 0).DefaultIfEmpty().Max() + 1;
        var document = new DeanDocument
        {
            RegistrationNumber = $"{prefix}{nextNumber:D4}", DocumentTypeId = type.DocumentTypeId, StatusId = initialStatus.StatusId,
            Title = input.Title.Trim(), Description = input.Description?.Trim(), Direction = input.Direction,
            SenderName = student ? User.FindFirst("FullName")?.Value : input.SenderName?.Trim(),
            RecipientName = input.RecipientName?.Trim(), DepartmentId = input.DepartmentId,
            CreatedByUserId = userId, Priority = input.Priority, DocumentDate = DateOnly.FromDateTime(input.DocumentDate),
            RegisteredAt = DateTime.Now, DueDate = input.DueDate?.Date, IsArchived = false
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(token);
        if (input.Direction == "Кіріс")
            db.IncomingDocuments.Add(new IncomingDocument { DocumentId = document.DocumentId, ExternalNumber = input.ExternalNumber, ExternalDate = input.ExternalDate.HasValue ? DateOnly.FromDateTime(input.ExternalDate.Value) : null, SenderOrganization = input.SenderOrganization?.Trim(), ReceivedMethod = input.ReceivedMethod, ReceivedAt = DateTime.Now });
        else if (input.Direction == "Шығыс")
            db.OutgoingDocuments.Add(new OutgoingDocument { DocumentId = document.DocumentId, RecipientOrganization = input.RecipientOrganization?.Trim(), DeliveryMethod = input.DeliveryMethod });
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = document.DocumentId, UserId = userId, ActionType = "CREATE", NewStatusId = document.StatusId, Description = student ? "Студент өтініш берді." : "Құжат тіркелді.", ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        await audit.WriteAsync("CREATE", "Documents", document.DocumentId, document.RegistrationNumber, userId);
        if (student)
            await notifications.NotifyDocumentParticipantsAsync(document, userId, "Жаңа өтініш тіркелді", $"{document.RegistrationNumber}: {document.Title}", "Status", token);
        TempData["Success"] = $"Құжат {document.RegistrationNumber} нөмірімен тіркелді.";
        return RedirectToAction(nameof(Details), new { id = document.DocumentId });
    }

    private async Task LoadLookups(DocumentListViewModel model, CancellationToken token)
    {
        model.Types = await db.DocumentTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.TypeName).ToListAsync(token);
        model.Statuses = await db.DocumentStatuses.AsNoTracking().OrderBy(x => x.StatusName).ToListAsync(token);
        model.Departments = await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DepartmentName).ToListAsync(token);
    }

    private async Task LoadFormLookups(CancellationToken token)
    {
        ViewBag.DocumentTypes = await db.DocumentTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.TypeName).ToListAsync(token);
        ViewBag.Departments = await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DepartmentName).ToListAsync(token);
    }

    private static void MapInput(DeanDocument document, DocumentFormInput input)
    {
        document.Title = input.Title.Trim(); document.Description = input.Description?.Trim(); document.DocumentTypeId = input.DocumentTypeId;
        document.Direction = input.Direction; document.SenderName = input.SenderName?.Trim(); document.RecipientName = input.RecipientName?.Trim();
        document.DepartmentId = input.DepartmentId; document.Priority = input.Priority; document.DocumentDate = DateOnly.FromDateTime(input.DocumentDate); document.DueDate = input.DueDate?.Date;
    }

    private static bool ValidValues(DocumentFormInput input)
        => new[] { "Кіріс", "Шығыс", "Ішкі" }.Contains(input.Direction, StringComparer.OrdinalIgnoreCase) &&
            new[] { "Төмен", "Қалыпты", "Жоғары", "Шұғыл" }.Contains(input.Priority, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeCode(string code)
    {
        var normalized = Regex.Replace(code.Trim().ToUpperInvariant(), "[^A-Z0-9]", "");
        return string.IsNullOrWhiteSpace(normalized) ? "DOC" : normalized;
    }
}
