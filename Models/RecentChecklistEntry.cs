using System;

namespace DevGuard.Models;

public sealed class RecentChecklistEntry
{
    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public WorkType WorkType { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime LastUsedOn { get; set; }
}
