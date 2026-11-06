using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace K2Marketing.IntegrationTests;

/// <summary>Sobe a API completa contra um PostgreSQL real em container (migrations + seed de demonstração).</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Admin = "admin@k2digital.com.br";
    public const string Manager = "gerente@k2digital.com.br";
    public const string Analyst = "analista@k2digital.com.br";
    public const string Viewer = "cliente@bellamoda.com.br";

    public static readonly Dictionary<string, string> Passwords = new()
    {
        [Admin] = "Admin@123", [Manager] = "Gerente@123", [Analyst] = "Analista@123", [Viewer] = "Cliente@123"
    };

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("k2marketing_tests").WithUsername("k2").WithPassword("k2-tests").Build();

    private readonly string _storage = Path.Combine(Path.GetTempPath(), "k2m-tests-" + Guid.NewGuid().ToString("N"));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:Secret", "integration-tests-secret-0123456789-abcdefghijklmnop");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:Seed", "true");
        builder.UseSetting("Jobs:NotificationsEnabled", "false");
        builder.UseSetting("Storage:RootPath", _storage);
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // força a inicialização (migrations e seed)
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
        try { Directory.Delete(_storage, true); } catch { /* best effort */ }
    }

    public async Task<(HttpClient Client, JsonElement Auth)> LoginAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Passwords[email] });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, auth);
    }

    public async Task<HttpClient> ClientAsync(string email) => (await LoginAsync(email)).Client;
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, ApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, ApiFactory.Json);

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var r = await client.GetAsync(url);
        r.EnsureSuccessStatusCode();
        return await r.JsonAsync();
    }

    public static string Code(this JsonElement problem) => problem.GetProperty("code").GetString()!;
    public static Guid Id(this JsonElement e) => e.GetProperty("id").GetGuid();
    public static decimal Dec(this JsonElement e, string name) => e.GetProperty(name).GetDecimal();
}

/// <summary>Cria dados próprios de cada teste (nomes únicos), sem depender de valores aleatórios do seed.</summary>
public static class Arrange
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

    public static async Task<JsonElement> ClientAsync(HttpClient http, decimal monthly = 10_000m)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var r = await http.PostJsonAsync("/api/v1/clients", new
        {
            name = $"Cliente Teste {tag}", companyName = $"Teste {tag} Ltda.", email = $"t{tag}@exemplo.com.br", industry = "Testes", monthlyBudget = monthly
        });
        r.EnsureSuccessStatusCode();
        return await r.JsonAsync();
    }

    public static async Task<JsonElement> CampaignAsync(HttpClient http, Guid clientId, string status = "Active", int startOffset = -30, int? endOffset = 30)
    {
        var r = await http.PostJsonAsync("/api/v1/campaigns", new
        {
            clientId, name = $"Campanha Teste {Guid.NewGuid():N}"[..30], objective = "Sales", platform = "MetaAds", status,
            startDate = Today.AddDays(startOffset), endDate = endOffset is { } e ? Today.AddDays(e) : (DateOnly?)null, plannedBudget = 3000m
        });
        r.EnsureSuccessStatusCode();
        return await r.JsonAsync();
    }

    public static async Task<JsonElement> MetricAsync(HttpClient http, Guid campaignId, int dayOffset, long impressions, long clicks, decimal spend,
        long leads = 0, long conversions = 0, decimal revenue = 0m)
    {
        var r = await http.PostJsonAsync("/api/v1/metrics", new
        {
            campaignId, date = Today.AddDays(dayOffset), impressions, reach = impressions / 2, clicks, spend, leads, conversions, attributedRevenue = revenue
        });
        r.EnsureSuccessStatusCode();
        return await r.JsonAsync();
    }
}
