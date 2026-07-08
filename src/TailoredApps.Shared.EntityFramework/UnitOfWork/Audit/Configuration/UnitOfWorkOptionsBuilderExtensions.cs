using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Exceptions;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration
{
    /// <summary>
    /// Provides extension methods on <see cref="IUnitOfWorkOptionsBuilder"/> for enabling
    /// the Unit of Work audit feature.
    /// </summary>
    public static class UnitOfWorkOptionsBuilderExtensions
    {
        /// <summary>
        /// Enables the Unit of Work audit mechanism, registering all required services and lifecycle hooks.
        /// </summary>
        /// <typeparam name="TTargetDbContext">
        /// The EF Core <see cref="DbContext"/> type to monitor for changes.
        /// </typeparam>
        /// <typeparam name="TEntityChangesAuditor">
        /// The <see cref="IEntityChangesAuditor"/> implementation that processes collected changes.
        /// </typeparam>
        /// <param name="options">The Unit of Work options builder to extend.</param>
        /// <param name="config">A configuration delegate used to set <see cref="IAuditSettings"/>.</param>
        /// <returns>The options builder for further chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidAuditConfigurationException">
        /// Thrown when the provided settings are missing required values.
        /// </exception>
        public static IUnitOfWorkOptionsBuilder WithUnitOfWorkAudit<TTargetDbContext, TEntityChangesAuditor>(this IUnitOfWorkOptionsBuilder options, Action<IAuditSettings> config)
            where TTargetDbContext : DbContext
            where TEntityChangesAuditor : class, IEntityChangesAuditor
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var auditSettings = new AuditSettings();
            config(auditSettings);
            ValidateSettings(auditSettings);

            options.Services.AddScoped<IUnitOfWorkAuditContext, UnitOfWorkAuditContext>();
            options.Services.AddSingleton<IAuditSettings>(auditSettings);
            options.Services.AddTransient<IEntityChangesAuditor, TEntityChangesAuditor>();
            options.Services.AddTransient<IAuditChangesCollector, AuditChangesCollector<TTargetDbContext>>();

            options.WithPostSaveChangesHook<PostSaveChangesAuditHook>()
                .WithPreSaveChangesHook<PreSaveChangesAuditHook>()
                .WithTransactionRollbackHook<TransactionRollbackAuditHook>()
                .WithTransactionCommitHook<TransactionCommitAuditHook>();

            return options;
        }

        private static void ValidateSettings(IAuditSettings auditSettings)
        {
            if (!auditSettings.TypesToCollect.Any())
                throw new InvalidAuditConfigurationException(nameof(auditSettings.TypesToCollect));

            if (!auditSettings.EntityStatesToCollect.Any())
                throw new InvalidAuditConfigurationException(nameof(auditSettings.EntityStatesToCollect));
        }
    }
}
