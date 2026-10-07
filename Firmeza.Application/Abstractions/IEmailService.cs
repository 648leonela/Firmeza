namespace Firmeza.Application.Abstractions;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default);
    Task SendSaleConfirmationEmailAsync(string toEmail, string customerName, string saleNumber, decimal total, CancellationToken cancellationToken = default);
}
