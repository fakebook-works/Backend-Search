using System;
using System.Collections.Generic;

namespace Backend_Search_Fakebook.Models;

public partial class Token
{
    public long Id { get; set; }

    public string TokenText { get; set; } = null!;

    public virtual ICollection<Object> Objects { get; set; } = new List<Object>();
}
