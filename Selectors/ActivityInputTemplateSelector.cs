using System.Windows;
using System.Windows.Controls;
using EcoQuest.Core.Domain;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.Selectors;

public sealed class ActivityInputTemplateSelector : DataTemplateSelector
{
    public DataTemplate? NumericTemplate { get; set; }

    public DataTemplate? CountTemplate { get; set; }

    public DataTemplate? TimeRangeTemplate { get; set; }

    public DataTemplate? ToggleTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        (item as ActivityInputItemViewModel)?.InputControlType switch
        {
            InputControlType.Numeric => NumericTemplate,
            InputControlType.Count => CountTemplate,
            InputControlType.TimeRange => TimeRangeTemplate,
            InputControlType.Toggle => ToggleTemplate,
            _ => base.SelectTemplate(item, container),
        };
}
