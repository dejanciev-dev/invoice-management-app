using FluentValidation;
using ManagementApp.Application.Accounts;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Application.Customers;
using ManagementApp.Application.Emails;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using ManagementApp.Application.Members;
using ManagementApp.Application.Platform;
using ManagementApp.Application.Products;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<SubscriptionGuard>();
        services.AddScoped<TenantClock>();

        services.AddScoped<GetInvoicesHandler>();
        services.AddScoped<GetInvoiceHandler>();
        services.AddScoped<GetInvoiceStatsHandler>();
        services.AddScoped<GetInvoicePdfHandler>();
        services.AddScoped<InvoicePdfService>();
        services.AddScoped<ExportInvoicesHandler>();
        services.AddScoped<CreateInvoiceHandler>();
        services.AddScoped<UpdateInvoiceHandler>();
        services.AddScoped<DeleteInvoiceHandler>();
        services.AddScoped<IssueInvoiceHandler>();
        services.AddScoped<VoidInvoiceHandler>();
        services.AddScoped<RecordPaymentHandler>();
        services.AddScoped<DeletePaymentHandler>();
        services.AddScoped<IssueCreditNoteHandler>();

        services.AddScoped<GetCustomersHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<DeleteCustomerHandler>();

        services.AddScoped<GetProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();

        services.AddScoped<SignupHandler>();
        services.AddScoped<GetSessionHandler>();

        services.AddScoped<GetCurrentTenantHandler>();
        services.AddScoped<UpdateTenantSettingsHandler>();
        services.AddScoped<TenantLogoHandler>();

        services.AddScoped<GetMembersHandler>();
        services.AddScoped<CreateMemberHandler>();
        services.AddScoped<UpdateMemberHandler>();
        services.AddScoped<SetMemberStatusHandler>();
        services.AddScoped<ResetMemberPasswordHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<GetInvitationsHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();

        services.AddScoped<GetAuditLogsHandler>();
        services.AddScoped<GetFinancialSummaryHandler>();

        services.AddScoped<EmailOutbox>();
        services.AddScoped<GetEmailsHandler>();
        services.AddScoped<RetryEmailHandler>();

        services.AddScoped<GetPlatformDashboardHandler>();
        services.AddScoped<GetTenantsHandler>();
        services.AddScoped<GetTenantDetailsHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<ChangeTenantStatusHandler>();
        services.AddScoped<UpdateSubscriptionHandler>();
        services.AddScoped<DeleteTenantHandler>();
        services.AddScoped<GetPlansHandler>();
        services.AddScoped<GetPlatformUsersHandler>();
        services.AddScoped<SetPlatformUserStatusHandler>();
        services.AddScoped<ImpersonationHandler>();

        return services;
    }
}
