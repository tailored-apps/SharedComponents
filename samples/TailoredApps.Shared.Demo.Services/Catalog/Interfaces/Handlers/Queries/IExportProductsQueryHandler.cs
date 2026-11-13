using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;

public interface IExportProductsQueryHandler : IStreamRequestHandler<ExportProducts, Product>;
