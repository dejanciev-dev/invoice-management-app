using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagementApp.Infrastructure.Persistence;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string DemoUserEmail = "demo@example.com";
    public const string DemoUserPassword = "Demo123!";

    public async Task InitialiseAsync()
    {
        try
        {
            if (context.Database.IsSqlServer())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        if (!configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            return;
        }

        var demoUser = await userManager.FindByEmailAsync(DemoUserEmail);
        if (demoUser is null)
        {
            demoUser = new ApplicationUser { UserName = DemoUserEmail, Email = DemoUserEmail };
            await userManager.CreateAsync(demoUser, DemoUserPassword);
        }

        if (!await context.Invoices.AnyAsync(i => i.OwnerId == demoUser.Id))
        {
            context.Invoices.AddRange(CreateDemoInvoices(demoUser.Id));
            await context.SaveChangesAsync();
        }
    }

    private IEnumerable<Invoice> CreateDemoInvoices(string ownerId)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        const string from = "Northwind Studio\n42 Harbour Street\nLondon, UK";

        Invoice NewInvoice(string number, string to, int issuedDaysAgo, int dueInDays) => new()
        {
            OwnerId = ownerId,
            InvoiceNumber = number,
            From = from,
            To = to,
            IssueDate = today.AddDays(-issuedDaysAgo),
            DueDate = today.AddDays(-issuedDaysAgo + dueInDays),
            PaymentTerms = $"Net {dueInDays}",
            Currency = "USD"
        };

        var websiteRedesign = NewInvoice("INV-2026-001", "Contoso Ltd\n1 Microsoft Way\nRedmond, WA", 75, 30);
        websiteRedesign.ReplaceItems(
        [
            new InvoiceItem { Description = "Discovery workshop", Quantity = 1, UnitPrice = 1200 },
            new InvoiceItem { Description = "UI design (hours)", Quantity = 32, UnitPrice = 85 }
        ]);
        websiteRedesign.Tax = 20;
        websiteRedesign.TaxType = TaxType.Percentage;
        websiteRedesign.AmountPaid = websiteRedesign.Total;

        var mobileApp = NewInvoice("INV-2026-002", "Fabrikam Inc.\n500 Market Street\nSan Francisco, CA", 45, 14);
        mobileApp.ReplaceItems(
        [
            new InvoiceItem { Description = "React Native development (hours)", Quantity = 60, UnitPrice = 95 },
            new InvoiceItem { Description = "App Store submission", Quantity = 1, UnitPrice = 350 }
        ]);
        mobileApp.Discount = 10;
        mobileApp.DiscountType = DiscountType.Percentage;

        var hosting = NewInvoice("INV-2026-003", "Adventure Works\n12 Mountain Road\nDenver, CO", 20, 30);
        hosting.ReplaceItems(
        [
            new InvoiceItem { Description = "Managed hosting (months)", Quantity = 3, UnitPrice = 149 },
            new InvoiceItem { Description = "SSL certificate", Quantity = 1, UnitPrice = 60 }
        ]);
        hosting.AmountPaid = 200;

        var consulting = NewInvoice("INV-2026-004", "Tailspin Toys\n8 Lakeside Avenue\nChicago, IL", 3, 30);
        consulting.ReplaceItems(
        [
            new InvoiceItem { Description = "Architecture review", Quantity = 1, UnitPrice = 2400 },
            new InvoiceItem { Description = "Performance audit (hours)", Quantity = 6.5m, UnitPrice = 120 }
        ]);
        consulting.Discount = 150;
        consulting.DiscountType = DiscountType.Flat;
        consulting.Tax = 8.25m;
        consulting.TaxType = TaxType.Percentage;
        consulting.Notes = "Thank you for your business!";

        return [websiteRedesign, mobileApp, hosting, consulting];
    }
}
