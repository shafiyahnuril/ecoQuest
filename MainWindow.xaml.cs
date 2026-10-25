using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ecoQuest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Trigger only (UIR-56): the view model decides whether and how to refresh.
            Activated += (_, _) => (DataContext as EcoQuest.ViewModels.ShellViewModel)?.RefreshPendingCommand.Execute(null);
        }
    }
}