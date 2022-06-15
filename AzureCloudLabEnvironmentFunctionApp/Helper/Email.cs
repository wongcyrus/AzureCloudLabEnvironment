using System.IO;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

using Azure.Communication.Email;
using Azure.Communication.Email.Models;
using System.Collections.Generic;
using System.Threading;
using EmailMessage = Azure.Communication.Email.Models.EmailMessage;
using System;

namespace AzureCloudLabEnvironment.Helper;

public class Email
{
    private readonly SmtpClient _client;
    private EmailClient _azureEmailClient;
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


        if (string.IsNullOrEmpty(config.GetConfig(Config.Key.CommunicationServiceConnectionString)))
        {
            _client = new SmtpClient(smtp, 587);
            _client.EnableSsl = true;
            _client.UseDefaultCredentials = false;
            _client.DeliveryMethod = SmtpDeliveryMethod.Network;
            _client.Credentials = new NetworkCredential(loginName, password);
        }
        else
        {
            _azureEmailClient = new EmailClient(config.GetConfig(Config.Key.CommunicationServiceConnectionString));
        }

    }

    public void Send(Model.EmailMessage email, Model.Attachment[] attachments)
    {
        var body = email.Body;
        if (!string.IsNullOrEmpty(_environment))
            body += "\n\n (environment:" + _environment + ")";
        if (_azureEmailClient != null)
        {
            EmailContent emailContent = new EmailContent(email.Subject);
            emailContent.PlainText = body;
            List<EmailAddress> emailAddresses = new List<EmailAddress> { new EmailAddress(email.To) };
            EmailRecipients emailRecipients = new EmailRecipients(emailAddresses);
            var emailMessage = new EmailMessage(_fromAddress, emailContent, emailRecipients);
            if (attachments != null)
                foreach (var attachment in attachments)
                    emailMessage.Attachments.Add(StringToEmailAttachment(attachment));            
            _azureEmailClient.Send(emailMessage, CancellationToken.None);
            return;
        }
        else
        {
            if (_client == null)
            {
                _log.LogInformation("Skipped Missing SMTP Settings in App Settings: " + email.To);
                return;
            }

            var message = new MailMessage(_fromAddress, email.To, email.Subject, body);

            if (attachments != null)
                foreach (var attachment in attachments)
                    message.Attachments.Add(StringToAttachment(attachment));
            _client.Send(message);
        }
        _log.LogInformation("Sent email to " + email.To);
    }

    private static System.Net.Mail.Attachment StringToAttachment(Model.Attachment attachment)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, leaveOpen: true);
        writer.Write(attachment.Content);
        writer.Flush();
        ms.Position = 0;
        return new System.Net.Mail.Attachment(new MemoryStream(ms.ToArray()), attachment.Name, attachment.MediaType);
    }

    private static EmailAttachment StringToEmailAttachment(Model.Attachment attachment)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, leaveOpen: true);
        writer.Write(attachment.Content);
        writer.Flush();
        ms.Position = 0;
        return new EmailAttachment(attachment.Name, attachment.MediaType, Convert.ToBase64String(new MemoryStream(ms.ToArray()).ToArray()));
    }
}