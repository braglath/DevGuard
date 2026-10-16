using Avalonia.Media;
using SuperDev.Models;

namespace SuperDev.Views;

internal static class PriorityAppearance
{
    public static readonly IBrush Neutral = Brush("#F8FAFC");
    public static readonly IBrush Critical = Brush("#F87171");
    public static readonly IBrush Required = Brush("#FBBF24");
    public static readonly IBrush Conditional = Brush("#38BDF8");
    public static readonly IBrush Review = Brush("#C4B5FD");

    public static string ClassName(ChecklistPriority priority) => priority switch
    {
        ChecklistPriority.Critical => "critical",
        ChecklistPriority.Required => "required",
        ChecklistPriority.Conditional => "conditional",
        _ => "review"
    };

    public static IBrush BrushFor(ChecklistPriority priority) => priority switch
    {
        ChecklistPriority.Critical => Critical,
        ChecklistPriority.Required => Required,
        ChecklistPriority.Conditional => Conditional,
        _ => Review
    };

    private static SolidColorBrush Brush(string hex) => SolidColorBrush.Parse(hex);
}
