using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UsuariosRoles.IntegrationTests;

public sealed class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateApiClient();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@prueba.pe";

    [Fact]
    public async Task Registro_verificacion_login_refresh_y_deteccion_de_reuso()
    {
        var email = NewEmail();
        const string password = "Registro#2026";

        // 1) Registro → 202 y correo de verificación en el buzón de desarrollo.
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, fullName = "Lucía Prueba", password });
        register.AssertStatus(HttpStatusCode.Accepted);

        // 2) Sin verificar no se puede entrar.
        var beforeVerify = await client.LoginAsync(email, password);
        beforeVerify.Response.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("email_not_verified", beforeVerify.Body.GetProperty("code").GetString());

        // 3) Verificación con el token del correo (y no se puede reutilizar).
        var verifyToken = await client.GetLatestTokenFromMailboxAsync(email, "Verifica");
        (await client.PostAsJsonAsync("/api/auth/verify-email", new { token = verifyToken })).AssertStatus(HttpStatusCode.OK);
        var reuseVerify = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = verifyToken });
        reuseVerify.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("invalid_token", await reuseVerify.CodeAsync());

        // 4) Login: access token en el cuerpo, refresh token SOLO en cookie HttpOnly.
        var login = await client.LoginAsync(email, password);
        login.Response.AssertStatus(HttpStatusCode.OK);
        Assert.NotNull(login.AccessToken);
        Assert.NotNull(login.RefreshCookie);
        Assert.False(login.Body.TryGetProperty("refreshToken", out _));
        var setCookie = string.Join(";", login.Response.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=strict", setCookie);

        var me = await client.GetAuthorizedAsync("/api/auth/me", login.AccessToken!);
        me.AssertStatus(HttpStatusCode.OK);
        var profile = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("usuario", profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        // 5) Refresh: rota el token.
        var refresh1 = await client.RefreshAsync(login.RefreshCookie);
        refresh1.AssertStatus(HttpStatusCode.OK);
        var rotatedCookie = refresh1.GetRefreshCookie();
        Assert.NotNull(rotatedCookie);
        Assert.NotEqual(login.RefreshCookie, rotatedCookie);

        // 6) Reuso del token viejo → 401 y se revoca toda la familia...
        var reuse = await client.RefreshAsync(login.RefreshCookie);
        reuse.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.Equal("refresh_token_reused", await reuse.CodeAsync());

        // ...incluido el token que parecía vigente.
        var afterReuse = await client.RefreshAsync(rotatedCookie);
        afterReuse.AssertStatus(HttpStatusCode.Unauthorized);

        // 7) El evento quedó auditado.
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var audit = await client.GetAuthorizedAsync("/api/audit?eventType=auth.refresh-token-reuse", admin.AccessToken!);
        var auditBody = await audit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(auditBody.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Registro_con_email_existente_responde_igual_y_avisa_por_correo()
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "usuario@demo.pe", fullName = "Intruso", password = "Otra#Clave2026" });

        response.AssertStatus(HttpStatusCode.Accepted);
        var mails = await client.GetFromJsonAsync<JsonElement>("/api/dev/mailbox?to=usuario@demo.pe");
        Assert.Contains(mails.EnumerateArray(), m => m.GetProperty("subject").GetString()!.Contains("Intento de registro"));
    }

    [Fact]
    public async Task Registro_con_password_debil_devuelve_problem_details_con_errores()
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = NewEmail(), fullName = "Débil", password = "123" });

        response.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").GetProperty("password").GetArrayLength() >= 3);
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Bloquea_la_cuenta_tras_intentos_fallidos()
    {
        var email = await CreateVerifiedUserAsync("Bloqueo#2026");

        for (var i = 0; i < 4; i++)
        {
            (await client.LoginAsync(email, "Incorrecta#1")).Response.AssertStatus(HttpStatusCode.Unauthorized);
        }

        var fifth = await client.LoginAsync(email, "Incorrecta#1");
        fifth.Response.AssertStatus((HttpStatusCode)423);
        Assert.Equal("account_locked", fifth.Body.GetProperty("code").GetString());

        // Con la cuenta bloqueada ni la contraseña correcta sirve.
        (await client.LoginAsync(email, "Bloqueo#2026")).Response.AssertStatus((HttpStatusCode)423);
    }

    [Fact]
    public async Task Credenciales_invalidas_no_revelan_si_el_email_existe()
    {
        var unknown = await client.LoginAsync(NewEmail(), "Cualquiera#1");
        var wrongPassword = await client.LoginAsync("usuario@demo.pe", "Cualquiera#1");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.Response.StatusCode);
        Assert.Equal(unknown.Body.GetProperty("detail").GetString(), wrongPassword.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Recuperacion_de_contrasena_con_token_de_un_solo_uso()
    {
        var email = await CreateVerifiedUserAsync("Original#2026");
        var session = await client.LoginAsync(email, "Original#2026");

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).AssertStatus(HttpStatusCode.Accepted);
        var token = await client.GetLatestTokenFromMailboxAsync(email, "Restablece");

        (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "Renovada#2026" })).AssertStatus(HttpStatusCode.OK);

        var reuse = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "OtraMas#2026" });
        reuse.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("invalid_token", await reuse.CodeAsync());

        (await client.LoginAsync(email, "Original#2026")).Response.AssertStatus(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(email, "Renovada#2026")).Response.AssertStatus(HttpStatusCode.OK);

        // Las sesiones previas al cambio quedaron revocadas.
        (await client.RefreshAsync(session.RefreshCookie)).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Olvide_password_con_email_desconocido_responde_igual()
    {
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = NewEmail() });
        response.AssertStatus(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Logout_revoca_el_refresh_token_y_borra_la_cookie()
    {
        var login = await client.LoginAsync("usuario@demo.pe", ApiFactory.DemoPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", login.RefreshCookie);
        var logout = await client.SendAsync(request);
        logout.AssertStatus(HttpStatusCode.NoContent);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("urb_refresh=;", StringComparison.Ordinal));

        (await client.RefreshAsync(login.RefreshCookie)).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cambiar_password_renueva_la_sesion_y_revoca_las_demas()
    {
        var email = await CreateVerifiedUserAsync("Inicial#2026");
        var otherDevice = await client.LoginAsync(email, "Inicial#2026");
        var current = await client.LoginAsync(email, "Inicial#2026");

        var change = await client.SendAuthorizedAsync(HttpMethod.Post, "/api/auth/change-password", current.AccessToken!, new { currentPassword = "Inicial#2026", newPassword = "Cambiada#2026" });
        change.AssertStatus(HttpStatusCode.OK);

        (await client.RefreshAsync(otherDevice.RefreshCookie)).AssertStatus(HttpStatusCode.Unauthorized);
        (await client.RefreshAsync(change.GetRefreshCookie())).AssertStatus(HttpStatusCode.OK);
    }

    private async Task<string> CreateVerifiedUserAsync(string password)
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var email = NewEmail();
        var created = await client.SendAuthorizedAsync(HttpMethod.Post, "/api/users", admin.AccessToken!, new { email, fullName = "Usuario Prueba", password, emailConfirmed = true });
        created.AssertStatus(HttpStatusCode.Created);
        return email;
    }
}
