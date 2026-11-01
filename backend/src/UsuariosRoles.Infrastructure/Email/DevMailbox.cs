using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Infrastructure.Email;

public sealed record DevEmail(Guid Id, string To, string Subject, string HtmlBody, string TextBody, DateTime SentAt);

/// <summary>
/// Buzón en memoria para desarrollo y pruebas: guarda los últimos correos "enviados".
/// Se consulta con GET /api/dev/mailbox (solo si Features:DevMailbox = true).
/// </summary>
public sealed class DevMailbox
{
    private const int Capacity = 200;
    private readonly ConcurrentQueue<DevEmail> messages = new();

    public void Add(DevEmail email)
    {
        messages.Enqueue(email);
        while (messages.Count > Capacity && messages.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<DevEmail> List(string? to = null) =>
        messages
            .Where(m => to is null || string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.SentAt)
            .ToList();

    public void Clear() => messages.Clear();
}

/// <summary>IEmailSender de desarrollo: no envía nada real, guarda en <see cref="DevMailbox"/> y escribe en el log.</summary>
internal sealed class DevMailboxEmailSender(DevMailbox mailbox, TimeProvider timeProvider, ILogger<DevMailboxEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        mailbox.Add(new DevEmail(Guid.NewGuid(), message.To, message.Subject, message.HtmlBody, message.TextBody, timeProvider.GetUtcNow().UtcDateTime));
        logger.LogInformation("Correo simulado para {To}: {Subject}", message.To, message.Subject);

        // El cuerpo (con enlaces de un solo uso) solo se registra en nivel Debug.
        logger.LogDebug("Contenido del correo simulado:\n{Body}", message.TextBody);
        return Task.CompletedTask;
    }
}
