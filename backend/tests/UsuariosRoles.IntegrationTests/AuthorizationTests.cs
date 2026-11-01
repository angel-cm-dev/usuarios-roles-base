using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UsuariosRoles.IntegrationTests;

public sealed class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateApiClient();

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/roles")]
    [InlineData("/api/audit")]
    [InlineData("/api/auth/me")]
    public async Task Sin_token_devuelve_401_con_problem_details(string url)
    {
        var response = await client.GetAsync(url);

        response.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.Equal("unauthorized", await response.CodeAsync());
    }

    [Fact]
    public async Task Token_manipulado_devuelve_401()
    {
        var login = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var tampered = login.AccessToken![..^4] + "AAAA";

        (await client.GetAuthorizedAsync("/api/users", tampered)).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/roles")]
    [InlineData("/api/permissions")]
    [InlineData("/api/audit")]
    [InlineData("/api/dashboard/stats")]
    public async Task Usuario_sin_permiso_recibe_403(string url)
    {
        var login = await client.LoginAsync("usuario@demo.pe", ApiFactory.DemoPassword);

        var response = await client.GetAuthorizedAsync(url, login.AccessToken!);

        response.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("forbidden", await response.CodeAsync());
    }

    [Fact]
    public async Task Admin_lista_usuarios_con_paginacion_filtros_y_orden()
    {
        var login = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);

        var page = await client.GetAuthorizedAsync("/api/users?page=1&pageSize=1&sortBy=email&sortDir=asc", login.AccessToken!);
        page.AssertStatus(HttpStatusCode.OK);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);
        Assert.Equal("admin@demo.pe", body.GetProperty("items")[0].GetProperty("email").GetString());

        var filtered = await client.GetAuthorizedAsync("/api/users?role=admin&search=ADMIN", login.AccessToken!);
        var filteredBody = await filtered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(filteredBody.GetProperty("items").EnumerateArray(), u =>
            Assert.Contains("admin", u.GetProperty("roles").EnumerateArray().Select(r => r.GetString())));
    }

    [Fact]
    public async Task Los_permisos_nuevos_aplican_al_renovar_el_access_token()
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);

        // Rol nuevo sin permisos + usuario con ese rol.
        var role = await client.SendAuthorizedAsync(HttpMethod.Post, "/api/roles", admin.AccessToken!, new { name = $"soporte-{Guid.NewGuid():N}"[..20], description = "Soporte", permissions = Array.Empty<string>() });
        role.AssertStatus(HttpStatusCode.Created);
        var roleId = (await role.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var email = $"soporte-{Guid.NewGuid():N}@prueba.pe";
        (await client.SendAuthorizedAsync(HttpMethod.Post, "/api/users", admin.AccessToken!, new { email, fullName = "Agente Soporte", password = "Soporte#2026", emailConfirmed = true, roleIds = new[] { roleId } }))
            .AssertStatus(HttpStatusCode.Created);

        var agent = await client.LoginAsync(email, "Soporte#2026");
        (await client.GetAuthorizedAsync("/api/audit", agent.AccessToken!)).AssertStatus(HttpStatusCode.Forbidden);

        // El admin concede "auditoria.leer" al rol.
        (await client.SendAuthorizedAsync(HttpMethod.Put, $"/api/roles/{roleId}/permissions", admin.AccessToken!, new { permissions = new[] { "auditoria.leer" } }))
            .AssertStatus(HttpStatusCode.OK);

        // El access token viejo no cambia (los permisos viajan en el JWT)...
        (await client.GetAuthorizedAsync("/api/audit", agent.AccessToken!)).AssertStatus(HttpStatusCode.Forbidden);

        // ...pero tras el refresh, el nuevo token ya incluye el permiso.
        var refreshed = await client.RefreshAsync(agent.RefreshCookie);
        refreshed.AssertStatus(HttpStatusCode.OK);
        var newToken = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        (await client.GetAuthorizedAsync("/api/audit", newToken)).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Los_permisos_del_rol_admin_no_se_pueden_modificar()
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var roles = await (await client.GetAuthorizedAsync("/api/roles?search=admin", admin.AccessToken!)).Content.ReadFromJsonAsync<JsonElement>();
        var adminRoleId = roles.GetProperty("items").EnumerateArray().First(r => r.GetProperty("name").GetString() == "admin").GetProperty("id").GetGuid();

        var response = await client.SendAuthorizedAsync(HttpMethod.Put, $"/api/roles/{adminRoleId}/permissions", admin.AccessToken!, new { permissions = Array.Empty<string>() });

        response.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("admin_role_locked", await response.CodeAsync());
    }

    [Fact]
    public async Task No_se_puede_dejar_el_sistema_sin_administradores()
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var adminId = admin.Body.GetProperty("user").GetProperty("id").GetGuid();

        var response = await client.SendAuthorizedAsync(HttpMethod.Put, $"/api/users/{adminId}/roles", admin.AccessToken!, new { roleIds = Array.Empty<Guid>() });

        response.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("last_admin", await response.CodeAsync());
    }

    [Fact]
    public async Task Admin_bloquea_usuario_y_sus_sesiones_se_revocan()
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);
        var email = $"bloq-{Guid.NewGuid():N}@prueba.pe";
        var created = await client.SendAuthorizedAsync(HttpMethod.Post, "/api/users", admin.AccessToken!, new { email, fullName = "Por Bloquear", password = "Bloquear#2026", emailConfirmed = true });
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var session = await client.LoginAsync(email, "Bloquear#2026");

        (await client.SendAuthorizedAsync(HttpMethod.Post, $"/api/users/{userId}/lock", admin.AccessToken!, new { minutes = 30 })).AssertStatus(HttpStatusCode.OK);

        (await client.RefreshAsync(session.RefreshCookie)).AssertStatus(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(email, "Bloquear#2026")).Response.AssertStatus((HttpStatusCode)423);

        (await client.SendAuthorizedAsync(HttpMethod.Post, $"/api/users/{userId}/unlock", admin.AccessToken!)).AssertStatus(HttpStatusCode.OK);
        (await client.LoginAsync(email, "Bloquear#2026")).Response.AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_devuelve_kpis_para_el_admin()
    {
        var admin = await client.LoginAsync("admin@demo.pe", ApiFactory.DemoPassword);

        var response = await client.GetAuthorizedAsync("/api/dashboard/stats", admin.AccessToken!);

        response.AssertStatus(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(stats.GetProperty("totalUsers").GetInt32() >= 2);
        Assert.Equal(7, stats.GetProperty("loginsLast7Days").GetArrayLength());
    }
}
