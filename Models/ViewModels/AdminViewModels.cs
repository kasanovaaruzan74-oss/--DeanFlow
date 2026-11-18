using System.ComponentModel.DataAnnotations;

namespace DeanFlow.Models.ViewModels;

public sealed class AdminUserInput
{
    public int? UserId { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(70)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(70)] public string LastName { get; set; } = string.Empty;
    [StringLength(70)] public string? MiddleName { get; set; }
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [StringLength(100)] public string? Position { get; set; }
    [Required] public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
    [MinLength(10), StringLength(128)] public string? NewPassword { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DepartmentInput
{
    public int? DepartmentId { get; set; }
    [Required, StringLength(150)] public string DepartmentName { get; set; } = string.Empty;
    [StringLength(30)] public string? ShortName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DocumentTypeInput
{
    public int? DocumentTypeId { get; set; }
    [Required, StringLength(100)] public string TypeName { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Code { get; set; } = string.Empty;
    [StringLength(255)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AdminOverviewViewModel
{
    public int Users { get; set; }
    public int ActiveUsers { get; set; }
    public int Departments { get; set; }
    public int ActiveDepartments { get; set; }
    public int DocumentTypes { get; set; }
    public int ActiveTypes { get; set; }
    public int AuditEntries { get; set; }
    public int Roles { get; set; }
}
