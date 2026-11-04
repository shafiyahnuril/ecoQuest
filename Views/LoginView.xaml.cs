using System.Windows.Controls;
using System.Windows.Input;

namespace EcoQuest.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();

        // Visual-only: initial focus on the identifier field (UI-UX-Flow-Frontend S-01, accessibility).
        Loaded += (_, _) => Keyboard.Focus(IdentifierBox);
    }
}
