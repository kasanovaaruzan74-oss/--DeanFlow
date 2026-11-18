using System.ComponentModel.DataAnnotations;
using DeanFlow.Data.Entities;

namespace DeanFlow.Models.ViewModels;

public sealed class DashboardViewModel
{
    public bool DatabaseAvailable { get; set; } = true;
    public string? DatabaseMessage { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string GreetingName { get; set; } = string.Empty;
    public int TotalDocuments { get; set; }
    public int TodayRegistered { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int PendingApprovals { get; set; }
    public int Overdue { get; set; }
    public int MyTasks { get; set; }
    public DateTime CalendarMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime CalendarGridStart { get; set; }
    public List<DeanDocument> RecentDocuments { get; set; } = [];
    public List<DocumentHistoryEntry> RecentActivity { get; set; } = [];
    public List<ExecutorAssignment> UpcomingTasks { get; set; } = [];
    public List<Notification> RecentNotifications { get; set; } = [];
    public List<DashboardCalendarEvent> CalendarEvents { get; set; } = [];
    public List<(string Label, int Count)> StatusSummary { get; set; } = [];
    public List<(string Label, int Count)> MonthlySummary { get; set; } = [];
}

public sealed class DashboardCalendarEvent
{
    public DateTime DueDate { get; set; }
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string Kind { get; set; } = "document";
    public bool IsCompleted { get; set; }
}

public sealed class DocumentListViewModel
{
    public IReadOnlyList<DeanDocument> Documents { get; set; } = [];
    public string? Query { get; set; }
    public string? Direction { get; set; }
    public string? StatusCode { get; set; }
    public string? Priority { get; set; }
    public int? DepartmentId { get; set; }
    public int? DocumentTypeId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public IReadOnlyList<DocumentType> Types { get; set; } = [];
    public IReadOnlyList<DocumentStatus> Statuses { get; set; } = [];
    public IReadOnlyList<Department> Departments { get; set; } = [];
}

public sealed class DocumentFormInput
{
    public int? DocumentId { get; set; }
    [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
    [StringLength(3000)] public string? Description { get; set; }
    [Required] public int DocumentTypeId { get; set; }
    [Required] public string Direction { get; set; } = "Кіріс";
    [StringLength(200)] public string? SenderName { get; set; }
    [StringLength(200)] public string? RecipientName { get; set; }
    public int? DepartmentId { get; set; }
    [Required] public string Priority { get; set; } = "Қалыпты";
    [DataType(DataType.Date)] public DateTime DocumentDate { get; set; } = DateTime.Today;
    [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
    [StringLength(100)] public string? ExternalNumber { get; set; }
    [DataType(DataType.Date)] public DateTime? ExternalDate { get; set; }
    [StringLength(200)] public string? SenderOrganization { get; set; }
    [StringLength(200)] public string? RecipientOrganization { get; set; }
    [StringLength(50)] public string? DeliveryMethod { get; set; }
    [StringLength(50)] public string? ReceivedMethod { get; set; }
}
