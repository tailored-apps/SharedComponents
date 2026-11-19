using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;

namespace TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Commands;

public interface IAddProductCommandHandler : IRequestHandler<AddProduct, AddProductResponse>;
