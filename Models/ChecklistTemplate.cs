using System.Collections.Generic;
using System.Linq;

namespace SuperDev.Models;

public sealed class ChecklistTemplate
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Icon { get; set; } = "📋";

    public WorkType WorkType { get; set; }

    public List<TemplateCheck> Items { get; set; } = [];

    public bool CanEdit => Id is not ("blank" or "daily");

    public ChecklistTemplate Copy() => new()
    {
        Id = Id,
        Title = Title,
        Icon = Icon,
        WorkType = WorkType,
        Items = Items.Select(item => item.Copy()).ToList()
    };
}

public sealed class TemplateCheck
{
    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public ChecklistPriority Priority { get; set; } = ChecklistPriority.Required;

    public TemplateCheck Copy() => new()
    {
        Title = Title,
        Category = Category,
        Priority = Priority
    };
}
