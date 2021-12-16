using System;
using System.Linq;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;

namespace LightCode.AlertService
{
    public class AlertService : IAlertService
    {
        private readonly ILogger<AlertService> logger;
        private readonly AlertServiceOptions alertServiceOptions;


        public AlertService(ILogger<AlertService> logger, IOptions<AlertServiceOptions> alertServiceOptions)
        {
            this.logger = logger;
            this.alertServiceOptions = alertServiceOptions.Value;
        }

        public void SafelySendAlert(Exception exception, Guid? guid = null)
        {
            try
            {
                logger.LogError(exception, $"SafelySendAlert {guid}");
                SafelySendAlert(exception.ToString(), guid);
            }
            catch (Exception e)
            {
                logger.LogError(e, "SafelySendAlert Error");
            }

        }

        public void SafelySendAlert(string messageBody, Guid? guid = null)
        {
            try
            {
                if (!alertServiceOptions.SendEmail) return;

                var subject = $"[{Environment.MachineName}] - {alertServiceOptions.Subject}";
                if (guid.HasValue)
                    subject += $" - {guid}";

                SendMail(alertServiceOptions.ToAddresses, null, subject, messageBody, false);

            }
            catch (Exception e)
            {
                logger.LogError(e, "SafelySendAlert Error");
            }
        }

        public void SendMail(string toAddress, string ccAddress, string subject, string messageBody, bool isBodyHtml, string attachmentFilesPaths = null)
        {
            Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3)
                }, (ex, span, retryCount, context) =>
                {
                    logger.LogInformation(
                        $"AlertService.SendMail: Retry [{retryCount}]: exception: [{ex}]");
                }).Execute(() =>
                {
                    using (var smtpClient = new SmtpClient())
                    {
                        smtpClient.Connect(alertServiceOptions.SmtpServer, alertServiceOptions.SmtpPort, alertServiceOptions.EnableSsl);
                        if (!alertServiceOptions.UseDefaultCredentials)
                        {
                            smtpClient.Authenticate(alertServiceOptions.Username, alertServiceOptions.Password);
                        }

                        smtpClient.SslProtocols = alertServiceOptions.SecurityProtocol.ToLower() switch
                        {
                            "tls" => SslProtocols.Tls,
                            "tls11" => SslProtocols.Tls11,
                            "tls12" => SslProtocols.Tls12,
                            _ => SslProtocols.None
                        };

                        var mailMessage = new MimeMessage();
                        mailMessage.From.Add(new MailboxAddress(alertServiceOptions.FromAddress,alertServiceOptions.FromAddress));
                        mailMessage.To.AddRange(alertServiceOptions.FromAddress.Split(",")
                            .Select(address => new MailboxAddress(address,address)));
                        if (!string.IsNullOrWhiteSpace(ccAddress))
                        {
                            mailMessage.Cc.AddRange(ccAddress.Split(",")
                                .Select(address => new MailboxAddress(address,address)));
                        }

                        mailMessage.Subject = subject;
                        
                        var builder = new BodyBuilder ();
                        if (isBodyHtml)
                            builder.TextBody = messageBody;
                        else
                            builder.HtmlBody = messageBody;


                        if (!string.IsNullOrWhiteSpace(attachmentFilesPaths))
                        {
                            var files = attachmentFilesPaths.Split(',');
                            foreach (var file in files)
                            {
                                builder.Attachments.Add(file);
                            }
                        }

                        smtpClient.Send(mailMessage);
                    }
                });

        }
    }
}
