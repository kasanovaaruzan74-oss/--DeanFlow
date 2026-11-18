using DeanFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace DeanFlow.Data;

public sealed class DeanFlowDbContext(DbContextOptions<DeanFlowDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentStatus> DocumentStatuses => Set<DocumentStatus>();
    public DbSet<DeanDocument> Documents => Set<DeanDocument>();
    public DbSet<IncomingDocument> IncomingDocuments => Set<IncomingDocument>();
    public DbSet<OutgoingDocument> OutgoingDocuments => Set<OutgoingDocument>();
    public DbSet<ExecutorAssignment> DocumentExecutors => Set<ExecutorAssignment>();
    public DbSet<DocumentHistoryEntry> DocumentHistory => Set<DocumentHistoryEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<DocumentComment> Comments => Set<DocumentComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        static void Text<T>(EntityTypeBuilder<T> entity, Expression<Func<T, string?>> property, int length) where T : class
            => entity.Property(property).IsUnicode().HasMaxLength(length);
        static void TextMax<T>(EntityTypeBuilder<T> entity, Expression<Func<T, string?>> property) where T : class
            => entity.Property(property).IsUnicode().HasColumnType("nvarchar(max)");

        model.Entity<Role>().ToTable("Roles").HasKey(x => x.RoleId);
        model.Entity<Department>().ToTable("Departments").HasKey(x => x.DepartmentId);
        model.Entity<AppUser>().ToTable("Users").HasKey(x => x.UserId);
        model.Entity<DocumentType>().ToTable("DocumentTypes").HasKey(x => x.DocumentTypeId);
        model.Entity<DocumentStatus>().ToTable("DocumentStatuses").HasKey(x => x.StatusId);
        model.Entity<DeanDocument>().ToTable("Documents").HasKey(x => x.DocumentId);
        model.Entity<IncomingDocument>().ToTable("IncomingDocuments").HasKey(x => x.IncomingDocumentId);
        model.Entity<OutgoingDocument>().ToTable("OutgoingDocuments").HasKey(x => x.OutgoingDocumentId);
        model.Entity<ExecutorAssignment>().ToTable("DocumentExecutors").HasKey(x => x.DocumentExecutorId);
        model.Entity<DocumentHistoryEntry>().ToTable("DocumentHistory").HasKey(x => x.HistoryId);
        model.Entity<Attachment>().ToTable("Attachments").HasKey(x => x.AttachmentId);
        model.Entity<DocumentComment>().ToTable("Comments").HasKey(x => x.CommentId);
        model.Entity<Notification>().ToTable("Notifications").HasKey(x => x.NotificationId);
        model.Entity<DocumentApproval>().ToTable("DocumentApprovals").HasKey(x => x.ApprovalId);
        model.Entity<AuditLog>().ToTable("AuditLogs").HasKey(x => x.AuditLogId);

        model.Entity<DeanDocument>().Property(x => x.DocumentDate).HasColumnType("date");
        model.Entity<IncomingDocument>().Property(x => x.ExternalDate).HasColumnType("date");
        model.Entity<Attachment>().Property(x => x.FileSize).HasColumnType("bigint");
        model.Entity<AuditLog>().Property(x => x.AuditLogId).HasColumnType("bigint").ValueGeneratedOnAdd();

        Text(model.Entity<Role>(), x => x.RoleName, 50);
        Text(model.Entity<Role>(), x => x.Description, 255);
        Text(model.Entity<Department>(), x => x.DepartmentName, 150);
        Text(model.Entity<Department>(), x => x.ShortName, 30);
        Text(model.Entity<AppUser>(), x => x.FirstName, 70);
        Text(model.Entity<AppUser>(), x => x.LastName, 70);
        Text(model.Entity<AppUser>(), x => x.MiddleName, 70);
        Text(model.Entity<AppUser>(), x => x.Email, 150);
        Text(model.Entity<AppUser>(), x => x.PasswordHash, 500);
        Text(model.Entity<AppUser>(), x => x.Phone, 30);
        Text(model.Entity<AppUser>(), x => x.Position, 100);
        Text(model.Entity<AppUser>(), x => x.ProfileImage, 500);
        Text(model.Entity<DocumentType>(), x => x.TypeName, 100);
        Text(model.Entity<DocumentType>(), x => x.Code, 20);
        Text(model.Entity<DocumentType>(), x => x.Description, 255);
        Text(model.Entity<DocumentStatus>(), x => x.StatusName, 70);
        Text(model.Entity<DocumentStatus>(), x => x.StatusCode, 30);
        Text(model.Entity<DocumentStatus>(), x => x.Description, 255);
        Text(model.Entity<DeanDocument>(), x => x.RegistrationNumber, 50);
        Text(model.Entity<DeanDocument>(), x => x.Title, 250);
        TextMax(model.Entity<DeanDocument>(), x => x.Description);
        Text(model.Entity<DeanDocument>(), x => x.Direction, 20);
        Text(model.Entity<DeanDocument>(), x => x.SenderName, 200);
        Text(model.Entity<DeanDocument>(), x => x.RecipientName, 200);
        Text(model.Entity<DeanDocument>(), x => x.Priority, 20);
        Text(model.Entity<IncomingDocument>(), x => x.ExternalNumber, 100);
        Text(model.Entity<IncomingDocument>(), x => x.SenderOrganization, 200);
        Text(model.Entity<IncomingDocument>(), x => x.ReceivedMethod, 50);
        Text(model.Entity<OutgoingDocument>(), x => x.RecipientOrganization, 200);
        Text(model.Entity<OutgoingDocument>(), x => x.DeliveryMethod, 50);
        Text(model.Entity<ExecutorAssignment>(), x => x.TaskDescription, 500);
        Text(model.Entity<DocumentHistoryEntry>(), x => x.ActionType, 100);
        Text(model.Entity<DocumentHistoryEntry>(), x => x.Description, 1000);
        Text(model.Entity<Attachment>(), x => x.FileName, 255);
        Text(model.Entity<Attachment>(), x => x.StoredFileName, 255);
        Text(model.Entity<Attachment>(), x => x.FilePath, 500);
        Text(model.Entity<Attachment>(), x => x.FileType, 100);
        Text(model.Entity<DocumentComment>(), x => x.CommentText, 1500);
        Text(model.Entity<Notification>(), x => x.Title, 200);
        Text(model.Entity<Notification>(), x => x.Message, 1000);
        Text(model.Entity<Notification>(), x => x.NotificationType, 50);
        Text(model.Entity<DocumentApproval>(), x => x.ApprovalStatus, 30);
        Text(model.Entity<DocumentApproval>(), x => x.Comment, 1000);
        Text(model.Entity<AuditLog>(), x => x.Action, 100);
        Text(model.Entity<AuditLog>(), x => x.EntityName, 100);
        Text(model.Entity<AuditLog>(), x => x.Details, 1500);
        Text(model.Entity<AuditLog>(), x => x.IpAddress, 50);

        model.Entity<AppUser>().HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<AppUser>().HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DeanDocument>().HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DeanDocument>().HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DeanDocument>().HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DeanDocument>().HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<IncomingDocument>().HasOne(x => x.Document).WithOne(x => x.Incoming).HasForeignKey<IncomingDocument>(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<OutgoingDocument>().HasOne(x => x.Document).WithOne(x => x.Outgoing).HasForeignKey<OutgoingDocument>(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<ExecutorAssignment>().HasOne(x => x.Document).WithMany(x => x.Executors).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<ExecutorAssignment>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<ExecutorAssignment>().HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentHistoryEntry>().HasOne(x => x.Document).WithMany(x => x.History).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentHistoryEntry>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentHistoryEntry>().HasOne<DocumentStatus>().WithMany().HasForeignKey(x => x.OldStatusId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentHistoryEntry>().HasOne<DocumentStatus>().WithMany().HasForeignKey(x => x.NewStatusId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<Attachment>().HasOne(x => x.Document).WithMany(x => x.Attachments).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<Attachment>().HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentComment>().HasOne(x => x.Document).WithMany(x => x.Comments).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentComment>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<Notification>().HasOne(x => x.Document).WithMany(x => x.Notifications).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentApproval>().HasOne(x => x.Document).WithMany(x => x.Approvals).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DocumentApproval>().HasOne(x => x.Approver).WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<AuditLog>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);

        model.Entity<AppUser>().HasIndex(x => x.Email);
        model.Entity<DeanDocument>().HasIndex(x => x.RegistrationNumber);
        model.Entity<DeanDocument>().HasIndex(x => new { x.IsArchived, x.RegisteredAt });
        model.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
    }
}
