# TailoredApps.Shared.EntityFramework

## Introduction 

Nuget packages for integrating Unit of Work (a pattern for data management and handling transactions) into backend projects that rely on Microsoft Dependency Injection and Entity Framework Core.

## ASP.NET Core Confguration

To start using simply register it within Microsoft Dependency Injection container using following extension method:

```csharp
services.AddUnitOfWorkForWebApi<IDbContextInteface, DbContext>();
```

Register action filter:

```csharp
// ASP.NET Core 
services.AddMvc(options =>
{
    options.Filters.AddUnitOfWorkTransactionAttribute();
});

```

Then inject `IUnitOfWork<IDbContextInteface>` into Your service.

## Hooks

package allows user to use following hooks:
* `PreSaveChangesHook` - executed before `DbContext.SaveChanges()` is executed.
* `PostSaveChangesHook` - executed after `DbContext.SaveChanges()` is executed.
* `TransactionRollbackHook` - executed after `IDbContextTransaction.Rollback()` is executed.
* `TransactionCommitHook` - executed after `IDbContextTransaction.Commit()` is executed.

### Configuration

```csharp
services.AddUnitOfWorkForWebApi<IDbContextInteface, DbContext>()
    .WithPostSaveChangesHook<PostSaveChangesHook>() // interface IPostSaveChangesHook
    .WithPreSaveChangesHook<PreSaveChangesHook>() // interface IPreSaveChangesHook
    .WithTransactionRollbackHook<TransactionRollbackHook>() // interface ITransactionRollbackHook
    .WithTransactionCommitHook<TransactionCommitHook>(); // interface ITransactionCommitHook
```


# TailoredApps.Shared.EntityFramework.UnitOfWork.Audit

This namespace is extension and it provides the user with ability to audit all changes that occurs within the scope of transaction while using Unit Of Work.

## Confguration

To configure `Audit` you need to provide a list of entities that You would like to monitor as well as entity states. Apart from that you need to implelement an `IEntityChangesAuditor` intrface. 

The `IEntityChangesAuditor.AuditChanges(IEnumerable<EntityChange> entityChanges)` is being called every time the transaction is commited. The parameter `entityChanges` will contain all of the entities in approperiate state corresponding with the setting you provided while registering the audit.

**NOTE:** It is recommended to narrow down those settings as much as possible to increase the 
performance.

```csharp
var typesToCollect = new List<Type> { typeof(Entity1), typeof(Entity2), };
var entityStatesToCollect = new List<AuditEntityState> 
{ 
    AuditEntityState.Added, 
    AuditEntityState.Modified,
    AuditEntityState.Deleted  
};

services.AddUnitOfWorkForWebApi<IDbContextInteface, DbContext>()
    .WithUnitOfWorkAudit<DbContext, IEntityChangesAuditor>(settings =>
    {
        settings.TypesToCollect = typesToCollect;
        settings.EntityStatesToCollect = entityStatesToCollect;
    })
```