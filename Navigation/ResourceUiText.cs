using System.Windows;

namespace EcoQuest.Navigation;

/// <summary><see cref="IUiText"/> backed by the merged Resources/Strings.id.xaml dictionary.</summary>
public sealed class ResourceUiText : IUiText
{
    public string Get(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
