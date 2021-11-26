using System;

namespace Lightcode.AlertService
{
    public interface IAlertService
    {
        void SafelySendAlert(Exception exception, Guid? guid = null);
        void SafelySendAlert(string messageBody, Guid? guid = null);
        void SendMail(string toAddress, string ccAddress, string subject, string messageBody, bool isBodyHtml = false, string attachmentFilesPaths = null);
    }

    public class AlertServiceOptions
    {
        public bool SendEmail { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string FromAddress { get; set; }
        public string ToAddresses { get; set; }
        public string Subject { get; set; }
        public bool UseDefaultCredentials { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; }
        public string SecurityProtocol { get; set; }
    }

}
