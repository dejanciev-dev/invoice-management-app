using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Emails;
using ManagementApp.Infrastructure.Auditing;
using ManagementApp.Infrastructure.BackgroundJobs;
using ManagementApp.Infrastructure.Files;
using ManagementApp.Infrastructure.Files.Storage;
using ManagementApp.Infrastructure.Identity;
using ManagementApp.Infrastructure.Emails;
using ManagementApp.Infrastructure.Persistence;
using ManagementApp.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlServer => sqlServer.EnableRetryOnFailure()));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<TenantAccessResolver>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<INotificationSender, OutboxNotificationSender>();

        services.AddIdentityApiEndpoints<ApplicationUser>(options =>
            {
                // Makes Identity consult ActiveUserConfirmation on sign-in, which refuses disabled accounts.
                options.SignIn.RequireConfirmedAccount = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IUserConfirmation<ApplicationUser>, ActiveUserConfirmation>();
        services.AddScoped<IIdentityService, IdentityService>();

        // The cookie is re-issued from the database periodically; keep the impersonation claims when that happens,
        // otherwise an impersonation would silently turn into a normal session as the target user.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.OnRefreshingPrincipal = context =>
            {
                var current = context.CurrentPrincipal?.Identities.FirstOrDefault();
                var refreshed = context.NewPrincipal?.Identities.FirstOrDefault();
                if (current is not null && refreshed is not null)
                {
                    foreach (var claim in current.Claims.Where(c => AppClaimTypes.Impersonation.Contains(c.Type)))
                    {
                        refreshed.AddClaim(new System.Security.Claims.Claim(claim.Type, claim.Value));
                    }
                }

                return Task.CompletedTask;
            });

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.AddAppUrls(configuration);
        services.AddTransient<ICsvFileBuilder, CsvFileBuilder>();
        services.AddFileStorage(configuration);
        services.AddEmail(configuration);
        services.AddSingleton<IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();

        services.AddSingleton<TenantJobRunner>();
        services.AddSingleton<OverdueInvoiceReminderJob>();
        services.AddSingleton<SubscriptionExpiryJob>();
        services.AddHostedService<BackgroundJobScheduler>();

        return services;
    }
}
