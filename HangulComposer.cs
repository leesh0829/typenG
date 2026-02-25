namespace TypeOverlay;

/// <summary>
/// Lightweight 2-beolsik Hangul composer for ASCII key input.
/// It allows users to type Korean syllables even when English keyboard input is received.
/// </summary>
public sealed class HangulComposer
{
    private static readonly Dictionary<char, char> KeyToConsonant = new()
    {
        ['r'] = 'ㄱ', ['R'] = 'ㄲ', ['s'] = 'ㄴ', ['e'] = 'ㄷ', ['E'] = 'ㄸ', ['f'] = 'ㄹ',
        ['a'] = 'ㅁ', ['q'] = 'ㅂ', ['Q'] = 'ㅃ', ['t'] = 'ㅅ', ['T'] = 'ㅆ', ['d'] = 'ㅇ',
        ['w'] = 'ㅈ', ['W'] = 'ㅉ', ['c'] = 'ㅊ', ['z'] = 'ㅋ', ['x'] = 'ㅌ', ['v'] = 'ㅍ', ['g'] = 'ㅎ'
    };

    private static readonly Dictionary<char, char> KeyToVowel = new()
    {
        ['k'] = 'ㅏ', ['o'] = 'ㅐ', ['i'] = 'ㅑ', ['O'] = 'ㅒ', ['j'] = 'ㅓ', ['p'] = 'ㅔ',
        ['u'] = 'ㅕ', ['P'] = 'ㅖ', ['h'] = 'ㅗ', ['y'] = 'ㅛ', ['n'] = 'ㅜ', ['b'] = 'ㅠ', ['m'] = 'ㅡ', ['l'] = 'ㅣ'
    };

    private static readonly Dictionary<string, char> CompoundVowel = new()
    {
        ["ㅗㅏ"] = 'ㅘ', ["ㅗㅐ"] = 'ㅙ', ["ㅗㅣ"] = 'ㅚ', ["ㅜㅓ"] = 'ㅝ', ["ㅜㅔ"] = 'ㅞ', ["ㅜㅣ"] = 'ㅟ', ["ㅡㅣ"] = 'ㅢ'
    };

    private static readonly Dictionary<string, char> CompoundFinal = new()
    {
        ["ㄱㅅ"] = 'ㄳ', ["ㄴㅈ"] = 'ㄵ', ["ㄴㅎ"] = 'ㄶ', ["ㄹㄱ"] = 'ㄺ', ["ㄹㅁ"] = 'ㄻ',
        ["ㄹㅂ"] = 'ㄼ', ["ㄹㅅ"] = 'ㄽ', ["ㄹㅌ"] = 'ㄾ', ["ㄹㅍ"] = 'ㄿ', ["ㄹㅎ"] = 'ㅀ', ["ㅂㅅ"] = 'ㅄ'
    };

    private static readonly Dictionary<char, (char first, char second)> FinalSplit = new()
    {
        ['ㄳ'] = ('ㄱ', 'ㅅ'), ['ㄵ'] = ('ㄴ', 'ㅈ'), ['ㄶ'] = ('ㄴ', 'ㅎ'), ['ㄺ'] = ('ㄹ', 'ㄱ'), ['ㄻ'] = ('ㄹ', 'ㅁ'),
        ['ㄼ'] = ('ㄹ', 'ㅂ'), ['ㄽ'] = ('ㄹ', 'ㅅ'), ['ㄾ'] = ('ㄹ', 'ㅌ'), ['ㄿ'] = ('ㄹ', 'ㅍ'), ['ㅀ'] = ('ㄹ', 'ㅎ'), ['ㅄ'] = ('ㅂ', 'ㅅ')
    };

    private static readonly char[] ChoseongTable = ['ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'];
    private static readonly char[] JungseongTable = ['ㅏ', 'ㅐ', 'ㅑ', 'ㅒ', 'ㅓ', 'ㅔ', 'ㅕ', 'ㅖ', 'ㅗ', 'ㅘ', 'ㅙ', 'ㅚ', 'ㅛ', 'ㅜ', 'ㅝ', 'ㅞ', 'ㅟ', 'ㅠ', 'ㅡ', 'ㅢ', 'ㅣ'];
    private static readonly char[] JongseongTable = ['\0', 'ㄱ', 'ㄲ', 'ㄳ', 'ㄴ', 'ㄵ', 'ㄶ', 'ㄷ', 'ㄹ', 'ㄺ', 'ㄻ', 'ㄼ', 'ㄽ', 'ㄾ', 'ㄿ', 'ㅀ', 'ㅁ', 'ㅂ', 'ㅄ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'];

    private char? _choseong;
    private char? _jungseong;
    private char? _jongseong;

    public string CompositionText => BuildCurrent().ToString();

    public static bool IsAsciiKoreanKey(char c) => KeyToConsonant.ContainsKey(c) || KeyToVowel.ContainsKey(c);

    public void Reset()
    {
        _choseong = null;
        _jungseong = null;
        _jongseong = null;
    }

    public string Flush()
    {
        var current = BuildCurrent();
        Reset();
        return current == '\0' ? string.Empty : current.ToString();
    }

    public string ProcessKey(char key)
    {
        if (!IsAsciiKoreanKey(key))
        {
            var flushed = Flush();
            return flushed + key;
        }

        if (KeyToVowel.TryGetValue(key, out var vowel))
        {
            return ProcessVowel(vowel);
        }

        var consonant = KeyToConsonant[key];
        return ProcessConsonant(consonant);
    }

    private string ProcessVowel(char vowel)
    {
        if (_choseong is null)
        {
            _choseong = 'ㅇ';
            _jungseong = vowel;
            return string.Empty;
        }

        if (_jungseong is null)
        {
            _jungseong = vowel;
            return string.Empty;
        }

        if (_jongseong is null)
        {
            if (CompoundVowel.TryGetValue($"{_jungseong}{vowel}", out var merged))
            {
                _jungseong = merged;
                return string.Empty;
            }

            var commit = BuildCurrent();
            _choseong = 'ㅇ';
            _jungseong = vowel;
            _jongseong = null;
            return commit.ToString();
        }

        if (FinalSplit.TryGetValue(_jongseong.Value, out var split))
        {
            var commitWithFirst = Compose(_choseong!.Value, _jungseong!.Value, split.first);
            _choseong = split.second;
            _jungseong = vowel;
            _jongseong = null;
            return commitWithFirst.ToString();
        }
        else
        {
            var moved = _jongseong.Value;
            var commit = Compose(_choseong!.Value, _jungseong!.Value, '\0');
            _choseong = moved;
            _jungseong = vowel;
            _jongseong = null;
            return commit.ToString();
        }
    }

    private string ProcessConsonant(char consonant)
    {
        if (_choseong is null)
        {
            _choseong = consonant;
            return string.Empty;
        }

        if (_jungseong is null)
        {
            var commit = _choseong.Value;
            _choseong = consonant;
            return commit.ToString();
        }

        if (_jongseong is null)
        {
            _jongseong = consonant;
            return string.Empty;
        }

        if (CompoundFinal.TryGetValue($"{_jongseong}{consonant}", out var mergedFinal))
        {
            _jongseong = mergedFinal;
            return string.Empty;
        }

        var commitSyllable = BuildCurrent();
        _choseong = consonant;
        _jungseong = null;
        _jongseong = null;
        return commitSyllable.ToString();
    }

    private char BuildCurrent()
    {
        if (_choseong is null)
        {
            return '\0';
        }

        if (_jungseong is null)
        {
            return _choseong.Value;
        }

        return Compose(_choseong.Value, _jungseong.Value, _jongseong ?? '\0');
    }

    private static char Compose(char choseong, char jungseong, char jongseong)
    {
        var l = Array.IndexOf(ChoseongTable, choseong);
        var v = Array.IndexOf(JungseongTable, jungseong);
        var t = jongseong == '\0' ? 0 : Array.IndexOf(JongseongTable, jongseong);

        if (l < 0 || v < 0 || t < 0)
        {
            return choseong;
        }

        return (char)(0xAC00 + (l * 21 + v) * 28 + t);
    }
}
