using System.Windows.Controls;
using System.Windows.Input;

namespace EcoQuest.Views;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();

        // Visual-only: initial focus on the username field (UI-UX-Flow-Frontend S-02, accessibility).
        Loaded += (_, _) => Keyboard.Focus(UserNameBox);
    }
}
