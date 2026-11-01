using System.Net;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Application.Emails;

/// <summary>Plantillas de correo simples (HTML + texto plano). Los datos del usuario se codifican para evitar inyección HTML.</summary>
public static class EmailTemplates
{
    public static EmailMessage VerifyEmail(string to, string fullName, string link, int hours) => Build(
        to,
        "Verifica tu email",
        fullName,
        $"Gracias por registrarte. Confirma tu email con el siguiente enlace (válido por {hours} horas):",
        link,
        "Verificar email");

    public static EmailMessage PasswordReset(string to, string fullName, string link, int minutes) => Build(
        to,
        "Restablece tu contraseña",
        fullName,
        $"Recibimos una solicitud para restablecer tu contraseña. El enlace es de un solo uso y vence en {minutes} minutos. Si no fuiste tú, ignora este correo.",
        link,
        "Restablecer contraseña");

    public static EmailMessage RegistrationAttempt(string to, string fullName, string loginLink) => Build(
        to,
        "Intento de registro con tu email",
        fullName,
        "Alguien intentó crear una cuenta con tu email, pero ya tienes una. Si fuiste tú, inicia sesión o recupera tu contraseña.",
        loginLink,
        "Ir a iniciar sesión");

    public static EmailMessage PasswordChanged(string to, string fullName, string loginLink) => Build(
        to,
        "Tu contraseña fue cambiada",
        fullName,
        "La contraseña de tu cuenta se cambió y se cerraron tus sesiones. Si no fuiste tú, recupera tu cuenta de inmediato.",
        loginLink,
        "Ir a iniciar sesión");

    private static EmailMessage Build(string to, string subject, string fullName, string intro, string link, string cta)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeLink = WebUtility.HtmlEncode(link);
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto">
              <h2 style="color:#4f46e5">usuarios-roles-base</h2>
              <p>Hola {safeName},</p>
              <p>{WebUtility.HtmlEncode(intro)}</p>
              <p><a href="{safeLink}" style="background:#4f46e5;color:#fff;padding:10px 16px;border-radius:6px;text-decoration:none">{WebUtility.HtmlEncode(cta)}</a></p>
              <p style="font-size:12px;color:#6b7280">Si el botón no funciona copia este enlace: {safeLink}</p>
            </div>
            """;
        var text = $"Hola {fullName},\n\n{intro}\n\n{cta}: {link}\n";
        return new EmailMessage(to, subject, html, text);
    }
}
