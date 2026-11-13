using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

/// <summary>
/// A stream request: the products arrive one by one as the database returns them, and the response is
/// written the same way, so the whole catalogue is never held in memory.
/// </summary>
public class ExportProducts : IStreamRequest<Product>
{
    public string? Category { get; set; }
}
