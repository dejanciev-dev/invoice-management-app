using FluentValidation;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<CreateInvoiceHandler>();
        services.AddScoped<GetUserInvoicesHandler>();

        return services;
    }
}
