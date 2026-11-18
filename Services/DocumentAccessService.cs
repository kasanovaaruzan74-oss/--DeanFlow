using DeanFlow.Data;
using DeanFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Services;

public sealed class DocumentAccessService(DeanFlowDbContext db)
{
    public IQueryable<DeanDocument> VisibleTo(int userId, string? role)
    {
        var documents = db.Documents.AsNoTracking();
        return role switch
        {
            AppRoles.Administrator or AppRoles.DeanerySpecialist or AppRoles.Management => documents,
            AppRoles.Executor => documents.Where(d => d.Executors.Any(x => x.UserId == userId)),
            // The existing schema has no separate student-owner column. Only requests created by the student are private and visible.
            AppRoles.Student => documents.Where(d => d.CreatedByUserId == userId),
            _ => documents.Where(_ => false)
        };
    }

    public async Task<DeanDocument?> FindVisibleAsync(int documentId, int userId, string? role, CancellationToken token = default)
        => await VisibleTo(userId, role).Include(d => d.DocumentType).Include(d => d.Status)
            .Include(d => d.Department).Include(d => d.CreatedBy).Include(d => d.Executors).ThenInclude(x => x.User)
            .Include(d => d.History).ThenInclude(x => x.User)
            .Include(d => d.Attachments).ThenInclude(x => x.UploadedBy)
            .Include(d => d.Comments).ThenInclude(x => x.User)
            .Include(d => d.Approvals).ThenInclude(x => x.Approver)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, token);

    public async Task<bool> CanParticipateAsync(int documentId, int userId, string? role, CancellationToken token = default)
    {
        if (role is AppRoles.Administrator or AppRoles.DeanerySpecialist or AppRoles.Management or AppRoles.Student)
            return await VisibleTo(userId, role).AnyAsync(x => x.DocumentId == documentId, token);
        return role == AppRoles.Executor && await db.DocumentExecutors.AnyAsync(x => x.DocumentId == documentId && x.UserId == userId, token);
    }
}
