namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    public interface IModelBase<T> : IModelBase
    {
        T Id { get; set; }
    }

    public interface IModelBase
    {
    }
}
