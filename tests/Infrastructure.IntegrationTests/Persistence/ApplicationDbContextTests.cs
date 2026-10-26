using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Infrastructure.IntegrationTests.Persistence;

public sealed class ApplicationDbContextTests : IDisposable
{
    private const string UserId = "00000000-0000-0000-0000-000000000000";

    private readonly DateTimeOffset _now = new(2026, 2, 2, 9, 30, 0, TimeSpan.Zero);
    private readonly ApplicationDbContext _sut;

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _sut = new ApplicationDbContext(options, new FixedCurrentUser(UserId), new FakeTimeProvider(_now));

        _sut.Invoices.Add(NewInvoice(1));
        _sut.Invoices.Add(NewInvoice(3));
        _sut.SaveChanges();
    }

    [Fact]
    public async Task SaveChangesAsync_GivenNewInvoice_ShouldSetCreatedProperties()
    {
        var invoice = NewInvoice(2);

        _sut.Invoices.Add(invoice);
        await _sut.SaveChangesAsync();

        invoice.Created.ShouldBe(_now);
        invoice.CreatedBy.ShouldBe(UserId);
    }

    [Fact]
    public async Task SaveChangesAsync_GivenExistingInvoice_ShouldSetLastModifiedProperties()
    {
        var invoice = await _sut.Invoices.FindAsync(1);
        invoice.ShouldNotBeNull();

        invoice.AmountPaid = 10m;
        await _sut.SaveChangesAsync();

        invoice.LastModified.ShouldBe(_now);
        invoice.LastModifiedBy.ShouldBe(UserId);
    }

    [Fact]
    public async Task SaveChangesAsync_GivenRemovedInvoice_ShouldDeleteIt()
    {
        var invoice = await _sut.Invoices.FindAsync(3);
        invoice.ShouldNotBeNull();

        _sut.Invoices.Remove(invoice);
        await _sut.SaveChangesAsync();

        (await _sut.Invoices.FindAsync(3)).ShouldBeNull();
    }

    public void Dispose() => _sut.Dispose();

    private static Invoice NewInvoice(int id) => new()
    {
        Id = id,
        OwnerId = UserId,
        InvoiceNumber = $"INV-{id:000}",
        From = "Acme Ltd",
        To = "Globex Corporation",
        IssueDate = new DateOnly(2026, 2, 1)
    };

    private sealed class FixedCurrentUser(string userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
    }
}
