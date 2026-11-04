using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EcoQuest.Controls;

/// <summary>
/// Visual-only start/end time fields (5-minute slots, typed). A range may cross midnight (EndTime before StartTime); the service decides
/// whether it is acceptable. Rules are in <see cref="TimeInputParser"/>.
/// </summary>
public partial class TimeRangePicker : UserControl
{
    public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register(
        nameof(StartTime), typeof(TimeOnly?), typeof(TimeRangePicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register(
        nameof(EndTime), typeof(TimeOnly?), typeof(TimeRangePicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    /// <summary>Mirrors <c>Validation.HasError</c> of this control (see <see cref="InputState"/>).</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(TimeRangePicker), new PropertyMetadata(false));

    public static readonly DependencyProperty HasValueProperty = DependencyProperty.Register(
        nameof(HasValue), typeof(bool), typeof(TimeRangePicker), new PropertyMetadata(false));

    private readonly TimeBoxSync _start;
    private readonly TimeBoxSync _end;

    public TimeRangePicker()
    {
        InitializeComponent();

        SetBinding(IsInvalidProperty, new Binding
        {
            Path = new PropertyPath("(0)", Validation.HasErrorProperty),
            RelativeSource = RelativeSource.Self,
            Mode = BindingMode.OneWay,
        });

        _start = new TimeBoxSync(StartBox, () => StartTime, time => StartTime = time);
        _end = new TimeBoxSync(EndBox, () => EndTime, time => EndTime = time);
    }

    public TimeOnly? StartTime
    {
        get => (TimeOnly?)GetValue(StartTimeProperty);
        set => SetValue(StartTimeProperty, value);
    }

    public TimeOnly? EndTime
    {
        get => (TimeOnly?)GetValue(EndTimeProperty);
        set => SetValue(EndTimeProperty, value);
    }

    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        private set => SetValue(IsInvalidProperty, value);
    }

    public bool HasValue
    {
        get => (bool)GetValue(HasValueProperty);
        private set => SetValue(HasValueProperty, value);
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimeRangePicker)d;
        picker.HasValue = picker.StartTime is not null || picker.EndTime is not null;
        picker._start?.OnSourceChanged();
        picker._end?.OnSourceChanged();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        StartTime = null;
        EndTime = null;
    }

    private sealed class TimeBoxSync
    {
        private readonly TextBox _box;
        private readonly Func<TimeOnly?> _read;
        private readonly Action<TimeOnly?> _write;
        private bool _isSyncingText;
        private bool _isTyping;

        public TimeBoxSync(TextBox box, Func<TimeOnly?> read, Action<TimeOnly?> write)
        {
            _box = box;
            _read = read;
            _write = write;
            box.TextChanged += OnTextChanged;
            box.LostKeyboardFocus += (_, _) => Show();
            box.PreviewKeyDown += OnPreviewKeyDown;
        }

        public void OnSourceChanged()
        {
            if (!_isTyping)
            {
                Show();
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText || !TimeInputParser.TryParse(_box.Text, out var parsed))
            {
                return;
            }

            _isTyping = true;
            try
            {
                _write(parsed);
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
                    Show();
                    break;
            }
        }

        private void StepBy(int direction)
        {
            _write(TimeInputParser.Step(_read(), direction));
            Show();
        }

        private void Show()
        {
            _isSyncingText = true;
            try
            {
                _box.Text = TimeInputParser.Format(_read());
            }
            finally
            {
                _isSyncingText = false;
            }
        }
    }
}
