using ManagementApp.Domain.Common;
using ManagementApp.Domain.Messaging;

namespace ManagementApp.Domain.UnitTests.Messaging;

public class EmailMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ANewEmail_IsPendingAndDueNow()
    {
        var email = NewEmail();

        email.Status.ShouldBe(EmailStatus.Pending);
        email.NextAttemptAt.ShouldBe(Now);
        email.Attempts.ShouldBe(0);
        email.To.ShouldBe("client@example.com");
        email.ReplyTo.ShouldBeNull();
    }

    [Fact]
    public void Sending_RecordsWhenAndTheProvidersId()
    {
        var email = NewEmail();

        email.MarkSent(Now.AddSeconds(5), "<abc@mail>");

        email.Status.ShouldBe(EmailStatus.Sent);
        email.SentAt.ShouldBe(Now.AddSeconds(5));
        email.ProviderMessageId.ShouldBe("<abc@mail>");
        email.Attempts.ShouldBe(1);
    }

    [Fact]
    public void FailedAttempts_AreRetriedLater_ThenGivenUp()
    {
        var email = NewEmail();
        var expectedDelays = new[] { 1, 5, 15, 60 };

        for (var attempt = 0; attempt < expectedDelays.Length; attempt++)
        {
            email.MarkAttemptFailed(Now, "Connection refused");
            email.Status.ShouldBe(EmailStatus.Pending);
            email.NextAttemptAt.ShouldBe(Now.AddMinutes(expectedDelays[attempt]));
        }

        email.MarkAttemptFailed(Now, "Connection refused");

        email.Status.ShouldBe(EmailStatus.Failed);
        email.Attempts.ShouldBe(EmailMessage.MaxAttempts);
        email.LastError.ShouldBe("Connection refused");
    }

    [Fact]
    public void LongErrors_AreCut()
    {
        var email = NewEmail();

        email.MarkAttemptFailed(Now, new string('x', 5000));

        email.LastError!.Length.ShouldBe(EmailMessage.ErrorMaxLength);
    }

    [Fact]
    public void OnlyFailedEmails_CanBeRetried()
    {
        var email = NewEmail();
        Should.Throw<DomainRuleException>(() => email.Retry(Now));

        for (var i = 0; i < EmailMessage.MaxAttempts; i++)
        {
            email.MarkAttemptFailed(Now, "Down");
        }

        email.Retry(Now.AddHours(2));

        email.Status.ShouldBe(EmailStatus.Pending);
        email.Attempts.ShouldBe(0);
        email.NextAttemptAt.ShouldBe(Now.AddHours(2));
    }

    [Fact]
    public void EmailsBelongToACompany()
    {
        Should.Throw<ArgumentException>(() =>
            EmailMessage.Create(Guid.Empty, "a@example.com", "Acme", null, "Subject", "Body", Now));
    }

    private static EmailMessage NewEmail() =>
        EmailMessage.Create(Guid.NewGuid(), " client@example.com ", "Northwind Studio", " ", "Invoice INV-0001", "Hello", Now);
}
