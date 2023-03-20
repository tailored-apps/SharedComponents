using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    public interface IActivity
    {
        DateTime CreatedDateUtc { get; set; }
        string CreatedBy { get; set; }
        DateTime? ModifiedDateUtc { get; set; }
        string ModifiedBy { get; set; }
    }
}
