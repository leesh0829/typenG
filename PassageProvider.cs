using System.IO;
using System.Text.Json;

namespace typenG;

public sealed class PassageProvider
{
    private const string PassageFileName = "passages.json";

    private readonly List<string[]> _passages;
    private int _currentIndex = -1;

    public PassageProvider()
    {
        _passages = LoadPassages();
        if (_passages.Count == 0)
        {
            _passages = BuildFallbackPassages();
        }
    }

    public string[] GetNextPassage()
    {
        if (_passages.Count == 0)
        {
            return ["No passage loaded."];
        }

        _currentIndex = (_currentIndex + 1) % _passages.Count;
        return _passages[_currentIndex];
    }

    private static List<string[]> LoadPassages()
    {
        var path = Path.Combine(AppContext.BaseDirectory, PassageFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(Environment.CurrentDirectory, PassageFileName);
        }

        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return NormalizeItems(items);
        }
        catch
        {
            return [];
        }
    }

    private static List<string[]> BuildFallbackPassages()
    {
        return NormalizeItems(
        [
            "습관은 작은 반복에서 시작된다. 오늘의 한 줄이 내일의 실력을 만든다. 천천히 정확하게 입력하는 것이 먼저다.",
            "코드는 읽기 쉬워야 오래 살아남는다. 짧은 함수와 명확한 이름이 버그를 줄인다. 테스트는 두려움을 자신감으로 바꾼다.",
            "집중은 한 번에 한 가지 일에서 나온다. 알림을 끄고 호흡을 고르면 마음이 정리된다. 작은 완료를 쌓아 큰 목표에 도달하자.",
            "타이핑 연습의 핵심은 리듬과 정확도다. 손가락의 이동을 최소화하면 속도가 오른다. 실수는 기록하고 같은 실수를 줄여 보자.",
            "이 문장은 장문 예시다. 시작은 느리지만 정확하게 치는 것이 중요하다. 속도는 정확도가 안정된 다음에 따라온다. 호흡을 일정하게 유지하고 오타를 줄여 보자. 오늘의 연습이 내일의 자신감을 만든다."
        ]);
    }

    private static List<string[]> NormalizeItems(IEnumerable<string> items)
    {
        var result = new List<string[]>();
        foreach (var item in items)
        {
            var lines = Normalize(item);
            if (lines.Length > 0)
            {
                result.Add(lines);
            }
        }

        return result;
    }

    private static string[] Normalize(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var byLine = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line));

        var sentences = new List<string>();
        foreach (var line in byLine)
        {
            var split = line.Split(['.', '!', '?'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
            {
                sentences.Add(line);
                continue;
            }

            foreach (var s in split)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    sentences.Add(s);
                }
            }
        }

        return sentences.ToArray();
    }
}
