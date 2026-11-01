using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace UsuariosRoles.IntegrationTests;

/// <summary>
/// Levanta la API completa en memoria con SQLite en memoria (sin Docker ni PostgreSQL).
/// Cada instancia usa su propia BD, por lo que las clases de prueba no interfieren entre sí.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo123!";

    private readonly SqliteConnection keepAlive;
    private readonly string connectionString;

    public ApiFactory()
    {
        // Una BD en memoria "compartida" existe mientras haya al menos una conexión abierta.
        connectionString = $"Data Source=file:urb-{Guid.NewGuid():N}?mode=memory&cache=shared";
        keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
    }

    protected virtual int AuthPermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Key", "clave-solo-para-pruebas-de-integracion-0123456789");
        builder.UseSetting("AuthCookie:Secure", "false");
        builder.UseSetting("Features:DevMailbox", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    /// <summary>Cliente sin manejo automático de cookies: las pruebas controlan qué refresh token envían.</summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            keepAlive.Dispose();
        }
    }
}

/// <summary>Variante con un límite muy bajo para probar el rate limiting.</summary>
public sealed class StrictRateLimitApiFactory : ApiFactory
{
    protected override int AuthPermitLimit => 3;
}

public sealed record LoginResult(HttpResponseMessage Response, string? AccessToken, string? RefreshCookie, JsonElement Body);

internal static partial class ApiTestExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<LoginResult> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        string? token = response.IsSuccessStatusCode ? body.GetProperty("accessToken").GetString() : null;
        return new LoginResult(response, token, response.GetRefreshCookie(), body);
    }

    /// <summary>Extrae "urb_refresh=valor" de la cabecera Set-Cookie.</summary>
    public static string? GetRefreshCookie(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        return cookies
            .Select(c => c.Split(';')[0])
            .FirstOrDefault(c => c.StartsWith("urb_refresh=", StringComparison.Ordinal) && c.Length > "urb_refresh=".Length);
    }

    public static async Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> SendAuthorizedAsync(this HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetAuthorizedAsync(this HttpClient client, string url, string token) =>
        client.SendAuthorizedAsync(HttpMethod.Get, url, token);

    /// <summary>Lee el último enlace enviado al buzón de desarrollo y devuelve su token.</summary>
    public static async Task<string> GetLatestTokenFromMailboxAsync(this HttpClient client, string email, string subjectFragment)
    {
        var mails = await client.GetFromJsonAsync<JsonElement>($"/api/dev/mailbox?to={Uri.EscapeDataString(email)}", Json);
        var mail = mails.EnumerateArray().First(m => m.GetProperty("subject").GetString()!.Contains(subjectFragment, StringComparison.OrdinalIgnoreCase));
        var match = TokenRegex().Match(mail.GetProperty("textBody").GetString()!);
        Assert.True(match.Success, "El correo no contiene un enlace con token");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    public static async Task<string> CodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return problem.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : string.Empty;
    }

    public static void AssertStatus(this HttpResponseMessage response, HttpStatusCode expected) =>
        Assert.True(response.StatusCode == expected, $"Se esperaba {(int)expected} y llegó {(int)response.StatusCode}: {response.Content.ReadAsStringAsync().Result}");

    [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex TokenRegex();
}
