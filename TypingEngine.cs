using typenG.Models;

namespace typenG;

public enum LineCharState
{
    Pending,
    Correct,
    Incorrect
}

public sealed record RenderCharacter(char Character, LineCharState State);

public sealed class TypingEngine
{
    private readonly List<string> _lines = [];
    private readonly List<char> _inputBuffer = [];
    private DateTimeOffset? _startedAt;
    private int _evaluatedChars;
    private int _correctChars;
    private int _submittedChars;
    private int _submittedWords;

    public TypingStats Stats { get; } = new();
    public int CurrentLineIndex { get; private set; }
    public bool IsPassageComplete => CurrentLineIndex >= _lines.Count;
    public DateTimeOffset? FinishedAt { get; private set; }
    public int CurrentInputLength => _inputBuffer.Count;
    public int TotalLineCount => _lines.Count;
    public string NextLine => CurrentLineIndex + 1 < _lines.Count ? _lines[CurrentLineIndex + 1] : string.Empty;
    public bool HasStarted => _startedAt is not null;

    public void EnsureTimingStarted()
    {
        _startedAt ??= DateTimeOffset.Now;
    }

    public void LoadPassage(IEnumerable<string> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        if (_lines.Count == 0)
        {
            _lines.Add("(empty)");
        }

        ResetRunState();
    }

    public void ResetRunState()
    {
        _inputBuffer.Clear();
        CurrentLineIndex = 0;
        _startedAt = null;
        FinishedAt = null;
        Stats.TotalKeystrokes = 0;
        Stats.CorrectKeystrokes = 0;
        _evaluatedChars = 0;
        _correctChars = 0;
        _submittedChars = 0;
        _submittedWords = 0;
    }

    public string CurrentLine => IsPassageComplete ? string.Empty : _lines[CurrentLineIndex];


    public bool IsCurrentTargetHangul()
    {
        if (IsPassageComplete)
        {
            return false;
        }

        var idx = Math.Min(_inputBuffer.Count, CurrentLine.Length - 1);
        if (idx < 0 || idx >= CurrentLine.Length)
        {
            return false;
        }

        var c = CurrentLine[idx];
        return (c >= '가' && c <= '힣') || (c >= 'ㄱ' && c <= 'ㆎ');
    }
    public bool TryApplyText(char input)
    {
        if (IsPassageComplete)
        {
            return false;
        }

        var line = CurrentLine;
        if (_inputBuffer.Count >= line.Length)
        {
            // 입력 초과는 무시
            return false;
        }

        EnsureTimingStarted();
        Stats.TotalKeystrokes++;

        var idx = _inputBuffer.Count;
        if (line[idx] == input)
        {
            Stats.CorrectKeystrokes++;
        }

        _inputBuffer.Add(input);
        return true;
    }

    public bool HandleBackspace()
    {
        if (_inputBuffer.Count == 0)
        {
            return false;
        }

        _inputBuffer.RemoveAt(_inputBuffer.Count - 1);
        return true;
    }

    public bool CanAdvanceLine()
    {
        if (IsPassageComplete)
        {
            return false;
        }

        var line = CurrentLine;
        return _inputBuffer.Count == line.Length;
    }

    public bool AdvanceLine()
    {
        if (!CanAdvanceLine())
        {
            return false;
        }

        var line = CurrentLine;
        _submittedChars += line.Length;
        _submittedWords += CountWords(line);
        _evaluatedChars += line.Length;
        for (var i = 0; i < line.Length; i++)
        {
            if (_inputBuffer[i] == line[i])
            {
                _correctChars++;
            }
        }

        _inputBuffer.Clear();
        CurrentLineIndex++;

        if (IsPassageComplete)
        {
            FinishedAt = DateTimeOffset.Now;
        }

        return true;
    }

    public IReadOnlyList<RenderCharacter> BuildRenderLine()
    {
        var line = CurrentLine;
        var rendered = new List<RenderCharacter>(line.Length);

        for (var i = 0; i < line.Length; i++)
        {
            if (i >= _inputBuffer.Count)
            {
                rendered.Add(new RenderCharacter(line[i], LineCharState.Pending));
            }
            else if (_inputBuffer[i] == line[i])
            {
                rendered.Add(new RenderCharacter(line[i], LineCharState.Correct));
            }
            else
            {
                rendered.Add(new RenderCharacter(line[i], LineCharState.Incorrect));
            }
        }

        return rendered;
    }


    private static int CountWords(string line)
    {
        return line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    public (double cpm, double wpm, double acc) CalculateResults()
    {
        var start = _startedAt;
        var end = FinishedAt ?? DateTimeOffset.Now;

        if (start is null)
        {
            return (0, 0, 100);
        }

        var elapsedMinutes = Math.Max((end - start.Value).TotalMinutes, 1.0 / 60000.0);
        var acc = _evaluatedChars == 0 ? 100 : _correctChars * 100.0 / _evaluatedChars;
        var cpm = _submittedChars / elapsedMinutes;
        var wpm = _submittedWords / elapsedMinutes;
        return (cpm, wpm, acc);
    }
}
