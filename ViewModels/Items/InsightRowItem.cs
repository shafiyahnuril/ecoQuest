using EcoQuest.Core.Domain;

namespace EcoQuest.ViewModels.Items;

/// <summary>One row of the dashboard insight card. InsightDto carries no title or icon, so both are derived from its kind (resource keys only).</summary>
public sealed record InsightRowItem(string Title, string Body, string IconKey, string BubbleBackgroundKey, string BubbleForegroundKey)
{
    public static InsightRowItem For(InsightKind kind, string title, string body) => kind switch
    {
        InsightKind.WeakestCategory => Of(title, body, "Icon.Bulb", "Amber"),
        InsightKind.WeekOverWeek => Of(title, body, "Icon.Calendar", "Blue"),
        InsightKind.Streak => Of(title, body, "Icon.Flame", "Orange"),
        _ => Of(title, body, "Icon.Leaf", "Green"),
    };

    public static InsightRowItem Tip(string title, string body) => Of(title, body, "Icon.Bulb", "Amber");

    private static InsightRowItem Of(string title, string body, string iconKey, string tone) =>
        new(title, body, iconKey, $"Bubble.{tone}.Bg", $"Bubble.{tone}.Fg");
}
