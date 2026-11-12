using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Emails;

public sealed record EmailDto(
    int Id,
    string To,
    string Subject,
    EmailStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    string? RelatedEntityType,
    string? RelatedEntityId,
    IReadOnlyList<string> Attachments);

public sealed record GetEmailsQuery(
    EmailStatus? Status = null, string? RelatedEntityType = null, string? RelatedEntityId = null, int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;
}

/// <summary>The current company's emails, newest first: what was sent, what is waiting, and what failed and why.</summary>
public sealed class GetEmailsHandler(IApplicationDbContext context)
{
    public async Task<PagedResult<EmailDto>> HandleAsync(GetEmailsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, GetEmailsQuery.MaxPageSize);

        // Scoped to the current tenant by the query filter.
        IQueryable<EmailMessage> emails = context.EmailMessages.AsNoTracking();
        if (query.Status is { } status)
        {
            emails = emails.Where(e => e.Status == status);
        }

        if (query.RelatedEntityType is { } type)
        {
            emails = emails.Where(e => e.RelatedEntityType == type && e.RelatedEntityId == query.RelatedEntityId);
        }

        var total = await emails.CountAsync(cancellationToken);
        var items = await emails
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailDto(
                e.Id,
                e.To,
                e.Subject,
                e.Status,
                e.Attempts,
                e.CreatedAt,
                e.SentAt,
                e.Status == EmailStatus.Pending ? e.NextAttemptAt : null,
                e.LastError,
                e.RelatedEntityType,
                e.RelatedEntityId,
                e.Attachments.Select(a => a.FileName).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailDto>(items, total, page, pageSize);
    }
}

/// <summary>Queues a failed email again, e.g. after the mail settings were fixed.</summary>
public sealed class RetryEmailHandler(IApplicationDbContext context, IAuditLogger auditLogger, TimeProvider timeProvider)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var email = await context.EmailMessages.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmailMessage), id);

        email.Retry(timeProvider.GetUtcNow());
        auditLogger.Add(new AuditEntry(AuditActions.EmailRetried)
        {
            EntityType = nameof(EmailMessage),
            EntityId = email.Id.ToString(),
            Details = new { email.To, email.Subject }
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
