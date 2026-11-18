using DeanFlow.Data;
using DeanFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Services;

public sealed class WorkflowService(DeanFlowDbContext db, AuditService audit, NotificationService notifications)
{
    public static string Stage(DocumentStatus? status)
    {
        var code = (status?.StatusCode ?? string.Empty).Trim().ToUpperInvariant();
        var name = (status?.StatusName ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Contains("ARCHIV") || name.Contains("архив")) return "archived";
        if (code.Contains("REJECT") || code.Contains("DECLIN") || name.Contains("қабылданба") || name.Contains("отклон")) return "rejected";
        if (code.Contains("RETURN") || code.Contains("REWORK") || name.Contains("қайтар") || name.Contains("возврат")) return "returned";
        if (code.Contains("OVERDUE") || name.Contains("мерзімі өткен") || name.Contains("просроч")) return "overdue";
        if (code.Contains("COMPLETE") || code == "DONE" || name.Contains("орындалды") || name.Contains("заверш")) return "completed";
        if (code.Contains("APPROV") || code.Contains("AGREEMENT") || name.Contains("келіс") || name.Contains("соглас")) return "approval";
        if (code.Contains("PROGRESS") || code == "INWORK" || name.Contains("орындалуда") || name.Contains("исполнении")) return "inprogress";
        if (code.Contains("ASSIGN") || name.Contains("тағайын") || name.Contains("назначен")) return "assigned";
        if (code.Contains("REVIEW") || name.Contains("қаралу") || name.Contains("рассмотр")) return "review";
        if (code.Contains("REGISTER") || name.Contains("тіркел") || name.Contains("зарегистр")) return "registered";
        if (code.Contains("DRAFT") || name.Contains("жоба") || name.Contains("чернов")) return "draft";
        return "other";
    }

    public async Task<(bool Success, string Message)> ChangeStatusAsync(DeanDocument document, string targetStage, int userId, CancellationToken token = default)
    {
        var currentStage = Stage(document.Status);
        var allowed = targetStage switch
        {
            "registered" => currentStage is "draft" or "returned",
            "review" => currentStage is "registered",
            "assigned" => currentStage is "review" or "registered" or "returned",
            "inprogress" => currentStage is "assigned" or "review",
            "approval" => currentStage is "inprogress" or "completed",
            "completed" => currentStage is "inprogress" or "approval",
            "returned" => currentStage is "approval" or "review" or "assigned",
            "rejected" => currentStage is "approval" or "review",
            _ => false
        };
        if (!allowed) return (false, "Бұл құжаттың ағымдағы күйінен таңдалған күйге ауысуға болмайды.");

        var candidates = await db.DocumentStatuses.AsNoTracking().ToListAsync(token);
        var nextStatus = candidates.FirstOrDefault(x => Stage(x) == targetStage);
        if (nextStatus is null) return (false, "Дерекқорда қажетті мәртебе табылмады.");

        var oldStatusId = document.StatusId;
        document.StatusId = nextStatus.StatusId;
        document.Status = nextStatus;
        if (targetStage == "completed") document.CompletedAt = DateTime.Now;
        else if (currentStage == "completed") document.CompletedAt = null;
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = document.DocumentId, UserId = userId, ActionType = "STATUS_CHANGE", OldStatusId = oldStatusId, NewStatusId = nextStatus.StatusId, Description = $"Мәртебе: {nextStatus.StatusName}", ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("STATUS_CHANGE", "Documents", document.DocumentId, $"{oldStatusId} -> {nextStatus.StatusId}", userId);
        await notifications.NotifyDocumentParticipantsAsync(document, userId, "Құжат мәртебесі өзгерді", $"{document.RegistrationNumber}: {nextStatus.StatusName}", "Status", token);
        return (true, "Құжат мәртебесі жаңартылды.");
    }

    public async Task RecordAsync(DeanDocument document, int userId, string action, string? description, CancellationToken token = default)
    {
        db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = document.DocumentId, UserId = userId, ActionType = action, OldStatusId = document.StatusId, NewStatusId = document.StatusId, Description = description, ActionDate = DateTime.Now });
        await db.SaveChangesAsync(token);
    }
}
