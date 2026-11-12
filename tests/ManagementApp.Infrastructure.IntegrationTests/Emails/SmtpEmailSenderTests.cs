using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Emails;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ManagementApp.Infrastructure.IntegrationTests.Emails;

/// <summary>Sends through MailKit to a tiny SMTP server in the test, and checks what arrived.</summary>
public sealed class SmtpEmailSenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnEmail_ArrivesWithTheCompanysName_ReplyTo_AndAttachment()
    {
        await using var server = FakeSmtpServer.Start();
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            FromAddress = "invoices@app.example",
            Smtp = new SmtpOptions { Host = "127.0.0.1", Port = server.Port, Security = "None" }
        }));
        var pdf = "%PDF-1.7 test"u8.ToArray();

        var messageId = await sender.SendAsync(new OutgoingEmail(
            "client@example.com",
            "Northwind Studio",
            "accounts@northwind.example",
            "Invoice INV-0001",
            "Please find the invoice attached.",
            [new OutgoingAttachment("INV-0001.pdf", "application/pdf", pdf)]), Ct);

        var received = await server.ReceivedAsync();
        received.Recipients.ShouldBe(["client@example.com"]);
        received.Sender.ShouldBe("invoices@app.example");
        var message = received.Message;
        message.MessageId.ShouldBe(messageId);
        message.From.Mailboxes.Single().ShouldSatisfyAllConditions(
            from => from.Name.ShouldBe("Northwind Studio"),
            from => from.Address.ShouldBe("invoices@app.example"));
        message.ReplyTo.Mailboxes.Single().Address.ShouldBe("accounts@northwind.example");
        message.Subject.ShouldBe("Invoice INV-0001");
        message.TextBody!.Trim().ShouldBe("Please find the invoice attached.");
        var attachment = message.Attachments.OfType<MimePart>().ShouldHaveSingleItem();
        attachment.FileName.ShouldBe("INV-0001.pdf");
        attachment.ContentType.MimeType.ShouldBe("application/pdf");
        using var content = new MemoryStream();
        await attachment.Content!.DecodeToAsync(content, Ct);
        content.ToArray().ShouldBe(pdf);
    }

    [Fact]
    public async Task AnUnreachableServer_Fails()
    {
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            FromAddress = "invoices@app.example",
            Smtp = new SmtpOptions { Host = "127.0.0.1", Port = FreePort(), Security = "None" }
        }));

        await Should.ThrowAsync<Exception>(() => sender.SendAsync(
            new OutgoingEmail("client@example.com", "Acme", null, "Hi", "Body", []), Ct));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Just enough SMTP to accept one message: greeting, EHLO, MAIL, RCPT, DATA and QUIT.</summary>
    private sealed class FakeSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task<Received> _session;

        private FakeSmtpServer()
        {
            _listener.Start();
            _session = Task.Run(AcceptAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public static FakeSmtpServer Start() => new();

        public Task<Received> ReceivedAsync() => _session.WaitAsync(TimeSpan.FromSeconds(10));

        private async Task<Received> AcceptAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            string? sender = null;
            var recipients = new List<string>();
            MimeMessage? message = null;
            await writer.WriteLineAsync("220 localhost ESMTP test");
            while (await reader.ReadLineAsync() is { } line)
            {
                var command = line.ToUpperInvariant();
                if (command.StartsWith("EHLO") || command.StartsWith("HELO"))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (command.StartsWith("MAIL FROM:"))
                {
                    sender = Address(line);
                    await writer.WriteLineAsync("250 OK");
                }
                else if (command.StartsWith("RCPT TO:"))
                {
                    recipients.Add(Address(line));
                    await writer.WriteLineAsync("250 OK");
                }
                else if (command == "DATA")
                {
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var data = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                        data.Append(dataLine.StartsWith("..") ? dataLine[1..] : dataLine).Append("\r\n");
                    }

                    message = MimeMessage.Load(new MemoryStream(Encoding.ASCII.GetBytes(data.ToString())));
                    await writer.WriteLineAsync("250 OK queued");
                }
                else if (command == "QUIT")
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }

            return new Received(sender!, recipients, message!);
        }

        private static string Address(string line) => line[(line.IndexOf('<') + 1)..line.IndexOf('>')];

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _session;
            }
            catch
            {
                // The test already failed or finished; the listener is stopped either way.
            }
        }
    }

    private sealed record Received(string Sender, IReadOnlyList<string> Recipients, MimeMessage Message);
}
