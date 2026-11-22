using System.Windows;

namespace EcoQuest.Controls.Charts;

/// <summary>Chart animation speed that honours the Windows "animation effects" setting (UI-UX-Flow-Frontend 6).</summary>
public static class ChartMotion
{
    private static readonly TimeSpan DefaultSpeed = TimeSpan.FromMilliseconds(500);

    public static TimeSpan Speed { get; } = SystemParameters.ClientAreaAnimation ? DefaultSpeed : TimeSpan.Zero;
}
