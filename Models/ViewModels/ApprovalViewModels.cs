using System.ComponentModel.DataAnnotations;

namespace DeanFlow.Models.ViewModels;

public sealed class ApprovalAssignmentInput
{
    [Required] public int DocumentId { get; set; }
    [Required] public int ApproverUserId { get; set; }
}
