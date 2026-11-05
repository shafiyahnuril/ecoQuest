using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only numeric field with − / + buttons. <see cref="Value"/> follows every valid keystroke without rewriting the text; the text is
/// reformatted (or restored when invalid) on focus loss and Enter. Rules are in <see cref="NumericInputParser"/>.
/// </summary>
public partial class NumericStepper : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(decimal?), typeof(NumericStepper),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty IsIntegerProperty = DependencyProperty.Register(
        nameof(IsInteger), typeof(bool), typeof(NumericStepper), new PropertyMetadata(false, OnIsIntegerChanged));

    /// <summary>Mirrors <c>Validation.HasError</c> of this control (see <see cref="InputState"/>).</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(NumericStepper), new PropertyMetadata(false));

    private bool _isSyncingText;
    private bool _isTyping;

    public NumericStepper()
    {
        InitializeComponent();

        SetBinding(IsInvalidProperty, new Binding
        {
            Path = new PropertyPath("(0)", Validation.HasErrorProperty),
            RelativeSource = RelativeSource.Self,
            Mode = BindingMode.OneWay,
        });

        Box.TextChanged += OnTextChanged;
        Box.LostKeyboardFocus += (_, _) => ShowValue();
        Box.PreviewKeyDown += OnPreviewKeyDown;
    }

    public decimal? Value
    {
        get => (decimal?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsInteger
    {
        get => (bool)GetValue(IsIntegerProperty);
        set => SetValue(IsIntegerProperty, value);
    }

    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        private set => SetValue(IsInvalidProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var stepper = (NumericStepper)d;
        if (!stepper._isTyping)
        {
            stepper.ShowValue();
        }
    }

    private static void OnIsIntegerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumericStepper)d).ShowValue();

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingText || !NumericInputParser.TryParse(Box.Text, IsInteger, out var parsed))
        {
            return;
        }

        _isTyping = true;
        try
        {
            Value = parsed;
        }
        finally
        {
            _isTyping = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                StepBy(1);
                e.Handled = true;
                break;
            case Key.Down:
                StepBy(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ShowValue();
                break;
        }
    }

    private void OnDecreaseClick(object sender, RoutedEventArgs e) => StepBy(-1);

    private void OnIncreaseClick(object sender, RoutedEventArgs e) => StepBy(1);

    private void StepBy(int direction)
    {
        Value = NumericInputParser.Step(Value, direction, IsInteger);
        ShowValue();
    }

    private void ShowValue()
    {
        _isSyncingText = true;
        try
        {
            Box.Text = NumericInputParser.Format(Value, IsInteger);
        }
        finally
        {
            _isSyncingText = false;
        }
    }
}
