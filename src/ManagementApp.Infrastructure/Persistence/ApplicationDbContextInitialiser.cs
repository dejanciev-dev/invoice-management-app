using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
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
    public const string DemoAdminEmail = "admin@example.com";
    public const string DemoAdminPassword = "Admin123!";
    public const string SecondDemoUserEmail = "owner@contoso.example";

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
        await GrantConfiguredPlatformAdminsAsync();

        if (!configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        var admin = await EnsureUserAsync(DemoAdminEmail, DemoAdminPassword, "Platform Admin");
        if (!admin.IsPlatformAdmin)
        {
            admin.IsPlatformAdmin = true;
            await userManager.UpdateAsync(admin);
        }

        var demoUser = await EnsureUserAsync(DemoUserEmail, DemoUserPassword, "Demo User");
        var northwind = await EnsureTenantAsync(demoUser, "Northwind Studio", TenantRole.TenantAdmin, tenant =>
        {
            tenant.Address = "42 Harbour Street\nLondon, UK";
            tenant.Country = "GB";
            tenant.Email = "billing@northwind.example";
            tenant.TaxNumber = "GB123456789";
            tenant.TimeZone = "Europe/London";
            tenant.InvoiceSettings.NumberPrefix = "INV-2026-";
            tenant.InvoiceSettings.NumberPadding = 3;
            tenant.InvoiceSettings.NextNumber = 5;
            tenant.InvoiceSettings.TaxLabel = "VAT";
            tenant.Subscription = TenantSubscription.Activate(SubscriptionPlans.Professional, BillingCycle.Yearly, now.AddYears(1));
        });

        if (!await context.Invoices.IgnoreQueryFilters().AnyAsync(i => i.TenantId == northwind.Id))
        {
            var customers = CreateDemoCustomers(northwind.Id);
            context.Customers.AddRange(customers.Values);
            await context.SaveChangesAsync(); // the invoices need the customers' ids

            context.Invoices.AddRange(CreateDemoInvoices(northwind.Id, demoUser.Id, customers));
            await context.SaveChangesAsync();
        }

        // A second company shows that tenants are isolated and gives the platform admin someone to impersonate.
        var contosoOwner = await EnsureUserAsync(SecondDemoUserEmail, DemoUserPassword, "Contoso Owner");
        var contoso = await EnsureTenantAsync(contosoOwner, "Contoso Consulting", TenantRole.TenantAdmin, tenant =>
        {
            tenant.Address = "1 Main Street\nSeattle, WA";
            tenant.Country = "US";
            tenant.Email = "accounts@contoso.example";
            tenant.TimeZone = "America/Los_Angeles";
            tenant.Subscription = TenantSubscription.StartTrial(now);
        });

        if (!await context.Invoices.IgnoreQueryFilters().AnyAsync(i => i.TenantId == contoso.Id))
        {
            var litware = new Customer
            {
                TenantId = contoso.Id,
                Name = "Litware Inc.",
                Address = "77 Pine Street\nPortland, OR",
                Email = "ap@litware.example",
                PaymentTermsDays = 30
            };
            context.Customers.Add(litware);
            await context.SaveChangesAsync();

            var invoice = new Invoice
            {
                TenantId = contoso.Id,
                OwnerId = contosoOwner.Id,
                InvoiceNumber = "INV-0001",
                From = contoso.InvoiceSenderDetails(),
                CustomerId = litware.Id,
                To = BillTo(litware),
                IssueDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-10),
                DueDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(20),
                PaymentTerms = "Net 30",
                Currency = "USD"
            };
            invoice.ReplaceItems([new InvoiceItem { Description = "Strategy workshop", Quantity = 2, UnitPrice = 1500 }]);
            context.Invoices.Add(invoice);
            contoso.InvoiceSettings.NextNumber = 2;
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Accounts listed in <c>Platform:AdminEmails</c> become platform admins; the only way to bootstrap one.</summary>
    private async Task GrantConfiguredPlatformAdminsAsync()
    {
        var emails = configuration.GetSection("Platform:AdminEmails").Get<string[]>() ?? [];
        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            var user = await userManager.FindByEmailAsync(email.Trim());
            if (user is { IsPlatformAdmin: false })
            {
                user.IsPlatformAdmin = true;
                await userManager.UpdateAsync(user);
                logger.LogInformation("Granted platform admin to {Email} from configuration", email);
            }
        }
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string password, string displayName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                CreatedAt = timeProvider.GetUtcNow()
            };
            await userManager.CreateAsync(user, password);
        }

        return user;
    }

    private async Task<Tenant> EnsureTenantAsync(ApplicationUser user, string name, TenantRole role, Action<Tenant> configure)
    {
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Name == name);
        if (tenant is null)
        {
            tenant = new Tenant { Name = name };
            configure(tenant);
            context.Tenants.Add(tenant);
        }

        if (!await context.TenantMemberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenant.Id && m.UserId == user.Id))
        {
            context.TenantMemberships.Add(TenantMembership.Create(tenant.Id, user.Id, role));
        }

        await context.SaveChangesAsync();
        return tenant;
    }

    /// <summary>The demo company's customers by a short key. The last one has no invoices yet.</summary>
    private static Dictionary<string, Customer> CreateDemoCustomers(Guid tenantId)
    {
        Customer NewCustomer(string name, string address, string? email = null, string? taxNumber = null, int? termsDays = null) => new()
        {
            TenantId = tenantId,
            Name = name,
            Address = address,
            Email = email,
            TaxNumber = taxNumber,
            PaymentTermsDays = termsDays,
            Currency = "USD"
        };

        return new()
        {
            ["contoso"] = NewCustomer("Contoso Ltd", "1 Microsoft Way\nRedmond, WA", "accounts@contoso.example"),
            ["fabrikam"] = NewCustomer("Fabrikam Inc.", "500 Market Street\nSan Francisco, CA", "ap@fabrikam.example", termsDays: 14),
            ["adventure"] = NewCustomer("Adventure Works", "12 Mountain Road\nDenver, CO"),
            ["tailspin"] = NewCustomer("Tailspin Toys", "8 Lakeside Avenue\nChicago, IL", "billing@tailspin.example", "US-98-7654321"),
            ["wingtip"] = NewCustomer("Wingtip Toys", "3 Harbour Lane\nBrighton, UK", "finance@wingtip.example", "GB987654321", 30)
        };
    }

    /// <summary>The printed "Bill to" snapshot of a demo customer: its name and address.</summary>
    private static string BillTo(Customer customer) =>
        string.Join('\n', new[] { customer.Name, customer.Address }.Where(line => !string.IsNullOrWhiteSpace(line)));

    private IEnumerable<Invoice> CreateDemoInvoices(Guid tenantId, string ownerId, IReadOnlyDictionary<string, Customer> customers)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        const string from = "Northwind Studio\n42 Harbour Street\nLondon, UK";

        Invoice NewInvoice(string number, string customerKey, int issuedDaysAgo, int dueInDays) => new()
        {
            TenantId = tenantId,
            OwnerId = ownerId,
            InvoiceNumber = number,
            From = from,
            CustomerId = customers[customerKey].Id,
            To = BillTo(customers[customerKey]),
            IssueDate = today.AddDays(-issuedDaysAgo),
            DueDate = today.AddDays(-issuedDaysAgo + dueInDays),
            PaymentTerms = $"Net {dueInDays}",
            Currency = "USD"
        };

        var websiteRedesign = NewInvoice("INV-2026-001", "contoso", 75, 30);
        websiteRedesign.ReplaceItems(
        [
            new InvoiceItem { Description = "Discovery workshop", Quantity = 1, UnitPrice = 1200 },
            new InvoiceItem { Description = "UI design (hours)", Quantity = 32, UnitPrice = 85 }
        ]);
        websiteRedesign.Tax = 20;
        websiteRedesign.TaxType = TaxType.Percentage;
        websiteRedesign.AmountPaid = websiteRedesign.Total;

        var mobileApp = NewInvoice("INV-2026-002", "fabrikam", 45, 14);
        mobileApp.ReplaceItems(
        [
            new InvoiceItem { Description = "React Native development (hours)", Quantity = 60, UnitPrice = 95 },
            new InvoiceItem { Description = "App Store submission", Quantity = 1, UnitPrice = 350 }
        ]);
        mobileApp.Discount = 10;
        mobileApp.DiscountType = DiscountType.Percentage;

        var hosting = NewInvoice("INV-2026-003", "adventure", 20, 30);
        hosting.ReplaceItems(
        [
            new InvoiceItem { Description = "Managed hosting (months)", Quantity = 3, UnitPrice = 149 },
            new InvoiceItem { Description = "SSL certificate", Quantity = 1, UnitPrice = 60 }
        ]);
        hosting.AmountPaid = 200;

        var consulting = NewInvoice("INV-2026-004", "tailspin", 3, 30);
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
