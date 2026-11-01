using System.Net;
using System.Net.Http.Json;

namespace UsuariosRoles.IntegrationTests;

public sealed class PlatformTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateApiClient();

    [Fact]
    public async Task Health_responde_ok()
    {
        var response = await client.GetAsync("/health");
        response.AssertStatus(HttpStatusCode.OK);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Respuestas_incluyen_cabeceras_de_seguridad()
    {
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "x@prueba.pe" });

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Ruta_inexistente_devuelve_problem_details_404()
    {
        var response = await client.GetAsync("/api/no-existe");
        response.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public sealed class RateLimitingTests(StrictRateLimitApiFactory factory) : IClassFixture<StrictRateLimitApiFactory>
{
    [Fact]
    public async Task Login_responde_429_al_superar_el_limite()
    {
        var client = factory.CreateApiClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.LoginAsync("nadie@prueba.pe", "Cualquiera#1")).Response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);

        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email = "nadie@prueba.pe", password = "x" });
        Assert.Equal("too_many_requests", await limited.CodeAsync());
    }
}
