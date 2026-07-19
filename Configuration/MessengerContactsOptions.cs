namespace BackEndSearchFakebook.Configuration;

public sealed class MessengerContactsOptions
{
    public const string SectionName = "InternalServices:Messaging";

    public string BaseUrl { get; set; } = string.Empty;

    public string SharedSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 3;

    public int CacheSeconds { get; set; } = 45;
}
