namespace ManagementApp.Infrastructure.Emails;

/// <summary>The <c>Email</c> configuration section.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary><c>Log</c> (the default: emails are only written to the log) or <c>Smtp</c>.</summary>
    public string Provider { get; set; } = "Log";

    /// <summary>The address every email is sent from; the company's name is shown as the sender's name.</summary>
    public string FromAddress { get; set; } = "no-reply@localhost";

    /// <summary>How often the outbox worker looks for emails to send.</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>Set to false to stop the worker (the tests run the outbox processor themselves).</summary>
    public bool WorkerEnabled { get; set; } = true;

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary><c>Auto</c>, <c>StartTls</c> (port 587), <c>SslOnConnect</c> (port 465) or <c>None</c> (local test servers only).</summary>
    public string Security { get; set; } = "Auto";

    public string? Username { get; set; }
    public string? Password { get; set; }
}
