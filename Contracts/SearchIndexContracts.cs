using BackEndSearchFakebook.Helper;

namespace BackEndSearchFakebook.Contracts;

public sealed record UpsertSearchIndexRequest(string? ObjectType, string? Text);

public sealed record SearchIndexWritePayload(
    bool Success,
    long Id,
    string ObjectType,
    bool Created);

public static class SearchContractValidator
{
    // Must accommodate the SocialGraph post contract (63,206 characters); rejecting a
    // valid post here would leave its canonical object without a searchable projection.
    public const int MaximumTextLength = 63_206;
    public const int MaximumKeywordLength = 200;
    public const int MaximumPageSize = 100;
    public const int MaximumPageNumber = 1_000_000;
    public const int MaximumOffset = 100_000;
    public const int MaximumQueryTokens = 12;
    public const int MaximumIndexedTokens = 512;

    public static bool IsPositiveId(long id) => id > 0;

    public static bool TryMapObjectType(
        string? objectType,
        out short type,
        out string canonicalObjectType)
    {
        canonicalObjectType = objectType?.Trim() ?? string.Empty;
        switch (canonicalObjectType.ToLowerInvariant())
        {
            case "user":
                type = 0;
                canonicalObjectType = "user";
                return true;
            case "group":
                type = 1;
                canonicalObjectType = "group";
                return true;
            case "feedpost":
                type = 2;
                canonicalObjectType = "feedPost";
                return true;
            case "grouppost":
                type = 3;
                canonicalObjectType = "groupPost";
                return true;
            case "reel":
                type = 4;
                canonicalObjectType = "reel";
                return true;
            default:
                type = default;
                canonicalObjectType = string.Empty;
                return false;
        }
    }

    public static bool IsValidLegacyType(short type) => type is >= 0 and <= 4;

    public static bool TryValidateText(string? text, out string message)
        => TryNormalizeText(text, out _, out message);

    public static bool TryNormalizeText(string? text, out string normalized, out string message)
    {
        if (!InputTextSecurity.TryNormalize(text, MaximumTextLength, out normalized, out message))
        {
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool TryValidateKeyword(string? keyword, out string message)
        => TryNormalizeKeyword(keyword, out _, out message);

    public static bool TryNormalizeKeyword(string? keyword, out string normalized, out string message)
    {
        if (!InputTextSecurity.TryNormalize(keyword, MaximumKeywordLength, out normalized, out message))
        {
            return false;
        }


        if (TextHelper.Tokenize(normalized).Distinct(StringComparer.Ordinal).Count() > MaximumQueryTokens)
        {
            message = $"keyword must not contain more than {MaximumQueryTokens} distinct terms.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool TryValidatePaging(int pageNumber, int pageSize, out string message)
    {
        if (pageNumber is < 1 or > MaximumPageNumber)
        {
            message = $"pageNumber must be between 1 and {MaximumPageNumber}.";
            return false;
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            message = $"pageSize must be between 1 and {MaximumPageSize}.";
            return false;
        }

        if ((long)(pageNumber - 1) * pageSize > MaximumOffset)
        {
            message = $"The requested page offset must not exceed {MaximumOffset}.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
