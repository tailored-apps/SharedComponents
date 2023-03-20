using System;

namespace TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork
{
    public interface ITransaction : IDisposable
    {
        void Commit();

        void Rollback();
    }
}