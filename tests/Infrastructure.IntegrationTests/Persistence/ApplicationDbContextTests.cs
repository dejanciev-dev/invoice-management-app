using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Infrastructure.IntegrationTests.Persistence
{
    public class ApplicationDbContextTests : IDisposable
    {
        private readonly string _userId;
        private readonly DateTime _dateTime;
        private readonly Mock<IDateTime> _dateTimeMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;

        private readonly ApplicationDbContext _sut;

        public ApplicationDbContextTests()
        {
            _dateTime = new DateTime(2011, 2, 2);
            _dateTimeMock = new Mock<IDateTime>();
            _dateTimeMock.Setup(m => m.Now).Returns(_dateTime);

            _userId = "00000000-0000-0000-0000-000000000000";
            _currentUserServiceMock = new Mock<ICurrentUserService>();
            _currentUserServiceMock.Setup(m => m.UserId).Returns(_userId);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
              .UseInMemoryDatabase(Guid.NewGuid().ToString())
              .Options;

            _sut = new ApplicationDbContext(options, _currentUserServiceMock.Object, _dateTimeMock.Object);

            _sut.Invoices.Add(new Invoice
            {
                Id = 1
            });
            _sut.Invoices.Add(new Invoice
            {
                Id = 3//delete invoice
            });

            _sut.SaveChanges();
        }

        [Fact]
        public async Task SaveChangesAsync_GivenNewInvoice_ShouldSetCreatedProperties()
        {
            var invoice = new Invoice
            {
                Id = 2
            };

            _sut.Invoices.Add(invoice);

            await _sut.SaveChangesAsync();

            invoice.Created.ShouldBe(_dateTime);
            invoice.CreatedBy.ShouldBe(_userId);
        }

        [Fact]
        public async Task SaveChangesAsync_GivenExistingInvoice_ShouldSetLastModifiedProperties()
        {
            int id = 1;
            var invoice = await _sut.Invoices.FindAsync(id);
            invoice.AmountPaid = 10.0;
            await _sut.SaveChangesAsync();

            invoice.ShouldNotBeNull();
            invoice.LastModified.ShouldBe(_dateTime);
            invoice.LastModifiedBy.ShouldBe(_userId);
        }

        [Fact]
        public async Task SaveChangesAsync_GivenExistingInvoice_ShouldDeleteGivenInvoice()
        {
            int id = 3;
            var invoice = await _sut.Invoices.FindAsync(id);
            _sut.Invoices.Remove(invoice);

            await _sut.SaveChangesAsync();

            invoice = await _sut.Invoices.FindAsync(id);

            invoice.ShouldBeNull();
        }

        [Fact]
        public void Database_DeleteDatabase()
        {
            Assert.True(_sut.Database.EnsureDeleted());
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }
    }
}