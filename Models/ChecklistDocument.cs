using System;
using System.Collections.Generic;

namespace DevGuard.Models;

public sealed class ChecklistDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public WorkType WorkType { get; set; }

    /// <summary>
    /// UTC timestamp from when the document was first created. Saves do not change it.
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful save. Opening a file does not change it.
    /// </summary>
    public DateTime UpdatedOn { get; set; }

    /// <summary>
    /// Set only by the Complete action. Item checkboxes do not set this.
    /// </summary>
    public bool IsCompleted { get; set; }

    public List<ChecklistItem> Items { get; set; } = [];
}
