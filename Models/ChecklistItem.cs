using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SuperDev.Models;

public class ChecklistSubtask
{
   public Guid Id { get; set; } = Guid.NewGuid();

   public string Title { get; set; } = string.Empty;

   public bool IsCompleted { get; set; }

   /// <summary>
   /// Set when this subtask points at another checklist. Completion follows that checklist.
   /// </summary>
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public Guid? LinkedDocumentId { get; set; }

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? LinkedFilePath { get; set; }

   [JsonIgnore]
   public bool IsLink =>
       LinkedDocumentId is not null || !string.IsNullOrWhiteSpace(LinkedFilePath);

   [JsonIgnore]
   public bool IsManual => !IsLink;

   [JsonIgnore]
   public string LinkLabel => IsCompleted ? $"✓ {Title}" : Title;
}

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

   /// <summary>
   /// Local minutes from midnight when a time was picked. Missing on older date-only deadlines.
   /// </summary>
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public int? DeadlineMinute { get; set; }

   /// <summary>
   /// Null skips a deadline notification. Zero notifies at the deadline. A positive value is minutes before it.
   /// </summary>
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public int? NotifyBeforeMinutes { get; set; }

   /// <summary>
   /// When set, this check's deadline notification is delivered without a sound.
   /// </summary>
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
   public bool DeadlineSilent { get; set; }

   public List<ChecklistSubtask> Subtasks { get; set; } = [];

   [JsonIgnore]
   public bool HasNotes =>
       !string.IsNullOrWhiteSpace(Notes);

   [JsonIgnore]
   public bool HasSubtasks =>
       Subtasks.Count > 0;

   [JsonIgnore]
   public bool HasLinks =>
       Subtasks.Any(subtask => subtask.IsLink);

   [JsonIgnore]
   public bool HasManualSubtasks =>
       Subtasks.Any(subtask => subtask.IsManual);

   [JsonIgnore]
   public IEnumerable<ChecklistSubtask> LinkedSubtasks =>
       Subtasks.Where(subtask => subtask.IsLink);

   [JsonIgnore]
   public IEnumerable<ChecklistSubtask> ManualSubtasks =>
       Subtasks.Where(subtask => subtask.IsManual);

   /// <summary>
   /// A task with subtasks can be completed only after every subtask is checked.
   /// </summary>
   [JsonIgnore]
   public bool CanComplete =>
       !Subtasks.Any(subtask => !subtask.IsCompleted);

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