using MailKit.Net.Smtp;
using MimeKit;

namespace MarkBackend.Helpers
{
    public class EmailHelper
    {
        private readonly IConfiguration _config;

        public EmailHelper(IConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> SendEmailRegistrationConfirm(string userEmail, string link)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress("IBuy Mart", "ibuy.support.mail@gmail.com"));
            message.To.Add(new MailboxAddress(name: userEmail, address: userEmail));
            message.Subject = "Confirmation of registration on the IBuy Mart website";
            message.Body = new TextPart("html")
            {
                Text = link,
            };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, false);
                await client.AuthenticateAsync("ibuy.support.mail@gmail.com", _config.GetValue<string>("GoogleApplicationPassword"));

                try
                {
                    await client.SendAsync(message);
                    return true;
                }
                catch (Exception)
                {
                    //Logging information
                }
                finally
                {
                    await client.DisconnectAsync(true);
                }
            }

            return false;
        }

        public async Task<bool> SendEmailPasswordReset(string userEmail, string link)
        {
            var message = new MimeMessage();

            //от кого отправляем и заголовок
            message.From.Add(new MailboxAddress("IBuy Mart", "ibuy.support.mail@gmail.com"));
            //кому отправляем
            message.To.Add(new MailboxAddress($"{userEmail}", userEmail));

            //тема письма
            message.Subject = "Сброс пароля на маркетплейсе IBuy Mart";
            //тело письма
            message.Body = new TextPart("html")
            {
                Text = link,
            };

            using (var client = new SmtpClient())
            {
                //Указываем smtp сервер почты и порт
                await client.ConnectAsync("smtp.gmail.com", 587, false);
                //Указываем свой Email адрес и пароль приложения
                await client.AuthenticateAsync("ibuy.support.mail@gmail.com", _config.GetValue<string>("GoogleApplicationPassword"));

                try
                {
                    client.Send(message);
                    return true;
                }
                catch (Exception)
                {

                    //Logging information
                }
                finally
                {
                    client.Disconnect(true);
                }
            }
            return false;
        }
    }
}
