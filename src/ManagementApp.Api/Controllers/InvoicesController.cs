using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create(
        CreateInvoiceCommand command,
        [FromServices] CreateInvoiceHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(command, cancellationToken);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> Get(
        [FromServices] GetUserInvoicesHandler handler,
        CancellationToken cancellationToken)
    {
        return Ok(await handler.HandleAsync(cancellationToken));
    }
}
