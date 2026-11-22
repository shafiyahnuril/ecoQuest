using System.Windows.Controls;

namespace EcoQuest.Views;

public partial class OnboardingView : UserControl
{
    public OnboardingView()
    {
        InitializeComponent();
        // Visual only: the first focus lands on the primary button so Enter continues.
        Loaded += (_, _) => NextButton.Focus();
    }
}
