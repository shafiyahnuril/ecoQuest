using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace EcoQuest.Tests.TestDoubles;

/// <summary>
/// One WPF <see cref="Application"/> and one STA UI thread for the whole test process (WPF allows a single Application per AppDomain, bound to
/// the thread that created it). Every layout test runs its code on that thread through <see cref="Run{T}"/>, so test classes can share it.
/// Needs a desktop session (no window is shown).
/// </summary>
public static class WpfTestHost
{
    private static readonly string[] ThemeFiles =
    [
        "Resources/Theme/Colors.xaml", "Resources/Theme/Typography.xaml", "Resources/Theme/Spacing.xaml",
        "Resources/Theme/Controls.xaml", "Resources/Icons/Icons.xaml", "Resources/Strings.id.xaml",
    ];

    private static readonly Lazy<Dispatcher> Ui = new(Start);

    /// <summary>Runs <paramref name="action"/> on the shared UI thread and returns its result (exceptions are rethrown to the caller).</summary>
    public static T Run<T>(Func<T> action)
    {
        Exception? error = null;
        T result = default!;
        Ui.Value.Invoke(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        if (error is not null)
        {
            throw new InvalidOperationException("WPF test run failed", error);
        }

        return result;
    }

    /// <summary>Processes pending binding/layout work (ErrorsChanged notifications, Loaded) at background priority.</summary>
    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var file in ThemeFiles)
                {
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/EcoQuest;component/{file}") });
                }

                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                ready.Set();
            }

            if (error is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "WPF test UI thread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        return error is null ? dispatcher! : throw new InvalidOperationException("Could not start the WPF test host", error);
    }
}

/// <summary>All tests that touch the shared WPF host run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class WpfCollection
{
    public const string Name = "Wpf";
}
