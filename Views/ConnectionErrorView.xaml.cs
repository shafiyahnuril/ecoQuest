using System.Windows.Controls;

namespace EcoQuest.Views;

public partial class ConnectionErrorView : UserControl
{
    public ConnectionErrorView()
    {
        InitializeComponent();
        Loaded += (_, _) => RetryButton.Focus();
    }
}
