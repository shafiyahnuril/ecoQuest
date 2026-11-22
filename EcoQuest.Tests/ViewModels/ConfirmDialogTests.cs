using System.Windows;
using System.Windows.Media;
using EcoQuest.Controls;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.Views.Dialogs;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>ConfirmDialog icon and one-button mode (D-01, D-02, D-04): the icon follows <see cref="DialogIcon"/>, the default stays Info.</summary>
[Collection(WpfCollection.Name)]
public class ConfirmDialogTests
{
    [Theory]
    [InlineData(DialogIcon.Info, "Icon.Info", false)]
    [InlineData(DialogIcon.Warning, "Icon.Alert", false)]
    [InlineData(DialogIcon.Leaf, "Icon.Leaf", false)]
    [InlineData(DialogIcon.Level, "Icon.Trophy", true)]
    [InlineData(DialogIcon.Badge, "Icon.Award", true)]
    public void Icon_key_and_animation_follow_the_dialog_icon(DialogIcon icon, string key, bool animates)
    {
        var vm = new ConfirmDialogViewModel("Judul", "Pesan", "Lanjut", cancelText: null, icon);

        Assert.Equal(key, vm.IconKey);
        Assert.Equal(animates, vm.AnimatesIcon);
    }

    [Fact]
    public void The_default_icon_is_info_and_cancel_exists_only_when_text_is_given()
    {
        var confirm = new ConfirmDialogViewModel("Judul", "Pesan", "Simpan", "Batal");
        var info = new ConfirmDialogViewModel("Judul", string.Empty, "Lanjut", cancelText: null);

        Assert.Equal("Icon.Info", confirm.IconKey);
        Assert.True(confirm.HasCancel);
        Assert.False(info.HasCancel);
        Assert.False(info.HasMessage);
    }

    [Theory]
    [InlineData(DialogIcon.Info, "Icon.Info")]
    [InlineData(DialogIcon.Warning, "Icon.Alert")]
    [InlineData(DialogIcon.Leaf, "Icon.Leaf")]
    [InlineData(DialogIcon.Level, "Icon.Trophy")]
    [InlineData(DialogIcon.Badge, "Icon.Award")]
    public void The_window_draws_the_icon_and_a_one_button_dialog_has_no_cancel(DialogIcon icon, string key)
    {
        var (drawn, expected, cancelVisible, confirmVisible) = WpfTestHost.Run(() =>
        {
            var dialog = new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel("Judul", "Pesan", "Lanjut", cancelText: null, icon),
                ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000, Top = -32000, Width = 800, Height = 600,
            };
            dialog.Show();
            try
            {
                dialog.UpdateLayout();
                var icons = Descendants(dialog).OfType<IconView>().ToList();
                return (icons.First().Data, (Geometry)Application.Current.FindResource(key),
                    ((UIElement)dialog.FindName("CancelButton")).IsVisible, ((UIElement)dialog.FindName("ConfirmButton")).IsVisible);
            }
            finally
            {
                dialog.Close();
            }
        });

        Assert.Same(expected, drawn);
        Assert.False(cancelVisible);
        Assert.True(confirmVisible);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
