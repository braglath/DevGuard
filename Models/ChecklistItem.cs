using System;

namespace DevGuard.Models;

public class ChecklistItem
{
   public Guid Id { get; set; } = Guid.NewGuid();

   public string Title { get; set; } = string.Empty;

   public string Category { get; set; } = string.Empty;

   public ChecklistPriority Priority { get; set; } =
       ChecklistPriority.Required;

   public string Notes { get; set; } = string.Empty;

   public bool HasNotes =>
       !string.IsNullOrWhiteSpace(Notes);

   public bool IsCompleted { get; set; }
}

public enum ChecklistPriority
{
   Critical,
   Required,
   Conditional,
   Review
}