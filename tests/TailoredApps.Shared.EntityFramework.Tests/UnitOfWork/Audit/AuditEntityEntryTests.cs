using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit;

public class AuditEntityEntryTests
{
    public class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; }
    }

    public class OrderLine
    {
        public int OrderId { get; set; }
        public int Position { get; set; }
        public string Product { get; set; }
    }

    public class Note
    {
        public string Text { get; set; }
    }

    private sealed class ShopContext : DbContext
    {
        public ShopContext()
            : base(new DbContextOptionsBuilder<ShopContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
        {
        }

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderLine>().HasKey(line => new { line.OrderId, line.Position });

            // The key of a note exists only in the model (a shadow property), not on the class.
            modelBuilder.Entity<Note>().Property<int>("NoteId");
            modelBuilder.Entity<Note>().HasKey("NoteId");
        }
    }

    [Fact]
    public void When_Entry_Is_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditEntityEntry(null));
    }

    [Fact]
    public void When_Entity_Is_Added_Should_Snapshot_Its_Values_Type_And_State()
    {
        // arrange
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);

        // act
        var entry = AuditEntityEntry.Create(context.Entry(order));

        // assert
        Assert.Equal(AuditEntityState.Added, entry.EntityState);
        Assert.Equal(typeof(Order), entry.EntityType);
        var current = Assert.IsType<Order>(entry.CurrentEntity);
        Assert.NotSame(order, current);
        Assert.Equal("Alice", current.Customer);
        Assert.Equal(5, current.Id);
    }

    [Fact]
    public void When_Entity_Is_Modified_Should_Keep_Original_And_Current_Values_Apart()
    {
        // arrange
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);
        context.SaveChanges();
        order.Customer = "Bob";
        context.ChangeTracker.DetectChanges();

        // act
        var entry = AuditEntityEntry.Create(context.Entry(order));

        // assert
        Assert.Equal(AuditEntityState.Modified, entry.EntityState);
        Assert.Equal("Alice", Assert.IsType<Order>(entry.OriginalEntity).Customer);
        Assert.Equal("Bob", Assert.IsType<Order>(entry.CurrentEntity).Customer);
    }

    [Fact]
    public void When_Snapshot_Is_Taken_Should_Not_Follow_Later_Changes_Of_The_Entity()
    {
        // arrange
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);
        var entry = AuditEntityEntry.Create(context.Entry(order));

        // act
        order.Customer = "Changed later";

        // assert
        Assert.Equal("Alice", ((Order)entry.CurrentEntity).Customer);
    }

    [Fact]
    public void When_Entity_Is_Deleted_Should_Report_Deleted_State()
    {
        // arrange
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);
        context.SaveChanges();
        context.Orders.Remove(order);

        // act
        var entry = AuditEntityEntry.Create(context.Entry(order));

        // assert
        Assert.Equal(AuditEntityState.Deleted, entry.EntityState);
    }

    [Fact]
    public void When_Key_Is_Single_Should_Build_Identifier_From_Type_Name_And_Key()
    {
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);

        var entry = AuditEntityEntry.Create(context.Entry(order));

        Assert.Equal("Order_5", entry.GetPrimaryKeyStringIdentifier());
        Assert.Equal(new Dictionary<string, object> { ["Id"] = 5 }, entry.GetPrimaryKeys());
    }

    [Fact]
    public void When_Key_Is_Composite_Should_Include_Every_Part_In_Key_Order()
    {
        using var context = new ShopContext();
        var line = new OrderLine { OrderId = 5, Position = 2, Product = "Tea" };
        context.OrderLines.Add(line);

        var entry = AuditEntityEntry.Create(context.Entry(line));

        Assert.Equal("OrderLine_5_2", entry.GetPrimaryKeyStringIdentifier());
        Assert.Equal(new[] { "OrderId", "Position" }, entry.GetPrimaryKeys().Keys.ToArray());
        Assert.Equal(new object[] { 5, 2 }, entry.GetPrimaryKeys().Values.ToArray());
    }

    [Fact]
    public void When_Key_Was_Assigned_After_The_Snapshot_Should_Copy_It_Into_Both_Snapshots()
    {
        // arrange: the snapshot is taken before the save, when a store-generated key is not known yet
        using var context = new ShopContext();
        var order = new Order { Id = 5, Customer = "Alice" };
        context.Orders.Add(order);
        var entry = AuditEntityEntry.Create(context.Entry(order));
        order.Id = 99;

        // act
        entry.SetPrimaryKeys();

        // assert
        Assert.Equal(99, ((Order)entry.CurrentEntity).Id);
        Assert.Equal(99, ((Order)entry.OriginalEntity).Id);
        Assert.Equal("Order_99", entry.GetPrimaryKeyStringIdentifier());
    }

    [Fact]
    public void When_Key_Is_A_Shadow_Property_Should_Skip_It_Instead_Of_Failing()
    {
        // arrange
        using var context = new ShopContext();
        var note = new Note { Text = "remember" };
        context.Notes.Add(note);
        var entry = AuditEntityEntry.Create(context.Entry(note));

        // act
        entry.SetPrimaryKeys();

        // assert
        Assert.Equal("remember", ((Note)entry.CurrentEntity).Text);
        Assert.Equal("Note_", entry.GetPrimaryKeyStringIdentifier());
        Assert.Null(Assert.Single(entry.GetPrimaryKeys()).Value);
    }

    [Fact]
    public void When_Changes_Are_Collected_Should_Return_Entries_Of_Audited_Types_In_Audited_States_Only()
    {
        // arrange
        using var context = new ShopContext();
        var unchanged = new Order { Id = 1, Customer = "Unchanged" };
        var modified = new Order { Id = 2, Customer = "Before" };
        context.Orders.AddRange(unchanged, modified);
        context.SaveChanges();
        modified.Customer = "After";
        context.Orders.Add(new Order { Id = 3, Customer = "New" });
        context.OrderLines.Add(new OrderLine { OrderId = 3, Position = 1, Product = "Not audited" });

        var settings = new Moq.Mock<IAuditSettings>();
        settings.Setup(s => s.TypesToCollect).Returns(new[] { typeof(Order) });
        settings.Setup(s => s.EntityStatesToCollect).Returns(new[] { AuditEntityState.Added, AuditEntityState.Modified });

        // act
        var entries = new AuditChangesCollector<ShopContext>(context, settings.Object).CollectChanges().ToList();

        // assert
        Assert.Equal(
            new[] { "Order_2:Modified", "Order_3:Added" },
            entries.Select(e => $"{e.GetPrimaryKeyStringIdentifier()}:{e.EntityState}").OrderBy(text => text).ToArray());
    }
}
