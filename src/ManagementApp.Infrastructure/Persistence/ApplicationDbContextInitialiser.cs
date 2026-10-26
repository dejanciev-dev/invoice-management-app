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
    IConfiguration configuration)
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

        if (await userManager.FindByEmailAsync(DemoUserEmail) is null)
        {
            var demoUser = new ApplicationUser { UserName = DemoUserEmail, Email = DemoUserEmail };
            await userManager.CreateAsync(demoUser, DemoUserPassword);
        }
    }
}
