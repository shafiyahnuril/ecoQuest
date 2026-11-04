namespace EcoQuest.ViewModels.Items;

/// <summary>Resource keys (Icons.xaml, Colors.xaml) of the icon bubble of one activity. ActivityTypeDto carries no icon, so the mapping lives here.</summary>
public sealed record ActivityVisual(string IconKey, string BubbleBackgroundKey, string BubbleForegroundKey)
{
    public static readonly ActivityVisual Fallback = new("Icon.Leaf", "Bubble.Default.Bg", "Bubble.Default.Fg");

    private static readonly IReadOnlyDictionary<int, ActivityVisual> ByActivityTypeId = new Dictionary<int, ActivityVisual>
    {
        [1] = Of("Icon.Walk", "Green"),
        [2] = Of("Icon.Bus", "Blue"),
        [3] = Of("Icon.Bulb", "Amber"),
        [4] = Of("Icon.Recycle", "Green"),
        [5] = Of("Icon.Bottle", "Blue"),
        [6] = Of("Icon.Bag", "Green"),
    };

    public static ActivityVisual For(int activityTypeId) => ByActivityTypeId.GetValueOrDefault(activityTypeId, Fallback);

    private static ActivityVisual Of(string iconKey, string tone) => new(iconKey, $"Bubble.{tone}.Bg", $"Bubble.{tone}.Fg");
}
