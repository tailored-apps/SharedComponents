using ForkForward.Mapper;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using Model = TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.Services.Catalog;

/// <summary>
/// Entity to data model and back, declared once (ForkForward.Mapper). The repository uses the same
/// configuration to map objects it has loaded and to project a query, so that a list reads only the columns
/// the model has.
/// </summary>
public class CatalogMappingProfile : Profile
{
    public CatalogMappingProfile()
    {
        CreateMap<Entities.Product, Model.Product>();

        // A command becomes a new entity; the database assigns the key.
        CreateMap<AddProduct, Entities.Product>()
            .ForMember(product => product.Id, options => options.Ignore());
    }
}
