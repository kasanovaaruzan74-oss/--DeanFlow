using DeanFlow.Data;
using DeanFlow.Data.Entities;

namespace DeanFlow.Services;

public sealed class AuditService(DeanFlowDbContext db, IHttpContextAccessor accessor)
{
    public async Task WriteAsync(string action, string? entityName = null, int? entityId = null, string? details = null, int? userId = null)
    {
        var context = accessor.HttpContext;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? (int.TryParse(context?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
    }
}
