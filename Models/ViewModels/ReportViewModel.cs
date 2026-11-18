namespace DeanFlow.Models.ViewModels;

public sealed class ReportViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Total { get; set; }
    public int Incoming { get; set; }
    public int Outgoing { get; set; }
    public int Completed { get; set; }
    public int Overdue { get; set; }
    public double? AverageCompletionDays { get; set; }
    public IReadOnlyList<(string Label, int Count)> ByStatus { get; set; } = [];
    public IReadOnlyList<(string Label, int Count)> ByType { get; set; } = [];
    public IReadOnlyList<(string Label, int Count)> ByDepartment { get; set; } = [];
    public IReadOnlyList<(string Label, int Count)> ByExecutor { get; set; } = [];
    public IReadOnlyList<(string Label, int Count)> ByMonth { get; set; } = [];
}
