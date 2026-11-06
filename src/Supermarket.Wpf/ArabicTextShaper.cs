using System.Text;

namespace Supermarket.Wpf;

/// <summary>
/// Lightweight Arabic shaper and bidi reorderer for canvas renderers (such as SkiaSharp / LiveCharts)
/// that lack native Complex Text Layout (CTL) shaping.
/// Maps Arabic letters to Unicode Presentation Forms-B and reverses Arabic runs for LTR canvas rendering.
/// </summary>
public static class ArabicTextShaper
{
    private readonly struct Forms
    {
        public char Isolated { get; }
        public char Final { get; }
        public char Initial { get; }
        public char Medial { get; }
        public bool ConnectsNext { get; }

        public Forms(char isolated, char final, char initial, char medial, bool connectsNext)
        {
            Isolated = isolated;
            Final = final;
            Initial = initial;
            Medial = medial;
            ConnectsNext = connectsNext;
        }

        public Forms(char isolated, char final)
            : this(isolated, final, isolated, final, false)
        {
        }
    }

    private static readonly Dictionary<char, Forms> LetterMap = new()
    {
        { '\u0621', new Forms('\uFE80', '\uFE80', '\uFE80', '\uFE80', false) }, // ء
        { '\u0622', new Forms('\uFE81', '\uFE82') }, // آ
        { '\u0623', new Forms('\uFE83', '\uFE84') }, // أ
        { '\u0624', new Forms('\uFE85', '\uFE86') }, // ؤ
        { '\u0625', new Forms('\uFE87', '\uFE88') }, // إ
        { '\u0626', new Forms('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C', true) },  // ئ
        { '\u0627', new Forms('\uFE8D', '\uFE8E') }, // ا
        { '\u0628', new Forms('\uFE8F', '\uFE90', '\uFE91', '\uFE92', true) },  // ب
        { '\u0629', new Forms('\uFE93', '\uFE94') }, // ة
        { '\u062A', new Forms('\uFE95', '\uFE96', '\uFE97', '\uFE98', true) },  // ت
        { '\u062B', new Forms('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C', true) },  // ث
        { '\u062C', new Forms('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0', true) },  // ج
        { '\u062D', new Forms('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4', true) },  // ح
        { '\u062E', new Forms('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8', true) },  // خ
        { '\u062F', new Forms('\uFEA9', '\uFEAA') }, // د
        { '\u0630', new Forms('\uFEAB', '\uFEAC') }, // ذ
        { '\u0631', new Forms('\uFEAD', '\uFEAE') }, // ر
        { '\u0632', new Forms('\uFEAF', '\uFEB0') }, // ز
        { '\u0633', new Forms('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4', true) },  // س
        { '\u0634', new Forms('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8', true) },  // ش
        { '\u0635', new Forms('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC', true) },  // ص
        { '\u0636', new Forms('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0', true) },  // ض
        { '\u0637', new Forms('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4', true) },  // ط
        { '\u0638', new Forms('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8', true) },  // ظ
        { '\u0639', new Forms('\uFEC9', '\uFECA', '\uFECB', '\uFECC', true) },  // ع
        { '\u063A', new Forms('\uFECD', '\uFECE', '\uFECF', '\uFED0', true) },  // غ
        { '\u0641', new Forms('\uFED1', '\uFED2', '\uFED3', '\uFED4', true) },  // ف
        { '\u0642', new Forms('\uFED5', '\uFED6', '\uFED7', '\uFED8', true) },  // ق
        { '\u0643', new Forms('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC', true) },  // ك
        { '\u0644', new Forms('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0', true) },  // ل
        { '\u0645', new Forms('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4', true) },  // م
        { '\u0646', new Forms('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8', true) },  // ن
        { '\u0647', new Forms('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC', true) },  // ه
        { '\u0648', new Forms('\uFEED', '\uFEEE') }, // و
        { '\u0649', new Forms('\uFEEF', '\uFEF0') }, // ى
        { '\u064A', new Forms('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4', true) }   // ي
    };

    public static bool HasArabic(string? text) =>
        !string.IsNullOrEmpty(text) && text.Any(c => (c >= '\u0600' && c <= '\u06FF') || (c >= '\uFB50' && c <= '\uFEFC'));

    /// <summary>
    /// Shapes Arabic characters to connected presentation forms and reorders them for an LTR canvas.
    /// Non-Arabic text (digits, English, punctuation) maintains its correct logical order.
    /// </summary>
    public static string ShapeAndReverse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        if (!HasArabic(input))
        {
            return input;
        }

        var shaped = Shape(input);
        return ReorderForLtr(shaped);
    }

    private static string Shape(string input)
    {
        var sb = new StringBuilder(input.Length);
        int n = input.Length;

        for (int i = 0; i < n; i++)
        {
            char c = input[i];

            // Lam-Alef ligatures
            if (c == '\u0644' && i + 1 < n)
            {
                char next = input[i + 1];
                char? la = next switch
                {
                    '\u0622' => '\uFEF5', // لآ
                    '\u0623' => '\uFEF7', // لأ
                    '\u0625' => '\uFEF9', // لإ
                    '\u0627' => '\uFEFB', // لا
                    _ => null
                };

                if (la.HasValue)
                {
                    bool prevConnects = i > 0 && LetterMap.TryGetValue(input[i - 1], out var pf) && pf.ConnectsNext;
                    sb.Append(prevConnects ? (char)(la.Value + 1) : la.Value);
                    i++; // skip alef
                    continue;
                }
            }

            if (!LetterMap.TryGetValue(c, out var forms))
            {
                sb.Append(c);
                continue;
            }

            bool prevConn = i > 0 && LetterMap.TryGetValue(input[i - 1], out var p) && p.ConnectsNext;
            bool nextConn = i + 1 < n && LetterMap.ContainsKey(input[i + 1]);

            if (prevConn && nextConn && forms.ConnectsNext)
            {
                sb.Append(forms.Medial);
            }
            else if (prevConn)
            {
                sb.Append(forms.Final);
            }
            else if (nextConn && forms.ConnectsNext)
            {
                sb.Append(forms.Initial);
            }
            else
            {
                sb.Append(forms.Isolated);
            }
        }

        return sb.ToString();
    }

    private static string ReorderForLtr(string text)
    {
        // Tokenize into runs of Arabic words, spaces, and Latin/numeric numbers
        var tokens = new List<(string Content, bool IsArabic)>();
        var current = new StringBuilder();
        bool? currentIsArabic = null;

        foreach (char c in text)
        {
            bool isArabicChar = (c >= '\u0600' && c <= '\u06FF') || (c >= '\uFE70' && c <= '\uFEFC');
            bool isSeparator = char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' || c == '-' || c == '/' || c == ':';

            if (isSeparator)
            {
                if (current.Length > 0)
                {
                    tokens.Add((current.ToString(), currentIsArabic ?? false));
                    current.Clear();
                    currentIsArabic = null;
                }
                tokens.Add((c.ToString(), false));
                continue;
            }

            if (currentIsArabic.HasValue && currentIsArabic.Value != isArabicChar)
            {
                tokens.Add((current.ToString(), currentIsArabic.Value));
                current.Clear();
            }

            currentIsArabic = isArabicChar;
            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add((current.ToString(), currentIsArabic ?? false));
        }

        // For an overall RTL sentence rendered on an LTR canvas, tokens flow in reverse order.
        // For Arabic tokens, their characters are reversed (because Arabic is written RTL).
        // For Latin/numeric tokens, their internal character order is preserved (e.g. "250" stays "250").
        var result = new StringBuilder(text.Length);
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            var (content, isAr) = tokens[i];
            if (isAr)
            {
                for (int j = content.Length - 1; j >= 0; j--)
                {
                    result.Append(content[j]);
                }
            }
            else
            {
                // Invert mirrorable parentheses in RTL context
                foreach (char c in content)
                {
                    char mapped = c switch
                    {
                        '(' => ')',
                        ')' => '(',
                        '[' => ']',
                        ']' => '[',
                        _ => c
                    };
                    result.Append(mapped);
                }
            }
        }

        return result.ToString();
    }
}
