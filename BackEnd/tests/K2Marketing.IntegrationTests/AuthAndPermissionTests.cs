using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace K2Marketing.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Login_returns_tokens_and_profile()
    {
        var (_, auth) = await api.LoginAsync(ApiFactory.Admin);
        auth.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        auth.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        auth.GetProperty("user").GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task Invalid_password_is_rejected_with_401()
    {
        var r = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.Admin, password = "errada" });
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await r.JsonAsync()).Code().Should().Be("auth.invalidCredentials");
    }

    [Fact]
    public async Task Protected_endpoint_requires_token()
    {
        var r = await api.CreateClient().GetAsync("/api/v1/clients");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_reuse_revokes_the_session()
    {
        var (_, auth) = await api.LoginAsync(ApiFactory.Analyst);
        var original = auth.GetProperty("refreshToken").GetString();
        var anon = api.CreateClient();

        var first = await anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await first.JsonAsync()).GetProperty("refreshToken").GetString();
        rotated.Should().NotBe(original);

        // Reutilizar o token antigo indica vazamento: é recusado e a sessão nova também é revogada.
        var reuse = await anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await reuse.JsonAsync()).Code().Should().Be("auth.refreshReused");
        (await anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rotated })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Language_preference_is_persisted_on_profile()
    {
        var http = await api.ClientAsync(ApiFactory.Manager);
        (await http.PutJsonAsync("/api/v1/auth/me/preferences", new { language = "en-US" })).EnsureSuccessStatusCode();
        (await http.GetJsonAsync("/api/v1/auth/me")).GetProperty("preferredLanguage").GetString().Should().Be("en-US");
        (await http.PutJsonAsync("/api/v1/auth/me/preferences", new { language = "fr-FR" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await http.PutJsonAsync("/api/v1/auth/me/preferences", new { language = "pt-BR" });
    }
}

[Collection(ApiCollection.Name)]
public class PermissionTests(ApiFactory api)
{
    [Fact]
    public async Task Analyst_cannot_manage_clients_but_can_operate_campaigns()
    {
        var analyst = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        (await analyst.PostJsonAsync("/api/v1/clients", new { name = "X", companyName = "X", email = "x@x.com", monthlyBudget = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var client = await Arrange.ClientAsync(admin);
        (await analyst.PostJsonAsync("/api/v1/campaigns", new
        {
            clientId = client.Id(), name = "Operada pelo analista", objective = "Traffic", platform = "GoogleAds",
            startDate = Arrange.Today, plannedBudget = 100m
        })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Only_admin_manages_users()
    {
        var manager = await api.ClientAsync(ApiFactory.Manager);
        (await manager.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        (await admin.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Client_viewer_is_read_only()
    {
        var viewer = await api.ClientAsync(ApiFactory.Viewer);
        (await viewer.PostJsonAsync("/api/v1/leads", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/v1/tasks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/v1/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Client_viewer_only_sees_own_client_data()
    {
        var (viewer, auth) = await api.LoginAsync(ApiFactory.Viewer);
        var ownClientId = auth.GetProperty("user").GetProperty("clientId").GetGuid();

        var clients = await viewer.GetJsonAsync("/api/v1/clients?pageSize=100");
        clients.GetProperty("items").EnumerateArray().Select(c => c.Id()).Should().Equal(ownClientId);

        var campaigns = await viewer.GetJsonAsync("/api/v1/campaigns?pageSize=200");
        campaigns.GetProperty("items").EnumerateArray().Should().NotBeEmpty()
            .And.OnlyContain(c => c.GetProperty("clientId").GetGuid() == ownClientId);

        var leads = await viewer.GetJsonAsync("/api/v1/leads?pageSize=500");
        leads.GetProperty("items").EnumerateArray().Should().OnlyContain(l => l.GetProperty("clientId").GetGuid() == ownClientId);

        // Recursos de outro cliente respondem 404 (não revelam a existência).
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var other = await Arrange.ClientAsync(admin);
        var otherCampaign = await Arrange.CampaignAsync(admin, other.Id());
        (await viewer.GetAsync($"/api/v1/clients/{other.Id()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await viewer.GetAsync($"/api/v1/campaigns/{otherCampaign.Id()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await viewer.GetAsync($"/api/v1/dashboard?clientId={other.Id()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await viewer.GetAsync($"/api/v1/reports/performance?clientId={other.Id()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_dashboard_totals_match_only_own_metrics()
    {
        var (viewer, auth) = await api.LoginAsync(ApiFactory.Viewer);
        var own = auth.GetProperty("user").GetProperty("clientId").GetGuid();
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var from = Arrange.Today.AddDays(-29).ToString("yyyy-MM-dd");
        var to = Arrange.Today.ToString("yyyy-MM-dd");

        var viewerSpend = (await viewer.GetJsonAsync($"/api/v1/dashboard?from={from}&to={to}")).GetProperty("metrics").Dec("spend");
        var adminOwnSpend = (await admin.GetJsonAsync($"/api/v1/dashboard?clientId={own}&from={from}&to={to}")).GetProperty("metrics").Dec("spend");
        var adminAllSpend = (await admin.GetJsonAsync($"/api/v1/dashboard?from={from}&to={to}")).GetProperty("metrics").Dec("spend");

        viewerSpend.Should().Be(adminOwnSpend).And.BeLessThan(adminAllSpend);
    }
}
