using System.Collections.Generic;
namespace TailoredApps.Shared.Querying
{
    public interface IPagedResult<T>
    {
        ICollection<T> Results { get; set; }
        int Count { get; set; }
    }
}
