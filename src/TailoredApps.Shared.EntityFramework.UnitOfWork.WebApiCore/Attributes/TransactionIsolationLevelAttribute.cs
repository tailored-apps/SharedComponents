using System;
using System.Data;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Attributes
{
    /// <summary>
    /// Decorates a controller action (or entire controller class) to specify
    /// the database transaction isolation level that the Unit of Work should apply.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TransactionIsolationLevelAttribute : Attribute
    {
        /// <summary>The isolation level to apply to the database transaction for the decorated action.</summary>
        public IsolationLevel Level { get; set; }

        /// <summary>
        /// Initialises the attribute with the specified isolation level.
        /// </summary>
        /// <param name="level">The <see cref="IsolationLevel"/> to use for the transaction.</param>
        public TransactionIsolationLevelAttribute(IsolationLevel level)
        {
            Level = level;
        }
    }
}
