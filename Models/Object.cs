using System;
using System.Collections.Generic;

namespace Backend_Search_Fakebook.Models;

public partial class Object
{
    public long Id { get; set; }

    public string Type { get; set; } = null!;

    public int? SortKey { get; set; }

    public long OwnerId { get; set; }

    public int? PrivacyLevel { get; set; }

    public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
}
