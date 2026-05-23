using DormitoryManagement.Infrastructure.Data;
using DormitoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagement.Infrastructure.Tests;

public sealed class DormitoryDbContextTests
{
    [Fact]
    public void DbContext_Model_CanBeCreated()
    {
        var options = new DbContextOptionsBuilder<DormitoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DormitoryDbContext(options);

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(DormitoryManagement.Domain.Entities.Student)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(DormitoryManagement.Domain.Entities.PendingAccountRegistration)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(PaymentExtension)));
        var invoice = dbContext.Model.FindEntityType(typeof(Invoice));
        Assert.NotNull(invoice?.FindProperty(nameof(Invoice.InvoiceKind)));
        Assert.NotNull(invoice?.FindProperty(nameof(Invoice.ContractId)));
        var invoicePeriodIndex = Assert.Single(invoice!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(Invoice.StudentId),
                nameof(Invoice.RoomId),
                nameof(Invoice.BillingPeriod),
                nameof(Invoice.InvoiceKind)
            }));
        Assert.True(invoicePeriodIndex.IsUnique);
        Assert.Equal("[InvoiceKind] = 'MonthlyUtility'", invoicePeriodIndex.GetFilter());
        var forumLike = dbContext.Model.FindEntityType(typeof(ForumLike));
        Assert.NotNull(forumLike);
        Assert.Contains(forumLike!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ForumLike.UserId),
                nameof(ForumLike.ThreadId)
            }));
        Assert.Contains(forumLike.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ForumLike.UserId),
                nameof(ForumLike.CommentId)
            }));
    }
}
