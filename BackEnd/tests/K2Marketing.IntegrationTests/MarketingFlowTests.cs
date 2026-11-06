using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace K2Marketing.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ClientCrudTests(ApiFactory api)
{
    [Fact]
    public async Task Create_update_search_and_deactivate_client()
    {
        var http = await api.ClientAsync(ApiFactory.Manager);
        var created = await Arrange.ClientAsync(http, 12_000m);
        var id = created.Id();

        var update = await http.PutJsonAsync($"/api/v1/clients/{id}", new
        {
            name = created.GetProperty("name").GetString(), companyName = "Nova Razão Ltda.", email = "novo@exemplo.com.br",
            document = "11.222.333/0001-81", industry = "Varejo", website = "https://exemplo.com.br", monthlyBudget = 15_000m
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.JsonAsync()).Dec("monthlyBudget").Should().Be(15_000m);

        var search = await http.GetJsonAsync($"/api/v1/clients?search={Uri.EscapeDataString(created.GetProperty("name").GetString()!)}");
        search.GetProperty("totalCount").GetInt32().Should().Be(1);

        var deactivate = await http.PatchJsonAsync($"/api/v1/clients/{id}/status", new { status = "Inactive" });
        (await deactivate.JsonAsync()).GetProperty("status").GetString().Should().Be("Inactive");

        // Cliente inativo não recebe novas campanhas.
        var campaign = await http.PostJsonAsync("/api/v1/campaigns", new
        {
            clientId = id, name = "Bloqueada", objective = "Sales", platform = "MetaAds", startDate = Arrange.Today, plannedBudget = 1m
        });
        campaign.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await campaign.JsonAsync()).Code().Should().Be("client.inactive");

        var history = await http.GetJsonAsync($"/api/v1/clients/{id}/history");
        history.EnumerateArray().Select(h => h.GetProperty("action").GetString()).Should().Contain(["Create", "Update", "Deactivate"]);
    }

    [Fact]
    public async Task Validation_errors_return_400_with_field_details()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var r = await http.PostJsonAsync("/api/v1/clients", new { name = "", companyName = "", email = "invalido", monthlyBudget = -1 });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await r.JsonAsync();
        problem.Code().Should().Be("validation");
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain(["Name", "Email", "MonthlyBudget"]);
    }

    [Fact]
    public async Task Duplicate_document_is_rejected()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var doc = $"{Random.Shared.Next(10, 99)}.{Random.Shared.Next(100, 999)}.{Random.Shared.Next(100, 999)}/0001-{Random.Shared.Next(10, 99)}";
        object Body(string n) => new { name = n, companyName = n, email = "d@exemplo.com.br", document = doc, monthlyBudget = 0 };
        (await http.PostJsonAsync("/api/v1/clients", Body("Doc A"))).StatusCode.Should().Be(HttpStatusCode.Created);
        var dup = await http.PostJsonAsync("/api/v1/clients", Body("Doc B"));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await dup.JsonAsync()).Code().Should().Be("client.documentTaken");
    }
}

[Collection(ApiCollection.Name)]
public class CampaignTests(ApiFactory api)
{
    [Fact]
    public async Task Status_lifecycle_follows_allowed_transitions()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(admin);
        var c = await Arrange.CampaignAsync(http, client.Id(), "Draft");
        var id = c.Id();
        c.GetProperty("allowedTransitions").EnumerateArray().Select(x => x.GetString()).Should().Contain("Active").And.NotContain("Paused");

        async Task<HttpResponseMessage> Set(string s) => await http.PatchJsonAsync($"/api/v1/campaigns/{id}/status", new { status = s });

        (await Set("Paused")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // rascunho não pode ser pausado
        (await (await Set("Active")).JsonAsync()).GetProperty("status").GetString().Should().Be("Active");
        (await (await Set("Paused")).JsonAsync()).GetProperty("status").GetString().Should().Be("Paused");
        (await (await Set("Active")).JsonAsync()).GetProperty("status").GetString().Should().Be("Active");
        (await (await Set("Completed")).JsonAsync()).GetProperty("status").GetString().Should().Be("Completed");
        var reopen = await Set("Active");
        reopen.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reopen.JsonAsync()).Code().Should().Be("campaign.invalidTransition");
    }

    [Fact]
    public async Task End_date_before_start_is_invalid()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(http);
        var r = await http.PostJsonAsync("/api/v1/campaigns", new
        {
            clientId = client.Id(), name = "Datas", objective = "Sales", platform = "MetaAds",
            startDate = Arrange.Today, endDate = Arrange.Today.AddDays(-1), plannedBudget = 10m
        });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_copies_ads_as_draft_and_campaign_with_data_cannot_be_deleted()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(http);
        var c = await Arrange.CampaignAsync(http, client.Id());
        (await http.PostJsonAsync("/api/v1/advertisements", new
        {
            campaignId = c.Id(), name = "Ad", headline = "Título", primaryText = "Texto", callToAction = "Saiba mais",
            destinationUrl = "https://exemplo.com.br", status = "Active"
        })).EnsureSuccessStatusCode();

        var copy = await (await http.PostJsonAsync($"/api/v1/campaigns/{c.Id()}/duplicate")).JsonAsync();
        copy.GetProperty("status").GetString().Should().Be("Draft");
        copy.GetProperty("adsCount").GetInt32().Should().Be(1);

        await Arrange.MetricAsync(http, c.Id(), -1, 1000, 10, 5m);
        var del = await http.DeleteAsync($"/api/v1/campaigns/{c.Id()}");
        del.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await del.JsonAsync()).Code().Should().Be("campaign.hasData");
        (await http.DeleteAsync($"/api/v1/campaigns/{copy.Id()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

[Collection(ApiCollection.Name)]
public class BudgetAndMetricTests(ApiFactory api)
{
    [Fact]
    public async Task Actual_spend_is_synced_from_metrics_and_threshold_alerts_are_sent_once()
    {
        var http = await api.ClientAsync(ApiFactory.Manager);
        var client = await Arrange.ClientAsync(http);
        var c = await Arrange.CampaignAsync(http, client.Id());
        var budget = await (await http.PostJsonAsync("/api/v1/budgets", new
        {
            campaignId = c.Id(), plannedAmount = 1000m, startDate = Arrange.Today.AddDays(-10), endDate = Arrange.Today.AddDays(10)
        })).JsonAsync();
        budget.Dec("actualSpend").Should().Be(0m);

        await Arrange.MetricAsync(http, c.Id(), -2, 10_000, 200, 450m);
        // Gasto registrado pelo orçamento soma na métrica de nível campanha do dia (sem lançamento duplicado).
        var afterSpend = await (await http.PostJsonAsync($"/api/v1/budgets/{budget.Id()}/spend", new { date = Arrange.Today.AddDays(-2), amount = 400m })).JsonAsync();
        afterSpend.Dec("actualSpend").Should().Be(850m);   // 450 (métrica do dia) + 400 somados à mesma linha
        afterSpend.Dec("usagePercent").Should().Be(85m);
        afterSpend.GetProperty("threshold").GetInt32().Should().Be(80);
        var metrics = await http.GetJsonAsync($"/api/v1/metrics?campaignId={c.Id()}");
        metrics.GetProperty("totalCount").GetInt32().Should().Be(1, "o gasto do orçamento não cria um segundo lançamento");

        await Arrange.MetricAsync(http, c.Id(), -1, 10_000, 200, 100m);
        var row = (await http.GetJsonAsync($"/api/v1/budgets?campaignId={c.Id()}")).GetProperty("items")[0];
        row.Dec("actualSpend").Should().Be(950m);
        row.GetProperty("threshold").GetInt32().Should().Be(90);

        // Métricas fora do período do orçamento não entram no realizado.
        await Arrange.MetricAsync(http, c.Id(), -20, 1000, 10, 999m);
        (await http.GetJsonAsync($"/api/v1/budgets/{budget.Id()}")).GetProperty("budget").Dec("actualSpend").Should().Be(950m);

        // Um alerta por limite atingido (80% e 90%), sem repetição.
        var notifications = await http.GetJsonAsync("/api/v1/notifications?pageSize=100");
        notifications.GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == "BudgetThreshold" && n.GetProperty("data").GetProperty("budgetId").GetGuid() == budget.Id())
            .Select(n => n.GetProperty("data").GetProperty("threshold").GetInt32())
            .Should().BeEquivalentTo([80, 90]);
    }

    [Fact]
    public async Task Metric_rows_are_unique_per_campaign_ad_and_day()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var c = await Arrange.CampaignAsync(admin, (await Arrange.ClientAsync(admin)).Id());
        await Arrange.MetricAsync(http, c.Id(), -3, 1000, 10, 10m);
        var dup = await http.PostJsonAsync("/api/v1/metrics", new
        {
            campaignId = c.Id(), date = Arrange.Today.AddDays(-3), impressions = 5, reach = 1, clicks = 1, spend = 1m, leads = 0, conversions = 0, attributedRevenue = 0m
        });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await dup.JsonAsync()).Code().Should().Be("metric.duplicate");

        var inconsistent = await http.PostJsonAsync("/api/v1/metrics", new
        {
            campaignId = c.Id(), date = Arrange.Today.AddDays(-4), impressions = 10, reach = 5, clicks = 50, spend = 1m, leads = 0, conversions = 0, attributedRevenue = 0m
        });
        inconsistent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_and_reports_derive_kpis_from_persisted_totals()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(http);
        var c = await Arrange.CampaignAsync(http, client.Id());
        await Arrange.MetricAsync(http, c.Id(), -2, impressions: 100, clicks: 10, spend: 10m, leads: 2, conversions: 1, revenue: 40m);
        await Arrange.MetricAsync(http, c.Id(), -1, impressions: 10_000, clicks: 100, spend: 90m, leads: 8, conversions: 4, revenue: 260m);

        var d = await http.GetJsonAsync($"/api/v1/dashboard?clientId={client.Id()}&from={Arrange.Today.AddDays(-6):yyyy-MM-dd}&to={Arrange.Today:yyyy-MM-dd}");
        var m = d.GetProperty("metrics");
        m.Dec("spend").Should().Be(100m);
        m.GetProperty("clicks").GetInt64().Should().Be(110);
        m.Dec("ctr").Should().Be(1.0891m);   // 110 / 10.100 × 100 (não a média de 10% e 1%)
        m.Dec("cpc").Should().Be(0.9091m);   // 100 / 110
        m.Dec("cpm").Should().Be(9.901m);    // 100 / 10.100 × 1000
        m.Dec("cpl").Should().Be(10m);       // 100 / 10
        m.Dec("cpa").Should().Be(20m);       // 100 / 5
        m.Dec("roas").Should().Be(3m);       // 300 / 100
        d.GetProperty("daily").GetArrayLength().Should().Be(7);
        d.GetProperty("activeClients").GetInt32().Should().Be(1);

        var report = await http.GetJsonAsync($"/api/v1/reports/performance?clientId={client.Id()}&groupBy=campaign&from={Arrange.Today.AddDays(-6):yyyy-MM-dd}&to={Arrange.Today:yyyy-MM-dd}");
        report.GetProperty("totals").Dec("roas").Should().Be(3m);

        var csv = await http.GetAsync($"/api/v1/reports/performance/export?clientId={client.Id()}&from={Arrange.Today.AddDays(-6):yyyy-MM-dd}&to={Arrange.Today:yyyy-MM-dd}");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        (await csv.Content.ReadAsStringAsync()).Should().Contain("TOTAL,,10100,");
    }

    [Fact]
    public async Task Kpis_without_denominator_are_null()
    {
        var http = await api.ClientAsync(ApiFactory.Admin);
        var c = await Arrange.CampaignAsync(http, (await Arrange.ClientAsync(http)).Id());
        await Arrange.MetricAsync(http, c.Id(), -1, impressions: 0, clicks: 0, spend: 0m);
        var m = (await http.GetJsonAsync($"/api/v1/campaigns/{c.Id()}")).GetProperty("metrics");
        m.GetProperty("ctr").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        m.GetProperty("roas").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }
}

[Collection(ApiCollection.Name)]
public class ImportAndConversionTests(ApiFactory api)
{
    private static MultipartFormDataContent Csv(string content, string? mode = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "metricas.csv");
        if (mode is not null) form.Add(new StringContent(mode), "mode");
        return form;
    }

    [Fact]
    public async Task Csv_import_previews_validates_and_handles_duplicates()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var c = await Arrange.CampaignAsync(admin, (await Arrange.ClientAsync(admin)).Id());
        var id = c.Id();
        await Arrange.MetricAsync(http, id, -3, 500, 5, 5m); // já existe no banco
        string D(int o) => Arrange.Today.AddDays(o).ToString("yyyy-MM-dd");
        var csv = string.Join('\n',
            "date;campaign_id;impressions;reach;clicks;spend;leads;conversions;attributed_revenue",
            $"{D(-1)};{id};1000;800;20;30,50;2;1;120,00",
            $"{D(-2)};{id};2000;1500;40;60.00;4;2;240",
            $"{D(-3)};{id};3000;2000;60;90;6;3;360",       // duplicada (banco)
            $"{D(-1)};{id};9;9;9;9;0;0;0",                  // duplicada no arquivo
            $"{D(-4)};{id};10;5;50;1;0;0;0",                // inválida: cliques > impressões
            $"{D(-5)};00000000-0000-0000-0000-000000000000;10;5;1;1;0;0;0"); // campanha inexistente

        var preview = await (await http.PostAsync("/api/v1/imports/metrics/preview", Csv(csv))).JsonAsync();
        preview.GetProperty("newRows").GetInt32().Should().Be(2);
        preview.GetProperty("duplicates").GetInt32().Should().Be(1);
        preview.GetProperty("duplicatesInFile").GetInt32().Should().Be(1);
        preview.GetProperty("invalid").GetInt32().Should().Be(2);
        (await http.GetJsonAsync($"/api/v1/metrics?campaignId={id}")).GetProperty("totalCount").GetInt32().Should().Be(1, "a prévia não grava nada");

        var skip = await (await http.PostAsync("/api/v1/imports/metrics/confirm", Csv(csv, "Skip"))).JsonAsync();
        skip.GetProperty("inserted").GetInt32().Should().Be(2);
        skip.GetProperty("updated").GetInt32().Should().Be(0);
        var totals = await http.GetJsonAsync($"/api/v1/metrics/totals?campaignId={id}");
        totals.Dec("spend").Should().Be(95.5m); // 5 + 30,50 + 60

        // Reimportar com Overwrite atualiza as linhas existentes em vez de duplicar.
        var overwrite = await (await http.PostAsync("/api/v1/imports/metrics/confirm", Csv(csv, "Overwrite"))).JsonAsync();
        overwrite.GetProperty("inserted").GetInt32().Should().Be(0);
        overwrite.GetProperty("updated").GetInt32().Should().Be(3);
        (await http.GetJsonAsync($"/api/v1/metrics/totals?campaignId={id}")).Dec("spend").Should().Be(180.5m); // 90 + 30,50 + 60
        var rows = await http.GetJsonAsync($"/api/v1/metrics?campaignId={id}&source=CsvImport");
        rows.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Missing_columns_are_reported_without_writing()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var preview = await (await http.PostAsync("/api/v1/imports/metrics/preview", Csv("date,clicks\n2026-01-01,1"))).JsonAsync();
        preview.GetProperty("missingColumns").EnumerateArray().Select(x => x.GetString()).Should().Contain(["impressions", "spend", "campaign_id|campaign_name"]);
        (await http.PostAsync("/api/v1/imports/metrics/confirm", Csv("date,clicks\n2026-01-01,1", "Skip"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Same_conversion_event_is_never_counted_twice()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(admin);
        var c = await Arrange.CampaignAsync(admin, client.Id());
        var lead = await (await http.PostJsonAsync("/api/v1/leads", new { clientId = client.Id(), campaignId = c.Id(), name = "Lead X", email = "lx@exemplo.com.br", consentGiven = true })).JsonAsync();

        object Purchase(string ext) => new { campaignId = c.Id(), leadId = lead.Id(), conversionType = "Purchase", conversionDate = DateTime.UtcNow.AddHours(-1), revenueAmount = 300m, externalId = ext };
        (await http.PostJsonAsync("/api/v1/conversions", Purchase("PED-77"))).StatusCode.Should().Be(HttpStatusCode.Created);
        var dup = await http.PostJsonAsync("/api/v1/conversions", Purchase("ped-77"));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await dup.JsonAsync()).Code().Should().Be("conversion.duplicate");
        (await http.PostJsonAsync("/api/v1/conversions", Purchase("PED-78"))).StatusCode.Should().Be(HttpStatusCode.Created);

        object LeadEvent() => new { campaignId = c.Id(), leadId = lead.Id(), conversionType = "Lead", conversionDate = DateTime.UtcNow.AddHours(-2), revenueAmount = 0m };
        (await http.PostJsonAsync("/api/v1/conversions", LeadEvent())).StatusCode.Should().Be(HttpStatusCode.Created);
        (await http.PostJsonAsync("/api/v1/conversions", LeadEvent())).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Conversões gerais × leads únicos × vendas.
        var s = await http.GetJsonAsync($"/api/v1/conversions/summary?campaignId={c.Id()}");
        s.GetProperty("totalConversions").GetInt32().Should().Be(3);
        s.GetProperty("uniqueLeadsConverted").GetInt32().Should().Be(1);
        s.GetProperty("sales").GetInt32().Should().Be(2);
        s.Dec("salesRevenue").Should().Be(600m);

        // A compra marca o lead como convertido e registra no histórico.
        var detail = await http.GetJsonAsync($"/api/v1/leads/{lead.Id()}");
        detail.GetProperty("lead").GetProperty("status").GetString().Should().Be("Converted");

        // Importação CSV também ignora o evento já registrado.
        var form = Csv($"campaign_id,conversion_type,conversion_date,revenue_amount,external_id\n{c.Id()},Compra,{Arrange.Today:yyyy-MM-dd},300,PED-77\n{c.Id()},Compra,{Arrange.Today:yyyy-MM-dd},150,PED-99");
        var result = await (await http.PostAsync("/api/v1/imports/conversions/confirm", form)).JsonAsync();
        result.GetProperty("inserted").GetInt32().Should().Be(1);
        result.GetProperty("skipped").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Upload_rejects_spoofed_file_and_serves_valid_file_only_to_authorized_users()
    {
        var http = await api.ClientAsync(ApiFactory.Analyst);
        var admin = await api.ClientAsync(ApiFactory.Admin);
        var client = await Arrange.ClientAsync(admin);

        MultipartFormDataContent Form(byte[] bytes, string name)
        {
            var f = new MultipartFormDataContent { { new StringContent(client.Id().ToString()), "clientId" }, { new StringContent("Peça"), "name" }, { new StringContent("Image"), "type" } };
            f.Add(new ByteArrayContent(bytes), "file", name);
            return f;
        }

        var spoofed = await http.PostAsync("/api/v1/creatives", Form("MZ fake executable"u8.ToArray(), "foto.png"));
        spoofed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        var ok = await http.PostAsync("/api/v1/creatives", Form(png, "foto.png"));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var creative = await ok.JsonAsync();

        (await http.GetAsync($"/api/v1/creatives/{creative.Id()}/file")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.CreateClient().GetAsync($"/api/v1/creatives/{creative.Id()}/file")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var viewer = await api.ClientAsync(ApiFactory.Viewer);
        (await viewer.GetAsync($"/api/v1/creatives/{creative.Id()}/file")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
