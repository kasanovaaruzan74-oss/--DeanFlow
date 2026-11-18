using System.ComponentModel.DataAnnotations;

namespace DeanFlow.Models.ViewModels;

public sealed class TaskAssignmentInput
{
    [Required] public int DocumentId { get; set; }
    [Required] public int UserId { get; set; }
    [StringLength(500)] public string? TaskDescription { get; set; }
    [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
    public bool IsPrimary { get; set; }
}
