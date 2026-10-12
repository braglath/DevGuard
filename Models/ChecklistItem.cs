using System;
using System.Text.Json.Serialization;

namespace SuperDev.Models;

public class ChecklistItem
{
   public Guid Id { get; set; } = Guid.NewGuid();

   public string Title { get; set; } = string.Empty;

   public string Category { get; set; } = string.Empty;

   public ChecklistPriority Priority { get; set; } =
       ChecklistPriority.Required;

   public string Notes { get; set; } = string.Empty;

   /// <summary>
   /// Optional calendar deadline. Missing on older files, and left unset until the user picks one.
   /// </summary>
   public DateOnly? Deadline { get; set; }

   [JsonIgnore]
   public bool HasNotes =>
       !string.IsNullOrWhiteSpace(Notes);

   [JsonIgnore]
   public string Detail =>
       string.IsNullOrWhiteSpace(Category)
           ? Priority.ToString()
           : $"{Category} · {Priority}";

   public bool IsCompleted { get; set; }
}

public enum ChecklistPriority
{
   Critical,
   Required,
   Conditional,
   Review
}