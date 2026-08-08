using System.Globalization;
using System.Text;

namespace BackEndSearchFakebook.Helper;

/// <summary>Cheap, dependency-free Unicode boundary used before tokenization.</summary>
public static class InputTextSecurity
{
    private const int MaximumCombiningMarks = 256;
    public static bool TryNormalize(
        string? value,
        int maximumLength,
        out string normalized,
        out string message,
        bool allowEmpty = false)
    {
        normalized = string.Empty;
        message = string.Empty;
        if (value is null)
        {
            message = "text is required.";
            return false;
        }

        if (value.Length > checked(maximumLength * 2 + 32))
        {
            message = $"text must not exceed {maximumLength} Unicode characters.";
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index]))
            {
                continue;
            }

            if (!char.IsHighSurrogate(value[index]) || index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
            {
                message = "text contains an invalid Unicode sequence.";
                return false;
            }

            index++;
        }

        var candidate = value.Normalize(NormalizationForm.FormKC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var builder = new StringBuilder(candidate.Length);
        var runes = 0;
        var combining = 0;
        var consecutiveCombining = 0;
        foreach (var rune in candidate.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            // LF and TAB are legitimate in indexed post text. They are the only
            // control characters allowed; all other Cc/Cf/private-use values are
            // rejected to avoid Zalgo/bidi/rendering abuse.
            if (rune.Value is '\n' or '\t')
            {
                if (++runes > maximumLength)
                {
                    message = $"text must not exceed {maximumLength} Unicode characters.";
                    return false;
                }

                builder.Append(rune.ToString());
                consecutiveCombining = 0;
                continue;
            }

            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            {
                message = "text contains an unsupported control or formatting character.";
                return false;
            }

            if (category is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                message = "text contains an unsupported line separator.";
                return false;
            }

            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                combining++;
                consecutiveCombining++;
                if (consecutiveCombining > 3 || combining > Math.Min(MaximumCombiningMarks, Math.Max(16, maximumLength / 8)))
                {
                    message = "text contains excessive combining marks.";
                    return false;
                }
            }
            else
            {
                consecutiveCombining = 0;
            }

            if (++runes > maximumLength)
            {
                message = $"text must not exceed {maximumLength} Unicode characters.";
                return false;
            }

            builder.Append(rune.ToString());
        }

        normalized = builder.ToString();
        if (!allowEmpty && string.IsNullOrWhiteSpace(normalized))
        {
            message = "text must contain at least one non-whitespace character.";
            return false;
        }

        return true;
    }
}
