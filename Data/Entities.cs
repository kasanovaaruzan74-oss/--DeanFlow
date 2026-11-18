namespace DeanFlow.Data.Entities;

public sealed class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

public sealed class Department
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class AppUser
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public string? ProfileImage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Role? Role { get; set; }
    public Department? Department { get; set; }

    public string FullName => string.Join(' ', new[] { LastName, FirstName, MiddleName }
        .Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed class DocumentType
{
    public int DocumentTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DocumentStatus
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class DeanDocument
{
    public int DocumentId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int DocumentTypeId { get; set; }
    public int StatusId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string? RecipientName { get; set; }
    public int? DepartmentId { get; set; }
    public int CreatedByUserId { get; set; }
    public string Priority { get; set; } = "Қалыпты";
    public DateOnly DocumentDate { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public DocumentType? DocumentType { get; set; }
    public DocumentStatus? Status { get; set; }
    public Department? Department { get; set; }
    public AppUser? CreatedBy { get; set; }
    public IncomingDocument? Incoming { get; set; }
    public OutgoingDocument? Outgoing { get; set; }
    public ICollection<ExecutorAssignment> Executors { get; set; } = new List<ExecutorAssignment>();
    public ICollection<DocumentHistoryEntry> History { get; set; } = new List<DocumentHistoryEntry>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<DocumentApproval> Approvals { get; set; } = new List<DocumentApproval>();
}

public sealed class IncomingDocument
{
    public int IncomingDocumentId { get; set; }
    public int DocumentId { get; set; }
    public string? ExternalNumber { get; set; }
    public DateOnly? ExternalDate { get; set; }
    public string? SenderOrganization { get; set; }
    public string? ReceivedMethod { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DeanDocument? Document { get; set; }
}

public sealed class OutgoingDocument
{
    public int OutgoingDocumentId { get; set; }
    public int DocumentId { get; set; }
    public string? RecipientOrganization { get; set; }
    public string? DeliveryMethod { get; set; }
    public DateTime? SentAt { get; set; }
    public DeanDocument? Document { get; set; }
}

public sealed class ExecutorAssignment
{
    public int DocumentExecutorId { get; set; }
    public int DocumentId { get; set; }
    public int UserId { get; set; }
    public int AssignedByUserId { get; set; }
    public string? TaskDescription { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsPrimary { get; set; }
    public DeanDocument? Document { get; set; }
    public AppUser? User { get; set; }
    public AppUser? AssignedBy { get; set; }
}

public sealed class DocumentHistoryEntry
{
    public int HistoryId { get; set; }
    public int DocumentId { get; set; }
    public int? UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int? OldStatusId { get; set; }
    public int? NewStatusId { get; set; }
    public string? Description { get; set; }
    public DateTime ActionDate { get; set; }
    public DeanDocument? Document { get; set; }
    public AppUser? User { get; set; }
}

public sealed class Attachment
{
    public int AttachmentId { get; set; }
    public int DocumentId { get; set; }
    public int UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? StoredFileName { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public DeanDocument? Document { get; set; }
    public AppUser? UploadedBy { get; set; }
}

public sealed class DocumentComment
{
    public int CommentId { get; set; }
    public int DocumentId { get; set; }
    public int UserId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public DeanDocument? Document { get; set; }
    public AppUser? User { get; set; }
}

public sealed class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int? DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NotificationType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public AppUser? User { get; set; }
    public DeanDocument? Document { get; set; }
}

public sealed class DocumentApproval
{
    public int ApprovalId { get; set; }
    public int DocumentId { get; set; }
    public int ApproverUserId { get; set; }
    public int ApprovalOrder { get; set; }
    public string ApprovalStatus { get; set; } = "Күтуде";
    public string? Comment { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? DecisionAt { get; set; }
    public DeanDocument? Document { get; set; }
    public AppUser? Approver { get; set; }
}

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public AppUser? User { get; set; }
}
