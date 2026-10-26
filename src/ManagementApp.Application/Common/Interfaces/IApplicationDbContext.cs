using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Invoice> Invoices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
