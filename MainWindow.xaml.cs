using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "typenG.log");

    private bool IsLongPassage => _engine.TotalLineCount > 2;

    private void LogState(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        Debug.WriteLine(line);
        Console.WriteLine(line);

        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch
        {
            // ignore logging failures
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        LogState("MainWindow initialized");
        LogState($"Log file path: {_logPath}");

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
            if (!_hangulComposer.HandleBackspace())
            {
                _engine.HandleBackspace();
            }

            _compositionText = _hangulComposer.CompositionText;
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

    private async void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
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

        // 측정 시작 기준: 문장이 표시된 시점이 아니라, 사용자의 첫 실제 입력 시점
        _engine.EnsureTimingStarted();
        foreach (var ch in e.Text)
        {
            if (ch is '\r' or '\n')
            {
                continue;
            }

            _engine.RegisterKeystroke();
        }

        if ((e.Text == " " || e.Text == "\r" || e.Text == "\n") && _engine.CanAdvanceLine())
        {
            _compositionText = string.Empty;
            CommitComposerTail();
            await AdvanceLineAsync();
            e.Handled = true;
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
        LogState($"AdvanceLineAsync: request canAdvance={_engine.CanAdvanceLine()} lineIndex={_engine.CurrentLineIndex}");
        if (!_engine.AdvanceLine())
        {
            LogState("AdvanceLineAsync: rejected by engine");
            return;
        }

        LogState($"AdvanceLineAsync: advanced to lineIndex={_engine.CurrentLineIndex} isComplete={_engine.IsPassageComplete}");
        _hangulComposer.Reset();
        _compositionText = string.Empty;
        _caretVisible = true;

        if (_engine.IsPassageComplete)
        {
            var (cpm, wpm, acc) = _engine.CalculateResults();
            await PlayTransitionAsync(BuildResultInlines(cpm, wpm, acc));
            _isResultScreen = true;
            RenderResultLine();
            return;
        }

        await PlayTransitionAsync(BuildLineInlines(_engine.BuildRenderLine()));
        RenderCurrentLine();
    }

    private void LoadNextPassage(bool skipAnimation)
    {
        _engine.LoadPassage(_passageProvider.GetNextPassage());
        LogState($"TransitionToNextPassageAsync: loaded lineLen={_engine.CurrentLine.Length} lineIndex={_engine.CurrentLineIndex}");
        LogState($"LoadNextPassage: lineLen={_engine.CurrentLine.Length} lineIndex={_engine.CurrentLineIndex}");
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
            LogState("TransitionToNextPassageAsync: animation completed");
            RenderCurrentLine();
            LogState($"TransitionToNextPassageAsync: rendered lineLen={_engine.CurrentLine.Length} inlineCount={CurrentLineText.Inlines.Count}");
            Focus();
        }
        catch
        {
            RenderCurrentLine();
            LogState($"TransitionToNextPassageAsync: rendered lineLen={_engine.CurrentLine.Length} inlineCount={CurrentLineText.Inlines.Count}");
            Focus();
            _isTransitioning = false;
        }
    }

    private void RenderResultLine()
    {
        var (cpm, wpm, acc) = _engine.CalculateResults();
        var resultText = $"CPM {Math.Round(cpm)}   WPM {Math.Round(wpm)}   ACC {acc:F1}%";
        AdjustFontSizeToFit(resultText);
        CurrentLineText.Inlines.Clear();
        foreach (var inline in BuildResultInlines(cpm, wpm, acc))
        {
            CurrentLineText.Inlines.Add(inline);
        }

        ProgressText.Visibility = Visibility.Collapsed;
        UpcomingLineText.Visibility = Visibility.Collapsed;
    }

    private void RenderCurrentLine()
    {
        AdjustFontSizeToFit(_engine.CurrentLine);
        RenderPassageMeta();
        var inlines = BuildLineInlines(_engine.BuildRenderLine());
        LogState($"RenderCurrentLine: lineIndex={_engine.CurrentLineIndex} textLen={_engine.CurrentLine.Length} inlineCount={inlines.Count}");
        CurrentLineText.Inlines.Clear();
        foreach (var inline in inlines)
        {
            CurrentLineText.Inlines.Add(inline);
        }
    }

    private void RenderPassageMeta()
    {
        if (!IsLongPassage || _isResultScreen)
        {
            ProgressText.Visibility = Visibility.Collapsed;
            UpcomingLineText.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressText.Text = $"{Math.Min(_engine.CurrentLineIndex + 1, _engine.TotalLineCount)}/{_engine.TotalLineCount}";
        ProgressText.Foreground = CreateForegroundWithOpacity(_baseBrush, 0.85);
        ProgressText.Visibility = Visibility.Visible;

        var nextLine = _engine.NextLine;
        if (string.IsNullOrWhiteSpace(nextLine))
        {
            UpcomingLineText.Visibility = Visibility.Collapsed;
            return;
        }

        UpcomingLineText.Text = nextLine;
        UpcomingLineText.Foreground = CreateForegroundWithOpacity(_baseBrush, 0.55);
        UpcomingLineText.Visibility = Visibility.Visible;
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
                    foreground = CreateForegroundWithOpacity(_baseBrush, PendingOpacity());
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


    private double PendingOpacity()
    {
        return _baseBrush == Brushes.Black ? 0.6 : 0.35;
    }

    private void AdjustFontSizeToFit(string text)
    {
        const double maxFont = 42;
        const double minFont = 10;

        var width = Math.Max(80, ActualWidth - 90);
        var reservedHeight = IsLongPassage ? 120 : 70;
        var height = Math.Max(28, ActualHeight - reservedHeight);
        if (string.IsNullOrEmpty(text))
        {
            CurrentLineText.FontSize = maxFont;
            NextLineText.FontSize = maxFont;
            UpcomingLineText.FontSize = 24;
            return;
        }

        var typeface = new Typeface(CurrentLineText.FontFamily, CurrentLineText.FontStyle, CurrentLineText.FontWeight, CurrentLineText.FontStretch);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var chosen = minFont;
        for (var size = maxFont; size >= minFont; size -= 1)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, dpi);
            if (formatted.Width <= width && formatted.Height <= height)
            {
                chosen = size;
                break;
            }
        }

        CurrentLineText.FontSize = chosen;
        NextLineText.FontSize = chosen;
        UpcomingLineText.FontSize = Math.Max(14, chosen * 0.62);
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
        LogState($"PlayTransitionAsync: start incoming={incomingInlines.Count}");

        Storyboard? sb = null;
        try
        {
            NextLineText.Inlines.Clear();
            foreach (var inline in incomingInlines)
            {
                NextLineText.Inlines.Add(CloneInline(inline));
            }

            CurrentTransform.Y = 0;
            NextTransform.Y = 140;
            CurrentLineText.Opacity = 1;
            NextLineText.Opacity = 0;

            if (!IsLoaded)
            {
                return;
            }

            sb = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(320);

            sb.Children.Add(MakeAnim(CurrentTransform, TranslateTransform.YProperty, 0, -140, duration));
            sb.Children.Add(MakeAnim(CurrentLineText, OpacityProperty, 1, 0, duration));
            sb.Children.Add(MakeAnim(NextTransform, TranslateTransform.YProperty, 140, 0, duration));
            sb.Children.Add(MakeAnim(NextLineText, OpacityProperty, 0, 1, duration));

            var tcs = new TaskCompletionSource<bool>();
            sb.Completed += (_, _) => tcs.TrySetResult(true);
            sb.Begin(this, true);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(350));
            if (completed != tcs.Task)
            {
                sb.Stop(this);
            }
        }
        finally
        {
            try
            {
                sb?.Remove(this);
            }
            catch
            {
                // ignored
            }

            CurrentLineText.BeginAnimation(OpacityProperty, null);
            NextLineText.BeginAnimation(OpacityProperty, null);
            CurrentTransform.BeginAnimation(TranslateTransform.YProperty, null);
            NextTransform.BeginAnimation(TranslateTransform.YProperty, null);

            CurrentTransform.Y = 0;
            NextTransform.Y = 140;
            CurrentLineText.Opacity = 1;
            NextLineText.Opacity = 0;
            CurrentLineText.Visibility = Visibility.Visible;
            NextLineText.Visibility = Visibility.Visible;

            CurrentLineText.Inlines.Clear();
            foreach (var inline in incomingInlines)
            {
                CurrentLineText.Inlines.Add(CloneInline(inline));
            }

            NextLineText.Inlines.Clear();
            LogState($"PlayTransitionAsync: finalize currentInlineCount={CurrentLineText.Inlines.Count} currentOpacity={CurrentLineText.Opacity:F2}");
            _isTransitioning = false;
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
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
        var thumbVisibility = _isResizeMode ? Visibility.Visible : Visibility.Collapsed;
        TopLeftResizeThumb.Visibility = thumbVisibility;
        TopResizeThumb.Visibility = thumbVisibility;
        TopRightResizeThumb.Visibility = thumbVisibility;
        LeftResizeThumb.Visibility = thumbVisibility;
        RightResizeThumb.Visibility = thumbVisibility;
        BottomLeftResizeThumb.Visibility = thumbVisibility;
        BottomResizeThumb.Visibility = thumbVisibility;
        BottomRightResizeThumb.Visibility = thumbVisibility;

        MainFrameBorder.BorderThickness = _isResizeMode ? new Thickness(1) : new Thickness(0);
        MainFrameBorder.BorderBrush = _isResizeMode ? Brushes.Black : Brushes.Transparent;
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
            RenderResultLine();
        }
        else
        {
            RenderCurrentLine();
        }
    }


    private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (OverlayContextMenu is null)
        {
            return;
        }

        OverlayContextMenu.PlacementTarget = this;
        OverlayContextMenu.IsOpen = true;
        e.Handled = true;
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
            Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(child),
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

        var minWidth = MinWidth > 0 ? MinWidth : 420;
        var minHeight = MinHeight > 0 ? MinHeight : 120;
        var thumbName = (sender as FrameworkElement)?.Name;

        switch (thumbName)
        {
            case nameof(TopLeftResizeThumb):
                ResizeFromLeft(e.HorizontalChange, minWidth);
                ResizeFromTop(e.VerticalChange, minHeight);
                break;
            case nameof(TopRightResizeThumb):
                ResizeFromRight(e.HorizontalChange, minWidth);
                ResizeFromTop(e.VerticalChange, minHeight);
                break;
            case nameof(TopResizeThumb):
                ResizeFromTop(e.VerticalChange, minHeight);
                break;
            case nameof(LeftResizeThumb):
                ResizeFromLeft(e.HorizontalChange, minWidth);
                break;
            case nameof(RightResizeThumb):
                ResizeFromRight(e.HorizontalChange, minWidth);
                break;
            case nameof(BottomLeftResizeThumb):
                ResizeFromLeft(e.HorizontalChange, minWidth);
                ResizeFromBottom(e.VerticalChange, minHeight);
                break;
            case nameof(BottomResizeThumb):
                ResizeFromBottom(e.VerticalChange, minHeight);
                break;
            default:
                ResizeFromRight(e.HorizontalChange, minWidth);
                ResizeFromBottom(e.VerticalChange, minHeight);
                break;
        }
    }

    private void ResizeFromRight(double horizontalChange, double minWidth)
    {
        Width = Math.Max(minWidth, Width + horizontalChange);
    }

    private void ResizeFromBottom(double verticalChange, double minHeight)
    {
        Height = Math.Max(minHeight, Height + verticalChange);
    }

    private void ResizeFromLeft(double horizontalChange, double minWidth)
    {
        var nextWidth = Math.Max(minWidth, Width - horizontalChange);
        var widthDelta = nextWidth - Width;
        Width = nextWidth;
        Left -= widthDelta;
    }

    private void ResizeFromTop(double verticalChange, double minHeight)
    {
        var nextHeight = Math.Max(minHeight, Height - verticalChange);
        var heightDelta = nextHeight - Height;
        Height = nextHeight;
        Top -= heightDelta;
    }
}
