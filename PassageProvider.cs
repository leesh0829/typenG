using System.IO;
using System.Text.Json;

namespace typenG;

public sealed class PassageProvider
{
    private const string PassageFileName = "passages.json";

    private readonly List<string[]> _shortPassages = [];
    private readonly List<string[]> _longPassages = [];
    private readonly Random _random = new();

    private int _lastShortIndex = -1;
    private int _lastLongIndex = -1;

    public PassageProvider()
    {
        var loaded = LoadPassages();
        if (loaded.Count == 0)
        {
            loaded = BuildFallbackPassages();
        }

        foreach (var passage in loaded)
        {
            if (passage.Length <= 2)
            {
                _shortPassages.Add(passage);
            }
            else
            {
                _longPassages.Add(passage);
            }
        }

        if (_shortPassages.Count == 0 && _longPassages.Count > 0)
        {
            _shortPassages.AddRange(_longPassages.Take(Math.Min(2, _longPassages.Count)));
        }

        if (_longPassages.Count == 0 && _shortPassages.Count > 0)
        {
            _longPassages.AddRange(_shortPassages);
        }
    }

    public string[] GetNextPassage()
    {
        if (_shortPassages.Count == 0 && _longPassages.Count == 0)
        {
            return ["No passage loaded."];
        }

        var pickLong = _shortPassages.Count == 0
            || (_longPassages.Count > 0 && _random.NextDouble() >= 0.5);

        return pickLong
            ? PickRandom(_longPassages, ref _lastLongIndex)
            : PickRandom(_shortPassages, ref _lastShortIndex);
    }

    private string[] PickRandom(List<string[]> items, ref int lastIndex)
    {
        if (items.Count == 0)
        {
            return ["No passage loaded."];
        }

        if (items.Count == 1)
        {
            lastIndex = 0;
            return items[0];
        }

        var index = _random.Next(items.Count);
        if (index == lastIndex)
        {
            index = (index + 1 + _random.Next(items.Count - 1)) % items.Count;
        }

        lastIndex = index;
        return items[index];
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
            "짧게 정확하게 치는 연습이 속도의 시작이다.",
            "하루 10분의 반복은 분명한 변화를 만든다.",
            "코드는 읽기 쉬워야 오래 살아남는다. 짧은 함수와 명확한 이름이 버그를 줄인다. 테스트는 두려움을 자신감으로 바꾼다.",
            "집중은 한 번에 한 가지 일에서 나온다. 알림을 끄고 호흡을 고르면 마음이 정리된다. 작은 완료를 쌓아 큰 목표에 도달하자.",
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
