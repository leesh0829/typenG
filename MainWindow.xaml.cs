using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;

namespace TypeOverlay;

public partial class MainWindow : Window
{
    private readonly TypingEngine _engine = new();
    private readonly PassageProvider _passageProvider = new();

    private bool _isTransitioning;
    private bool _isResizeMode;
    private bool _isResultScreen;
    private Brush _baseBrush = Brushes.White;

    public MainWindow()
    {
        InitializeComponent();
        LoadNextPassage(skipAnimation: true);
        Focus();
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isTransitioning)
        {
            e.Handled = true;
            return;
        }

        if (_isResultScreen)
        {
            if (e.Key is Key.Enter)
            {
                await TransitionToNextPassageAsync();
                e.Handled = true;
            }
            return;
        }

        if (e.Key is Key.Back)
        {
            _engine.HandleBackspace();
            RenderCurrentLine();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Space or Key.Enter)
        {
            if (_engine.CanAdvanceLine())
            {
                await AdvanceLineAsync();
            }

            e.Handled = true;
            return;
        }

    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_isTransitioning || _isResultScreen)
        {
            e.Handled = true;
            return;
        }

        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        _engine.TryApplyText(e.Text[0]);
        RenderCurrentLine();
        e.Handled = true;
    }

    private async Task AdvanceLineAsync()
    {
        if (!_engine.AdvanceLine())
        {
            return;
        }

        if (_engine.IsPassageComplete)
        {
            var (cpm, wpm, acc) = _engine.CalculateResults();
            await PlayTransitionAsync(BuildResultInlines(cpm, wpm, acc));
            _isResultScreen = true;
            return;
        }

        await PlayTransitionAsync(BuildLineInlines(_engine.BuildRenderLine()));
    }

    private void LoadNextPassage(bool skipAnimation)
    {
        _engine.LoadPassage(_passageProvider.GetNextPassage());
        _isResultScreen = false;

        if (skipAnimation)
        {
            RenderCurrentLine();
        }
    }

    private async Task TransitionToNextPassageAsync()
    {
        _engine.LoadPassage(_passageProvider.GetNextPassage());
        _isResultScreen = false;
        await PlayTransitionAsync(BuildLineInlines(_engine.BuildRenderLine()));
    }

    private void RenderCurrentLine()
    {
        var inlines = BuildLineInlines(_engine.BuildRenderLine());
        CurrentLineText.Inlines.Clear();
        foreach (var inline in inlines)
        {
            CurrentLineText.Inlines.Add(inline);
        }
    }

    private List<Inline> BuildLineInlines(IReadOnlyList<RenderCharacter> items)
    {
        var result = new List<Inline>(items.Count);

        foreach (var item in items)
        {
            var run = new Run(item.Character.ToString())
            {
                Foreground = item.State switch
                {
                    LineCharState.Incorrect => Brushes.Red,
                    LineCharState.Pending => CreateForegroundWithOpacity(_baseBrush, 0.35),
                    _ => CreateForegroundWithOpacity(_baseBrush, 1.0)
                }
            };

            result.Add(run);
        }

        return result;
    }

    private List<Inline> BuildResultInlines(double cpm, double wpm, double acc)
    {
        return
        [
            new Run($"CPM {Math.Round(cpm)}") { Foreground = CreateForegroundWithOpacity(_baseBrush, 1.0) },
            new Run("   ") { Foreground = _baseBrush },
            new Run($"WPM {Math.Round(wpm)}") { Foreground = CreateForegroundWithOpacity(_baseBrush, 1.0) },
            new Run("   ") { Foreground = _baseBrush },
            new Run($"ACC {acc:F1}%") { Foreground = CreateForegroundWithOpacity(_baseBrush, 1.0) }
        ];
    }

    private static Brush CreateForegroundWithOpacity(Brush source, double opacity)
    {
        var clone = source.Clone();
        clone.Opacity = opacity;
        return clone;
    }

    private async Task PlayTransitionAsync(List<Inline> incomingInlines)
    {
        _isTransitioning = true;

        NextLineText.Inlines.Clear();
        foreach (var inline in incomingInlines)
        {
            NextLineText.Inlines.Add(inline);
        }

        CurrentTransform.Y = 0;
        NextTransform.Y = 90;
        CurrentLineText.Opacity = 1;
        NextLineText.Opacity = 0;

        var sb = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(170);

        sb.Children.Add(MakeAnim(CurrentTransform, TranslateTransform.YProperty, 0, -90, duration));
        sb.Children.Add(MakeAnim(CurrentLineText, OpacityProperty, 1, 0, duration));
        sb.Children.Add(MakeAnim(NextTransform, TranslateTransform.YProperty, 90, 0, duration));
        sb.Children.Add(MakeAnim(NextLineText, OpacityProperty, 0, 1, duration));

        var tcs = new TaskCompletionSource<bool>();
        sb.Completed += (_, _) => tcs.TrySetResult(true);
        sb.Begin();
        await tcs.Task;

        CurrentLineText.Inlines.Clear();
        foreach (var inline in incomingInlines)
        {
            CurrentLineText.Inlines.Add(CloneInline(inline));
        }

        CurrentTransform.Y = 0;
        NextTransform.Y = 90;
        CurrentLineText.Opacity = 1;
        NextLineText.Opacity = 0;
        NextLineText.Inlines.Clear();

        _isTransitioning = false;
    }

    private static Inline CloneInline(Inline inline)
    {
        if (inline is not Run run)
        {
            return new Run(string.Empty);
        }

        return new Run(run.Text)
        {
            Foreground = run.Foreground,
            FontWeight = run.FontWeight
        };
    }

    private static Timeline MakeAnim(DependencyObject target, DependencyProperty prop, double from, double to, TimeSpan duration)
    {
        var anim = new DoubleAnimation(from, to, new Duration(duration))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, new PropertyPath(prop));
        return anim;
    }

    private async void NextPassageMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isTransitioning)
        {
            return;
        }

        await TransitionToNextPassageAsync();
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResizeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _isResizeMode = !_isResizeMode;
        ResizeThumb.Visibility = _isResizeMode ? Visibility.Visible : Visibility.Collapsed;
        ResizeMenuItem.Header = _isResizeMode ? "화면 조정 끝내기" : "화면 조정 시작";
    }

    private void ForegroundMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_baseBrush == Brushes.White)
        {
            _baseBrush = Brushes.Black;
            ForegroundMenuItem.Header = "글자 색상: 검정";
        }
        else
        {
            _baseBrush = Brushes.White;
            ForegroundMenuItem.Header = "글자 색상: 흰색";
        }

        if (_isResultScreen)
        {
            var (cpm, wpm, acc) = _engine.CalculateResults();
            CurrentLineText.Inlines.Clear();
            foreach (var inline in BuildResultInlines(cpm, wpm, acc))
            {
                CurrentLineText.Inlines.Add(inline);
            }
        }
        else
        {
            RenderCurrentLine();
        }
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source is DependencyObject source)
        {
            var menu = FindVisualParent<ContextMenu>(source);
            if (menu is not null)
            {
                return;
            }
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if mouse state changes quickly.
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T matched)
            {
                return matched;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isResizeMode)
        {
            return;
        }

        Width = Math.Max(MinWidth > 0 ? MinWidth : 420, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight > 0 ? MinHeight : 120, Height + e.VerticalChange);
    }
}
