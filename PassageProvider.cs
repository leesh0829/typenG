namespace TypeOverlay;

public sealed class PassageProvider
{
    private readonly List<string[]> _passages;
    private int _currentIndex = -1;

    public PassageProvider()
    {
        _passages =
        [
            Normalize(@"습관은 작은 반복에서 시작된다.
오늘의 한 줄이 내일의 실력을 만든다.
천천히 정확하게 입력하는 것이 먼저다."),
            Normalize(@"코드는 읽기 쉬워야 오래 살아남는다.
짧은 함수와 명확한 이름이 버그를 줄인다.
테스트는 두려움을 자신감으로 바꾼다."),
            Normalize(@"집중은 한 번에 한 가지 일에서 나온다.
알림을 끄고 호흡을 고르면 마음이 정리된다.
작은 완료를 쌓아 큰 목표에 도달하자."),
            Normalize(@"타이핑 연습의 핵심은 리듬과 정확도다.
손가락의 이동을 최소화하면 속도가 오른다.
실수는 기록하고 같은 실수를 줄여 보자."),
            Normalize(@"꾸준함은 재능을 이기는 가장 현실적인 힘이다.
매일 10분의 성실함이 변화를 만든다.
오늘도 한 단계 성장했다는 증거를 남기자.")
        ];
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

    private static string[] Normalize(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
