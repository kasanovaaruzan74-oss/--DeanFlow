using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize(Roles = AppRoles.AdminOrDeanery + "," + AppRoles.Management)]
public sealed class ApprovalsController : AppController
{
    private readonly DeanFlowDbContext db;
    private readonly WorkflowService workflow;
    private readonly AuditService audit;
    private readonly NotificationService notifications;

    public ApprovalsController(DeanFlowDbContext db, WorkflowService workflow, AuditService audit,
        NotificationService notifications) : base(notifications)
    {
        this.db = db; this.workflow = workflow; this.audit = audit; this.notifications = notifications;
    }

    [Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Management), HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var query = db.DocumentApprovals.AsNoTracking().Where(x => x.DecisionAt == null &&
            !db.DocumentApprovals.Any(previous => previous.DocumentId == x.DocumentId && previous.DecisionAt == null && previous.ApprovalOrder < x.ApprovalOrder));
        if (User.IsInRole(AppRoles.Management)) query = query.Where(x => x.ApproverUserId == User.UserId());
        var approvals = await query.OrderBy(x => x.AssignedAt).ThenBy(x => x.ApprovalOrder)
            .Include(x => x.Document).ThenInclude(x => x!.Status)
            .Include(x => x.Document).ThenInclude(x => x!.DocumentType)
            .Include(x => x.Approver).Take(250).ToListAsync(token);
        ViewData["Title"] = "Келісу кезегі";
        return View(approvals);
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpGet]
    public async Task<IActionResult> Assign(int documentId, CancellationToken token)
    {
        var document = await db.Documents.Include(x => x.Status).FirstOrDefaultAsync(x => x.DocumentId == documentId && !x.IsArchived, token);
        if (document is null) return NotFound();
        ViewBag.Document = document;
        ViewBag.Approvers = await db.Users.AsNoTracking().Where(x => x.IsActive && x.Role != null &&
            (x.Role.RoleName == AppRoles.Management || x.Role.RoleName == AppRoles.Administrator)).OrderBy(x => x.LastName).ToListAsync(token);
        ViewData["Title"] = "Келісуге жіберу";
        return View(new ApprovalAssignmentInput { DocumentId = documentId });
    }

    [Authorize(Roles = AppRoles.AdminOrDeanery), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(ApprovalAssignmentInput input, CancellationToken token)
    {
        var document = await db.Documents.Include(x => x.Status).FirstOrDefaultAsync(x => x.DocumentId == input.DocumentId && !x.IsArchived, token);
        if (document is null) return NotFound();
        var stage = WorkflowService.Stage(document.Status);
        if (stage is not ("completed" or "approval"))
            ModelState.AddModelError(string.Empty, "Құжат келісуге жіберілмес бұрын орындалуы аяқталуы керек.");
        var approver = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.UserId == input.ApproverUserId && x.IsActive &&
            (x.Role!.RoleName == AppRoles.Management || x.Role.RoleName == AppRoles.Administrator), token);
        if (approver is null) ModelState.AddModelError(nameof(input.ApproverUserId), "Белсенді басқарушы немесе әкімшіні таңдаңыз.");
        if (approver is not null && await db.DocumentApprovals.AnyAsync(x => x.DocumentId == document.DocumentId && x.ApproverUserId == approver.UserId && x.DecisionAt == null, token))
            ModelState.AddModelError(nameof(input.ApproverUserId), "Бұл пайдаланушыға белсенді келісім тапсырмасы берілген.");
        if (!ModelState.IsValid)
        {
            ViewBag.Document = document;
            ViewBag.Approvers = await db.Users.AsNoTracking().Where(x => x.IsActive && x.Role != null && (x.Role.RoleName == AppRoles.Management || x.Role.RoleName == AppRoles.Administrator)).OrderBy(x => x.LastName).ToListAsync(token);
            return View(input);
        }
        var order = await db.DocumentApprovals.Where(x => x.DocumentId == document.DocumentId).Select(x => (int?)x.ApprovalOrder).MaxAsync(token) ?? 0;
        var approval = new DocumentApproval { DocumentId = document.DocumentId, ApproverUserId = approver!.UserId, ApprovalOrder = order + 1,
            ApprovalStatus = "Күтуде", AssignedAt = DateTime.Now };
        db.DocumentApprovals.Add(approval);
        await db.SaveChangesAsync(token);
        if (stage == "completed")
        {
            var changed = await workflow.ChangeStatusAsync(document, "approval", User.UserId(), token);
            if (!changed.Success) { TempData["Error"] = changed.Message; return RedirectToAction("Details", "Documents", new { id = document.DocumentId }); }
        }
        else
        {
            db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = document.DocumentId, UserId = User.UserId(), ActionType = "APPROVAL_ASSIGNED", OldStatusId = document.StatusId, NewStatusId = document.StatusId, Description = $"Келісуге жіберілді: {approver.FullName}", ActionDate = DateTime.Now });
            await db.SaveChangesAsync(token);
        }
        await notifications.AddAsync(approver.UserId, document.DocumentId, "Келісуге құжат келді", $"{document.RegistrationNumber}: {document.Title}", "Approval", token);
        await audit.WriteAsync("ASSIGN", "DocumentApprovals", approval.ApprovalId, $"Келісуші тағайындалды: {approver.FullName}", User.UserId());
        TempData["Success"] = $"Құжат {approver.FullName} пайдаланушысына келісуге жіберілді.";
        return RedirectToAction("Details", "Documents", new { id = document.DocumentId });
    }

    [Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Management), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int approvalId, string decision, string? comment, CancellationToken token)
    {
        var approval = await db.DocumentApprovals.Include(x => x.Document).ThenInclude(x => x!.Status)
            .FirstOrDefaultAsync(x => x.ApprovalId == approvalId && x.DecisionAt == null, token);
        if (approval?.Document is null) return NotFound();
        if (User.IsInRole(AppRoles.Management) && approval.ApproverUserId != User.UserId()) return Forbid();
        var earlierPending = await db.DocumentApprovals.AnyAsync(x => x.DocumentId == approval.DocumentId && x.DecisionAt == null && x.ApprovalOrder < approval.ApprovalOrder, token);
        if (earlierPending) { TempData["Error"] = "Алдымен алдыңғы келісушінің шешімін күтіңіз."; return RedirectToAction(nameof(Index)); }
        var normalized = decision.Trim().ToLowerInvariant();
        var status = normalized switch { "approve" => "Келісілді", "return" => "Қайтарылды", "reject" => "Қабылданбады", _ => null };
        if (status is null) return BadRequest();
        if (normalized is "return" or "reject" && string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "Қайтару немесе қабылдамау себебін жазыңыз.";
            return RedirectToAction(nameof(Index));
        }
        approval.ApprovalStatus = status;
        approval.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        approval.DecisionAt = DateTime.Now;
        await db.SaveChangesAsync(token);
        var actionType = normalized switch { "approve" => "APPROVE", "return" => "RETURN", _ => "REJECT" };
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = approval.DocumentId, UserId = User.UserId(), ActionType = actionType, OldStatusId = approval.Document.StatusId, NewStatusId = approval.Document.StatusId, Description = $"{status}. {approval.Comment}", ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
        await audit.WriteAsync(actionType, "DocumentApprovals", approval.ApprovalId, status, User.UserId());
        await notifications.NotifyDocumentParticipantsAsync(approval.Document, User.UserId(), "Келісім шешімі қабылданды", $"{approval.Document.RegistrationNumber}: {status}", "Approval", token);
        var target = normalized switch { "return" => "returned", "reject" => "rejected", _ => null };
        if (target is not null && WorkflowService.Stage(approval.Document.Status) == "approval")
        {
            var change = await workflow.ChangeStatusAsync(approval.Document, target, User.UserId(), token);
            TempData[change.Success ? "Success" : "Error"] = change.Message;
        }
        else if (normalized == "approve")
        {
            var pending = await db.DocumentApprovals.AnyAsync(x => x.DocumentId == approval.DocumentId && x.DecisionAt == null, token);
            var returnedOrRejected = await db.DocumentApprovals.AnyAsync(x => x.DocumentId == approval.DocumentId && (x.ApprovalStatus == "Қайтарылды" || x.ApprovalStatus == "Қабылданбады"), token);
            if (!pending && !returnedOrRejected && WorkflowService.Stage(approval.Document.Status) == "approval")
            {
                var change = await workflow.ChangeStatusAsync(approval.Document, "completed", User.UserId(), token);
                TempData[change.Success ? "Success" : "Error"] = change.Message;
            }
            else TempData["Success"] = "Шешім сақталды. Келесі келісуші кезекке жіберілді.";
        }
        else TempData["Success"] = "Шешім сақталды.";
        return RedirectToAction(nameof(Index));
    }
}
