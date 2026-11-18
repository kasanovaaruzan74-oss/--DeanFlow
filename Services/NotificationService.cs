using DeanFlow.Data;
using DeanFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Services;

public sealed class NotificationService(DeanFlowDbContext db)
{
    public Task<int> UnreadCountAsync(int userId, CancellationToken token = default)
        => db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, token);

    public async Task AddAsync(int userId, int? documentId, string title, string message, string type, CancellationToken token = default)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            DocumentId = documentId,
            Title = title,
            Message = message,
            NotificationType = type,
            IsRead = false,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync(token);
    }

    public async Task NotifyDocumentParticipantsAsync(DeanDocument document, int actorId, string title, string message, string type, CancellationToken token = default)
    {
        var recipients = await db.DocumentExecutors.AsNoTracking()
            .Where(x => x.DocumentId == document.DocumentId && x.UserId != actorId)
            .Select(x => x.UserId).Distinct().ToListAsync(token);
        if (document.CreatedByUserId != actorId)
            recipients.Add(document.CreatedByUserId);
        foreach (var recipient in recipients.Distinct())
            db.Notifications.Add(new Notification { UserId = recipient, DocumentId = document.DocumentId, Title = title, Message = message, NotificationType = type, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync(token);
    }
}
