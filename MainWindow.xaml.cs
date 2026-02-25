using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace typenG;

public partial class MainWindow : Window
{
    private readonly TypingEngine _engine = new();
    private readonly PassageProvider _passageProvider = new();
    private readonly DispatcherTimer _caretTimer;

    private bool _isTransitioning;
    private bool _isResizeMode;
    private bool _isResultScreen;
    private bool _caretVisible = true;
    private readonly HangulComposer _hangulComposer = new();
    private string _compositionText = string.Empty;
    private Brush _baseBrush = Brushes.Black;

    public MainWindow()
    {
        InitializeComponent();

        _caretTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(420)
        };
        _caretTimer.Tick += (_, _) =>
        {
            _caretVisible = !_caretVisible;
            if (!_isTransitioning && !_isResultScreen)
            {
                RenderCurrentLine();
            }
        };
        _caretTimer.Start();

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
            if (e.Key is Key.Enter or Key.Space)
            {
                await TransitionToNextPassageAsync();
                e.Handled = true;
            }

            return;
        }

        if (e.Key is Key.Back)
        {
            _compositionText = string.Empty;
            _hangulComposer.Reset();
            _engine.HandleBackspace();
            RenderCurrentLine();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter)
        {
            _compositionText = string.Empty;
            CommitComposerTail();
            if (_engine.CanAdvanceLine())
            {
                await AdvanceLineAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key is Key.Space)
        {
            _compositionText = string.Empty;
            CommitComposerTail();
            if (_engine.CanAdvanceLine())
            {
                await AdvanceLineAsync();
                e.Handled = true;
                return;
            }
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

        _caretVisible = true;

        ProcessIncomingText(e.Text);
        RenderCurrentLine();
        e.Handled = true;
    }


    private void ProcessIncomingText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n')
            {
                continue;
            }

            var expectHangul = _engine.IsCurrentTargetHangul();
            if (expectHangul && HangulComposer.IsAsciiKoreanKey(ch))
            {
                var committed = _hangulComposer.ProcessKey(ch);
                ApplyCommittedText(committed);
                _compositionText = _hangulComposer.CompositionText;
                continue;
            }

            CommitComposerTail();
            _engine.TryApplyText(ch);
        }

        if (!_engine.IsCurrentTargetHangul())
        {
            _compositionText = string.Empty;
        }
    }

    private void CommitComposerTail()
    {
        var flushed = _hangulComposer.Flush();
        ApplyCommittedText(flushed);
        _compositionText = string.Empty;
    }

    private void ApplyCommittedText(string committed)
    {
        if (string.IsNullOrEmpty(committed))
        {
            return;
        }

        foreach (var c in committed)
        {
            _engine.TryApplyText(c);
        }
    }

    private async Task AdvanceLineAsync()
    {
        if (!_engine.AdvanceLine())
        {
            return;
        }

        _hangulComposer.Reset();
        _compositionText = string.Empty;
        _caretVisible = true;

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
        _hangulComposer.Reset();
        _compositionText = string.Empty;
        _caretVisible = true;

        if (skipAnimation)
        {
            RenderCurrentLine();
        }
    }

    private async Task TransitionToNextPassageAsync()
    {
        _engine.LoadPassage(_passageProvider.GetNextPassage());
        _isResultScreen = false;
        _hangulComposer.Reset();
        _compositionText = string.Empty;
        _caretVisible = true;

        if (_isTransitioning)
        {
            return;
        }

        try
        {
            await PlayTransitionAsync(BuildLineInlines(_engine.BuildRenderLine()));
        }
        catch
        {
            RenderCurrentLine();
            _isTransitioning = false;
        }
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
        var result = new List<Inline>(items.Count + 1);
        var caretIndex = Math.Min(_engine.CurrentInputLength, items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isCaretPosition = i == caretIndex;
            var runText = item.Character.ToString();

            Brush foreground;
            if (item.State == LineCharState.Incorrect)
            {
                foreground = Brushes.Red;
            }
            else if (item.State == LineCharState.Pending)
            {
                if (isCaretPosition && !string.IsNullOrEmpty(_compositionText))
                {
                    runText = _compositionText;
                    foreground = CreateForegroundWithOpacity(_baseBrush, 1.0);
                }
                else
                {
                    foreground = CreateForegroundWithOpacity(_baseBrush, 0.35);
                }
            }
            else
            {
                foreground = CreateForegroundWithOpacity(_baseBrush, 1.0);
            }

            var run = new Run(runText)
            {
                Foreground = foreground
            };

            if (isCaretPosition && _caretVisible)
            {
                result.Add(new Run("│") { Foreground = CreateForegroundWithOpacity(_baseBrush, 0.95), FontWeight = FontWeights.Thin, FontSize = 28 });
            }

            result.Add(run);
        }

        if (caretIndex >= items.Count)
        {
            result.Add(new Run(_caretVisible ? "│" : " ")
            {
                Foreground = CreateForegroundWithOpacity(_baseBrush, 0.9),
                FontWeight = FontWeights.Thin,
                FontSize = 28
            });
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

        try
        {
            NextLineText.Inlines.Clear();
            foreach (var inline in incomingInlines)
            {
                NextLineText.Inlines.Add(inline);
            }

            CurrentTransform.Y = 0;
            NextTransform.Y = 90;
            CurrentLineText.Opacity = 1;
            NextLineText.Opacity = 0;

            if (!IsLoaded)
            {
                ApplyIncomingAsCurrent(incomingInlines);
                return;
            }

            var sb = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(170);

            sb.Children.Add(MakeAnim(CurrentTransform, TranslateTransform.YProperty, 0, -90, duration));
            sb.Children.Add(MakeAnim(CurrentLineText, OpacityProperty, 1, 0, duration));
            sb.Children.Add(MakeAnim(NextTransform, TranslateTransform.YProperty, 90, 0, duration));
            sb.Children.Add(MakeAnim(NextLineText, OpacityProperty, 0, 1, duration));

            var tcs = new TaskCompletionSource<bool>();
            sb.Completed += (_, _) => tcs.TrySetResult(true);
            sb.Begin();
            await Task.WhenAny(tcs.Task, Task.Delay(280));

            ApplyIncomingAsCurrent(incomingInlines);
        }
        finally
        {
            CurrentTransform.Y = 0;
            NextTransform.Y = 90;
            CurrentLineText.Opacity = 1;
            NextLineText.Opacity = 0;
            NextLineText.Inlines.Clear();
            _isTransitioning = false;
        }
    }

    private void ApplyIncomingAsCurrent(List<Inline> incomingInlines)
    {
        CurrentLineText.Inlines.Clear();
        foreach (var inline in incomingInlines)
        {
            CurrentLineText.Inlines.Add(CloneInline(inline));
        }
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
            FontWeight = run.FontWeight,
            TextDecorations = run.TextDecorations
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
        ResizeMode = _isResizeMode ? ResizeMode.CanResize : ResizeMode.NoResize;
        ResizeThumb.Visibility = _isResizeMode ? Visibility.Visible : Visibility.Collapsed;
        ResizeMenuItem.Header = _isResizeMode ? "위치/크기 조정 끝내기" : "위치/크기 조정 시작";
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
        if (e.Source is not DependencyObject source)
        {
            return;
        }

        if (FindVisualParent<ContextMenu>(source) is not null)
        {
            return;
        }

        if (FindVisualParent<Thumb>(source) is not null || source is Thumb)
        {
            return;
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
        DependencyObject? parent = GetParentObject(child);
        while (parent is not null)
        {
            if (parent is T matched)
            {
                return matched;
            }

            parent = GetParentObject(parent);
        }

        return null;
    }

    private static DependencyObject? GetParentObject(DependencyObject child)
    {
        return child switch
        {
            null => null,
            Visual or Visual3D => VisualTreeHelper.GetParent(child),
            FrameworkContentElement fce => fce.Parent,
            ContentElement ce => ContentOperations.GetParent(ce) ?? (ce as FrameworkContentElement)?.Parent,
            _ => null
        };
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
