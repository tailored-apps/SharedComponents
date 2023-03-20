using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Exceptions;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Hooks;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Configuration
{
    public static class UnitOfWorkOptionsBuilderExtensions
    {
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