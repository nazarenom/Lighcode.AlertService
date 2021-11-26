using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Lightcode.AlertService
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

                SendMail(alertServiceOptions.ToAddresses, null, subject, messageBody);

            }
            catch (Exception e)
            {
                logger.LogError(e, "SafelySendAlert Error");
            }
        }

        public void SendMail(string toAddress, string ccAddress, string subject, string messageBody, bool isBodyHtml = false, string attachmentFilesPaths = null)
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
                    using var smtpClient = new SmtpClient(alertServiceOptions.SmtpServer, alertServiceOptions.SmtpPort);
                    if (!alertServiceOptions.UseDefaultCredentials)
                    {
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.Credentials = new NetworkCredential(alertServiceOptions.Username, alertServiceOptions.Password);
                    }

                    if (alertServiceOptions.EnableSsl)
                        smtpClient.EnableSsl = alertServiceOptions.EnableSsl;

                    ServicePointManager.SecurityProtocol = alertServiceOptions.SecurityProtocol.ToLower() switch
                    {
                        "tls" => SecurityProtocolType.Tls,
                        "tls11" => SecurityProtocolType.Tls11,
                        "tls12" => SecurityProtocolType.Tls12,
                        _ => SecurityProtocolType.SystemDefault
                    };

                    var mailMessage = new MailMessage(alertServiceOptions.FromAddress, toAddress)
                    {
                        Subject = subject,
                        Body = messageBody,
                        IsBodyHtml = false
                    };

                    if (!string.IsNullOrWhiteSpace(ccAddress))
                    {
                        mailMessage.CC.Add(ccAddress);
                    }

                    if (!string.IsNullOrWhiteSpace(attachmentFilesPaths))
                    {
                        string[] files = attachmentFilesPaths.Split(',');
                        foreach (string file in files)
                        {
                            mailMessage.Attachments.Add(new Attachment(file));
                        }
                    }

                    smtpClient.Send(mailMessage);
                });

        }
    }
}
