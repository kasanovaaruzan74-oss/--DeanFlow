using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class CommentsController : AppController
{
    private readonly DeanFlowDbContext db;
    private readonly DocumentAccessService access;
    private readonly AuditService audit;
    private readonly NotificationService notifications;

    public CommentsController(DeanFlowDbContext db, DocumentAccessService access, AuditService audit,
        NotificationService notifications) : base(notifications)
    {
        this.db = db; this.access = access; this.audit = audit; this.notifications = notifications;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int documentId, string? commentText, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Trim().Length > 1500)
        {
            TempData["Error"] = "Пікірді 1-ден 1500 таңбаға дейін енгізіңіз.";
            return RedirectToAction("Details", "Documents", new { id = documentId });
        }
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!await access.CanParticipateAsync(documentId, User.UserId(), role, token)) return NotFound();
        var document = await db.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId, token);
        if (document is null) return NotFound();
        var comment = new DocumentComment { DocumentId = documentId, UserId = User.UserId(), CommentText = commentText.Trim(), CreatedAt = DateTime.Now };
        db.Comments.Add(comment);
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = documentId, UserId = User.UserId(), ActionType = "COMMENT", OldStatusId = document.StatusId, NewStatusId = document.StatusId, Description = "Құжатқа пікір қосылды.", ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("COMMENT", "Comments", comment.CommentId, document.RegistrationNumber, User.UserId());
        await notifications.NotifyDocumentParticipantsAsync(document, User.UserId(), "Жаңа пікір", $"{document.RegistrationNumber}: {User.FindFirst("FullName")?.Value} пікір қалдырды.", "Comment", token);
        TempData["Success"] = "Пікір қосылды.";
        return RedirectToAction("Details", "Documents", new { id = documentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int commentId, string? commentText, CancellationToken token)
    {
        var comment = await db.Comments.Include(x => x.Document).FirstOrDefaultAsync(x => x.CommentId == commentId, token);
        if (comment?.Document is null) return NotFound();
        if (comment.UserId != User.UserId()) return Forbid();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!await access.CanParticipateAsync(comment.DocumentId, User.UserId(), role, token)) return NotFound();
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Trim().Length > 1500)
        {
            TempData["Error"] = "Пікірді 1-ден 1500 таңбаға дейін енгізіңіз.";
            return RedirectToAction("Details", "Documents", new { id = comment.DocumentId });
        }
        comment.CommentText = commentText.Trim(); comment.IsEdited = true; comment.EditedAt = DateTime.Now;
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("UPDATE", "Comments", comment.CommentId, "Пікір өңделді.", User.UserId());
        TempData["Success"] = "Пікір өңделді.";
        return RedirectToAction("Details", "Documents", new { id = comment.DocumentId });
    }
}
