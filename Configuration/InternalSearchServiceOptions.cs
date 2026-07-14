namespace BackEndSearchFakebook.Configuration
{
    public sealed class InternalSearchServiceOptions
    {
        public const string SectionName = "InternalSearchService";

        public string Secret { get; set; } = string.Empty;
    }
}
