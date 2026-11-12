using System;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit.Utils;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit;

/// <summary>
/// How an audit record already collected in a transaction is merged with a later change of the same entity.
/// </summary>
public class EntityChangeUpdateOperationFactoryTests
{
    private const string Identifier = "ExampleEntity_1";

    private static InternalEntityChange<ExampleEntity> Change(string original, string current, AuditEntityState state)
        => new(new ExampleEntity { Prop = current }, new ExampleEntity { Prop = original }, new Dictionary<string, object> { ["Id"] = 1 }, state);

    /// <summary>Applies the operation registered for the transition between the two changes and returns what is left.</summary>
    private static IDictionary<string, IInternalEntityChange> Merge(InternalEntityChange<ExampleEntity> existing, InternalEntityChange<ExampleEntity> collected)
    {
        var changes = new Dictionary<string, IInternalEntityChange> { [Identifier] = existing };
        var context = new EntityChangeUpdateContext(changes, collected, Identifier);
        var transition = new EntityStateTransition(existing.GetAuditEntityState(), collected.GetAuditEntityState());

        EntityChangeUpdateOperationFactory.Create(transition)(context);

        return changes;
    }

    [Fact]
    public void When_Added_Entity_Is_Deleted_In_The_Same_Transaction_Should_Drop_The_Record()
    {
        // arrange
        var existing = Change(original: "new", current: "new", AuditEntityState.Added);
        var collected = Change(original: "new", current: "new", AuditEntityState.Deleted);

        // act
        var changes = Merge(existing, collected);

        // assert
        Assert.Empty(changes);
    }

    [Fact]
    public void When_Added_Entity_Is_Modified_Should_Stay_Added_With_The_Latest_Values()
    {
        // arrange
        var existing = Change(original: "new", current: "new", AuditEntityState.Added);
        var collected = Change(original: "new", current: "edited", AuditEntityState.Modified);

        // act
        Merge(existing, collected);

        // assert
        Assert.Equal(AuditEntityState.Added, existing.State);
        Assert.Equal("edited", existing.CurrentEntity.Prop);
        Assert.Equal("edited", existing.OriginalEntity.Prop);
    }

    [Fact]
    public void When_Modified_Entity_Is_Modified_Again_Should_Keep_The_First_Original_And_Take_The_Latest_Current()
    {
        // arrange
        var existing = Change(original: "stored", current: "first edit", AuditEntityState.Modified);
        var collected = Change(original: "first edit", current: "second edit", AuditEntityState.Modified);

        // act
        Merge(existing, collected);

        // assert
        Assert.Equal(AuditEntityState.Modified, existing.State);
        Assert.Equal("stored", existing.OriginalEntity.Prop);
        Assert.Equal("second edit", existing.CurrentEntity.Prop);
    }

    [Fact]
    public void When_Modified_Entity_Is_Deleted_Should_Become_Deleted_With_The_Collected_Values()
    {
        // arrange
        var existing = Change(original: "stored", current: "edited", AuditEntityState.Modified);
        var collected = Change(original: "edited", current: "deleted", AuditEntityState.Deleted);

        // act
        var changes = Merge(existing, collected);

        // assert
        Assert.Same(existing, Assert.Single(changes).Value);
        Assert.Equal(AuditEntityState.Deleted, existing.State);
        Assert.Equal("edited", existing.OriginalEntity.Prop);
        Assert.Equal("deleted", existing.CurrentEntity.Prop);
    }

    [Fact]
    public void When_Deleted_Entity_Is_Added_Again_Should_Take_The_New_Values_For_Both_Snapshots()
    {
        // arrange
        var existing = Change(original: "stored", current: "stored", AuditEntityState.Deleted);
        var collected = Change(original: "recreated", current: "recreated", AuditEntityState.Added);

        // act
        Merge(existing, collected);

        // assert
        Assert.Equal(AuditEntityState.Deleted, existing.State);
        Assert.Equal("recreated", existing.OriginalEntity.Prop);
        Assert.Equal("recreated", existing.CurrentEntity.Prop);
    }

    [Theory]
    [InlineData(AuditEntityState.Added, AuditEntityState.Added)]
    [InlineData(AuditEntityState.Modified, AuditEntityState.Added)]
    [InlineData(AuditEntityState.Deleted, AuditEntityState.Modified)]
    [InlineData(AuditEntityState.Deleted, AuditEntityState.Deleted)]
    public void When_Transition_Cannot_Happen_In_One_Transaction_Should_Throw(AuditEntityState from, AuditEntityState to)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EntityChangeUpdateOperationFactory.Create(new EntityStateTransition(from, to)));

        Assert.Contains("Unexpected entity state transition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_Update_Context_Is_Built_For_An_Entity_That_Was_Not_Collected_Should_Throw()
    {
        var changes = new Dictionary<string, IInternalEntityChange>();
        var collected = Change("a", "b", AuditEntityState.Modified);

        Assert.Throws<InvalidOperationException>(() => new EntityChangeUpdateContext(changes, collected, Identifier));
        Assert.Throws<ArgumentNullException>(() => new EntityChangeUpdateContext(null, collected, Identifier));
        Assert.Throws<ArgumentNullException>(() => new EntityChangeUpdateContext(changes, null, Identifier));
        Assert.Throws<ArgumentNullException>(() => new EntityChangeUpdateContext(changes, collected, null));
    }
}
