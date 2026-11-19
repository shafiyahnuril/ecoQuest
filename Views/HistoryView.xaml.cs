using System.Windows.Controls;
using System.Windows.Input;

namespace EcoQuest.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnDayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && sender is ListBoxItem item)
        {
            item.IsSelected = true;
            e.Handled = true;
        }
    }
}
