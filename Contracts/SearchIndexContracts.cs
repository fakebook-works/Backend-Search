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
    public const int MaximumTextLength = 50_000;
    public const int MaximumKeywordLength = 200;
    public const int MaximumPageSize = 100;
    public const int MaximumPageNumber = 1_000_000;
    public const int MaximumOffset = 100_000;

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
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            message = "text must contain at least one non-whitespace character.";
            return false;
        }

        if (text.Length > MaximumTextLength)
        {
            message = $"text must not exceed {MaximumTextLength} characters.";
            return false;
        }

        if (TextHelper.Tokenize(text).Any(token => token.Length > 255))
        {
            message = "text contains a token longer than 255 characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool TryValidateKeyword(string? keyword, out string message)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            message = "keyword must contain at least one non-whitespace character.";
            return false;
        }

        if (keyword.Length > MaximumKeywordLength)
        {
            message = $"keyword must not exceed {MaximumKeywordLength} characters.";
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
