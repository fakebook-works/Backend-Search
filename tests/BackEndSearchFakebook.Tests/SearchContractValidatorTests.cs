using BackEndSearchFakebook.Contracts;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class SearchContractValidatorTests
{
    [Theory]
    [InlineData("user", (short)SearchObjectType.User, "user")]
    [InlineData(" GROUP ", (short)SearchObjectType.Group, "group")]
    [InlineData("FEEDPOST", (short)SearchObjectType.FeedPost, "feedPost")]
    [InlineData("groupPost", (short)SearchObjectType.GroupPost, "groupPost")]
    [InlineData("Reel", (short)SearchObjectType.Reel, "reel")]
    public void TryMapObjectType_AcceptsEveryCanonicalTypeCaseInsensitively(
        string input,
        short expectedType,
        string expectedCanonicalName)
    {
        var valid = SearchContractValidator.TryMapObjectType(
            input,
            out var type,
            out var canonicalName);

        Assert.True(valid);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedCanonicalName, canonicalName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("post")]
    [InlineData("story")]
    [InlineData("feed-post")]
    public void TryMapObjectType_RejectsUnknownOrAmbiguousTypes(string? input)
    {
        var valid = SearchContractValidator.TryMapObjectType(
            input,
            out _,
            out var canonicalName);

        Assert.False(valid);
        Assert.Equal(string.Empty, canonicalName);
    }

    [Fact]
    public void TryValidateKeyword_EnforcesRequiredAndMaximumLength()
    {
        Assert.False(SearchContractValidator.TryValidateKeyword(null, out _));
        Assert.False(SearchContractValidator.TryValidateKeyword(" \t\r\n ", out _));
        Assert.True(SearchContractValidator.TryValidateKeyword(
            new string('a', SearchContractValidator.MaximumKeywordLength),
            out var validMessage), validMessage);
        Assert.False(SearchContractValidator.TryValidateKeyword(
            new string('a', SearchContractValidator.MaximumKeywordLength + 1),
            out _));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, SearchContractValidator.MaximumPageSize, true)]
    [InlineData(1001, 100, true)]
    [InlineData(1002, 100, false)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, SearchContractValidator.MaximumPageSize + 1, false)]
    [InlineData(SearchContractValidator.MaximumPageNumber + 1, 1, false)]
    public void TryValidatePaging_EnforcesBoundsAndMaximumOffset(
        int pageNumber,
        int pageSize,
        bool expectedValid)
    {
        var valid = SearchContractValidator.TryValidatePaging(
            pageNumber,
            pageSize,
            out var message);

        Assert.Equal(expectedValid, valid);
        Assert.Equal(expectedValid, string.IsNullOrEmpty(message));
    }
}
