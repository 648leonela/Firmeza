using System.Net;
using System.Net.Mail;
using Firmeza.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Firmeza.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Se intento enviar un correo sin destinatario.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning("Servicio SMTP no configurado con credenciales activas. Simulando envio a {ToEmail}: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.UserName, _options.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Correo enviado exitosamente a {ToEmail}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo SMTP a {ToEmail} con asunto {Subject}", toEmail, subject);
            throw;
        }
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default)
    {
        var subject = "¡Bienvenido a Firmeza Materiales!";
        var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; line-height: 1.6;">
                <div style="max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;">
                    <h2 style="color: #d97706;">¡Hola {WebUtility.HtmlEncode(customerName)}!</h2>
                    <p>Te damos una cordial bienvenida a <strong>Firmeza Materiales</strong>.</p>
                    <p>Tu cuenta como cliente ha sido creada satisfactoriamente. Ahora puedes consultar nuestro catalogo y realizar tus pedidos en linea.</p>
                    <hr style="border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;" />
                    <p style="font-size: 12px; color: #64748b;">Este es un mensaje automatico. Por favor no respondas a este correo.</p>
                </div>
            </body>
            </html>
            """;

        await SendEmailAsync(toEmail, subject, body, isHtml: true, cancellationToken);
    }

    public async Task SendSaleConfirmationEmailAsync(string toEmail, string customerName, string saleNumber, decimal total, CancellationToken cancellationToken = default)
    {
        var subject = $"Confirmación de Compra #{saleNumber} - Firmeza";
        var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; line-height: 1.6;">
                <div style="max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;">
                    <h2 style="color: #059669;">¡Gracias por tu compra, {WebUtility.HtmlEncode(customerName)}!</h2>
                    <p>Hemos procesado tu orden con éxito.</p>
                    <div style="background-color: #f8fafc; padding: 15px; border-radius: 6px; margin: 15px 0;">
                        <p style="margin: 5px 0;"><strong>Número de Comprobante:</strong> {WebUtility.HtmlEncode(saleNumber)}</p>
                        <p style="margin: 5px 0;"><strong>Total Pagado:</strong> ${total:N2}</p>
                    </div>
                    <p>Estaremos preparando tus materiales para entrega a la mayor brevedad.</p>
                    <hr style="border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;" />
                    <p style="font-size: 12px; color: #64748b;">Firmeza Materiales de Construcción - Tu aliado en cada proyecto.</p>
                </div>
            </body>
            </html>
            """;

        await SendEmailAsync(toEmail, subject, body, isHtml: true, cancellationToken);
    }
}
