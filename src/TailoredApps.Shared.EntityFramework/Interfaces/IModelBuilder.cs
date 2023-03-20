using Microsoft.EntityFrameworkCore;

namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    public interface IModelBuilder
    {
        void MapModel(ModelBuilder modelBuilder);
    }
}
