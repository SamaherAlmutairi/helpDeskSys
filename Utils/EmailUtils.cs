using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace HelpDesk.Utils
{
    public class EmailUtils
    {
        public static void SendEmail(string recieveAddress, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse("HelpDeskApplication@outlook.com"));
            email.To.Add(MailboxAddress.Parse(recieveAddress));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = body };

            using var smtp = new SmtpClient();
            smtp.Connect("smtp.office365.com", 587, SecureSocketOptions.StartTls);
            smtp.Authenticate("HelpDeskApplication@outlook.com", "jzmhkmzseffthxjj");
            smtp.Send(email);
            smtp.Disconnect(true);
        }
    }
}
