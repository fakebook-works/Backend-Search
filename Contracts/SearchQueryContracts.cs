namespace BackEndSearchFakebook.Contracts;

public enum SearchObjectType : short
{
    User = 0,
    Group = 1,
    FeedPost = 2,
    GroupPost = 3,
    Reel = 4
}

public enum ProfileConnectionType
{
    Friends,
    Following,
    Followers
}

public sealed record SearchCandidate(long ReferenceId, SearchObjectType ObjectType);

public sealed record SearchCandidatePage(
    IReadOnlyList<long> ReferenceIds,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public enum SearchViewRecordResult
{
    Recorded,
    AlreadyRecorded,
    NotFound
}
