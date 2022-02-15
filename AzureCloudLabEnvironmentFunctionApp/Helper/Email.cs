using System.IO;
using System.Net;
using System.Net.Mail;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Helper;

public class Email
{
    private readonly SmtpClient _client;
    private readonly string _environment;
    private readonly string _fromAddress;
    private readonly ILogger _log;

    public Email(Config config, ILogger log)
    {
        _log = log;
        //https://stackoverflow.com/questions/18503333/the-smtp-server-requires-a-secure-connection-or-the-client-was-not-authenticated

        var smtp = config.GetConfig(Config.Key.EmailSmtp);
        var loginName = config.GetConfig(Config.Key.EmailUserName);
        var password = config.GetConfig(Config.Key.EmailPassword);
        _fromAddress = config.GetConfig(Config.Key.EmailFromAddress);
        _environment = config.GetConfig(Config.Key.Environment);

        if (string.IsNullOrEmpty(smtp) || string.IsNullOrEmpty(loginName) || string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(_fromAddress))
        {
            _log.LogInformation("Missing SMTP Settings in App Settings!");
            return;
        }

        _client = new SmtpClient(smtp, 587);
        _client.EnableSsl = true;
        _client.UseDefaultCredentials = false;
        _client.DeliveryMethod = SmtpDeliveryMethod.Network;
        _client.Credentials = new NetworkCredential(loginName, password);
    }

    public void Send(EmailMessage email, Attachment[] attachments)
    {
        if (_client == null)
        {
            _log.LogInformation("Skipped Missing SMTP Settings in App Settings: " + email.To);
            return;
        }

        var body = email.Body;
        if (!string.IsNullOrEmpty(_environment))
            body += "\n\n (environment:" + _environment + ")";

        var message = new MailMessage(_fromAddress, email.To, email.Subject, body);

        if (attachments != null)
            foreach (var attachment in attachments)
                message.Attachments.Add(attachment);

        _client.Send(message);
        _log.LogInformation("Sent email to " + email.To);
    }

    public static Attachment StringToAttachment(string content, string name, string mediaType)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, leaveOpen: true);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        return new Attachment(new MemoryStream(ms.ToArray()), name, mediaType);
    }
}