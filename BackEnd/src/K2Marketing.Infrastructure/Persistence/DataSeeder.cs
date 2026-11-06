using K2Marketing.Application.Budgets;
using K2Marketing.Application.Common;
using K2Marketing.Application.Conversions;
using K2Marketing.Application.Notifications;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K2Marketing.Infrastructure.Persistence;

/// <summary>
/// Dados fictícios da agência K2 Digital: 10 clientes, 25 campanhas, 60 anúncios, 100 criativos, 150 leads,
/// conversões, ~90 dias de métricas, orçamentos, tarefas e notificações. Datas relativas a "hoje" para manter a demo atual.
/// Executa somente com o banco vazio (idempotente).
/// </summary>
public class DataSeeder(AppDbContext db, IPasswordHasher hasher, IClock clock, IFileStorage storage,
    BudgetSyncService budgetSync, NotificationScanner scanner, NotificationService notifications, ILogger<DataSeeder> logger)
{
    private enum Profile { None, Good, Medium, Poor }

    private record ClientSeed(string Name, string Company, string Industry, decimal Monthly, string Domain, (byte, byte, byte) Color, bool Active = true);

    private record CampaignSeed(int Client, string Name, CampaignObjective Objective, AdPlatform Platform, CampaignStatus Status, Profile Profile,
        int StartOffset, int? EndOffset, int? StopOffset = null, decimal Pace = 1m);

    private static readonly ClientSeed[] Clients =
    [
        new("Bella Moda", "Bella Moda Comércio de Roupas Ltda.", "Varejo de moda", 25000m, "bellamoda.com.br", (233, 30, 99)),
        new("NutriVida Suplementos", "NutriVida Nutrição Esportiva S.A.", "E-commerce de saúde", 18000m, "nutrivida.com.br", (18, 183, 106)),
        new("Clínica Sorriso Pleno", "Sorriso Pleno Odontologia Ltda.", "Saúde e odontologia", 9000m, "sorrisopleno.com.br", (46, 144, 250)),
        new("Construtora Horizonte", "Horizonte Empreendimentos Imobiliários Ltda.", "Imobiliário", 40000m, "horizonteimoveis.com.br", (16, 24, 40)),
        new("EducaMais Cursos", "EducaMais Ensino a Distância Ltda.", "Educação", 22000m, "educamais.com.br", (105, 65, 198)),
        new("AutoPrime Veículos", "AutoPrime Comércio de Veículos Ltda.", "Automotivo", 30000m, "autoprime.com.br", (240, 68, 56)),
        new("Café Aroma Serrano", "Aroma Serrano Torrefação Ltda.", "Alimentos e bebidas", 8000m, "aromaserrano.com.br", (121, 85, 72)),
        new("TechNova Software", "TechNova Sistemas de Gestão S.A.", "SaaS B2B", 35000m, "technova.com.br", (0, 150, 199)),
        new("Pet&Cia", "Pet e Cia Produtos Veterinários Ltda.", "Pet shop", 12000m, "petecia.com.br", (247, 144, 9)),
        new("Viagens Atlântico", "Atlântico Turismo e Viagens Ltda.", "Turismo", 20000m, "viagensatlantico.com.br", (0, 172, 193), Active: false)
    ];

    private static readonly CampaignSeed[] Campaigns =
    [
        new(0, "Coleção Verão — Vendas", CampaignObjective.Sales, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Good, -75, 15),
        new(0, "Remarketing Carrinho Abandonado", CampaignObjective.Conversions, AdPlatform.GoogleAds, CampaignStatus.Active, Profile.Good, -60, 30),
        new(0, "Reconhecimento de Marca — Reels", CampaignObjective.Awareness, AdPlatform.TikTokAds, CampaignStatus.Completed, Profile.Medium, -88, -40),
        new(1, "Whey Black Friday Antecipada", CampaignObjective.Sales, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Medium, -45, 2, Pace: 1.06m),
        new(1, "Pesquisa — Suplementos", CampaignObjective.Conversions, AdPlatform.GoogleAds, CampaignStatus.Active, Profile.Good, -80, null),
        new(1, "TikTok Creators — Engajamento", CampaignObjective.Engagement, AdPlatform.TikTokAds, CampaignStatus.Paused, Profile.Poor, -50, 20, StopOffset: -20),
        new(2, "Avaliação Gratuita — Leads", CampaignObjective.LeadGeneration, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Good, -70, 20),
        new(2, "Implantes — Pesquisa Local", CampaignObjective.LeadGeneration, AdPlatform.GoogleAds, CampaignStatus.Active, Profile.Medium, -55, 35),
        new(3, "Lançamento Residencial Vista Mar", CampaignObjective.LeadGeneration, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Medium, -85, 5, Pace: 1.02m),
        new(3, "Decisores — LinkedIn", CampaignObjective.LeadGeneration, AdPlatform.LinkedInAds, CampaignStatus.Active, Profile.Poor, -40, 50),
        new(3, "Pesquisa — Apartamentos 2 Quartos", CampaignObjective.Traffic, AdPlatform.GoogleAds, CampaignStatus.Scheduled, Profile.None, 5, 60),
        new(4, "Matrículas 2º Semestre", CampaignObjective.Conversions, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Good, -65, 25),
        new(4, "YouTube — Aulas Abertas", CampaignObjective.Awareness, AdPlatform.GoogleAds, CampaignStatus.Completed, Profile.Medium, -90, -30),
        new(4, "Webinar Gratuito", CampaignObjective.LeadGeneration, AdPlatform.LinkedInAds, CampaignStatus.Active, Profile.Medium, -30, 3, Pace: 0.97m),
        new(5, "Feirão de Seminovos", CampaignObjective.LeadGeneration, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Medium, -35, 10),
        new(5, "SUV Test Drive", CampaignObjective.Conversions, AdPlatform.GoogleAds, CampaignStatus.Paused, Profile.Poor, -60, 30, StopOffset: -15),
        new(6, "Assinatura Mensal de Cafés", CampaignObjective.Sales, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Good, -80, null),
        new(6, "Tráfego para Loja Online", CampaignObjective.Traffic, AdPlatform.TikTokAds, CampaignStatus.Draft, Profile.None, 10, 40),
        new(7, "Demonstração do ERP — B2B", CampaignObjective.LeadGeneration, AdPlatform.LinkedInAds, CampaignStatus.Active, Profile.Good, -70, 20),
        new(7, "Pesquisa — Software de Gestão", CampaignObjective.Conversions, AdPlatform.GoogleAds, CampaignStatus.Active, Profile.Medium, -50, 40),
        new(7, "Retargeting Trial", CampaignObjective.Conversions, AdPlatform.MetaAds, CampaignStatus.Cancelled, Profile.Poor, -45, -25, StopOffset: -30),
        new(8, "Banho & Tosa — Agendamentos", CampaignObjective.Conversions, AdPlatform.MetaAds, CampaignStatus.Active, Profile.Medium, -40, 20),
        new(8, "Ração Premium — Vendas", CampaignObjective.Sales, AdPlatform.Other, CampaignStatus.Active, Profile.Poor, -25, 35),
        new(9, "Pacotes Nordeste — Férias", CampaignObjective.Sales, AdPlatform.MetaAds, CampaignStatus.Completed, Profile.Good, -90, -50),
        new(9, "Reconhecimento — Destinos", CampaignObjective.Awareness, AdPlatform.GoogleAds, CampaignStatus.Completed, Profile.Poor, -85, -55)
    ];

    private static readonly string[] FirstNames = ["Ana", "Bruno", "Carla", "Diego", "Elisa", "Felipe", "Gabriela", "Henrique", "Isabela", "João",
        "Larissa", "Marcelo", "Natália", "Otávio", "Paula", "Ricardo", "Sabrina", "Thiago", "Vanessa", "William", "Yasmin", "Lucas", "Mariana", "Pedro", "Renata"];
    private static readonly string[] LastNames = ["Almeida", "Barbosa", "Cardoso", "Dias", "Esteves", "Ferreira", "Gomes", "Hora", "Ibrahim", "Jardim",
        "Klein", "Lopes", "Moreira", "Nunes", "Oliveira", "Pereira", "Queiroz", "Ramos", "Santos", "Teixeira", "Uchoa", "Vieira", "Xavier", "Zanetti"];

    private readonly Random _rnd = new(2026);
    private DateOnly _today;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Banco já possui dados; seed ignorado.");
            return;
        }
        _today = clock.Today;
        var now = clock.UtcNow;
        logger.LogInformation("Gerando dados de demonstração da agência K2 Digital...");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ---------------- Clientes
        var clients = Clients.Select((c, i) => new Client
        {
            Name = c.Name, CompanyName = c.Company, Email = $"marketing@{c.Domain}", Phone = $"(11) 3{_rnd.Next(100, 999)}-{_rnd.Next(1000, 9999)}",
            Document = $"{_rnd.Next(10, 99)}.{_rnd.Next(100, 999)}.{_rnd.Next(100, 999)}/0001-{_rnd.Next(10, 99)}", Industry = c.Industry,
            Website = $"https://www.{c.Domain}", Status = c.Active ? ClientStatus.Active : ClientStatus.Inactive, MonthlyBudget = c.Monthly,
            CreatedAt = now.AddDays(-120 + i * 3)
        }).ToList();
        db.Clients.AddRange(clients);

        // ---------------- Usuários (credenciais exclusivamente de demonstração — altere em produção)
        var admin = User("Karina Duarte", "admin@k2digital.com.br", "Admin@123", UserRole.Admin, null, now);
        var manager = User("Marcos Lima", "gerente@k2digital.com.br", "Gerente@123", UserRole.Manager, null, now);
        var analyst = User("Juliana Rocha", "analista@k2digital.com.br", "Analista@123", UserRole.Analyst, null, now);
        var analyst2 = User("Rafael Souza", "rafael@k2digital.com.br", "Analista@123", UserRole.Analyst, null, now);
        var viewer = User("Carla Mendes", "cliente@bellamoda.com.br", "Cliente@123", UserRole.ClientViewer, clients[0].Id, now);
        db.Users.AddRange(admin, manager, analyst, analyst2, viewer);

        // ---------------- Criativos (10 por cliente)
        var creativesByClient = new Dictionary<Guid, List<CreativeAsset>>();
        for (var ci = 0; ci < clients.Count; ci++)
        {
            var client = clients[ci];
            var seed = Clients[ci];
            var list = new List<CreativeAsset>();
            var plan = new[] { CreativeType.Image, CreativeType.Image, CreativeType.Image, CreativeType.Image, CreativeType.Banner, CreativeType.Banner,
                CreativeType.Carousel, CreativeType.Video, CreativeType.Text, CreativeType.Text };
            for (var k = 0; k < plan.Length; k++)
                list.Add(await CreativeAsync(client, seed, plan[k], k, now.AddDays(-100 + ci * 4 + k), ct));
            creativesByClient[client.Id] = list;
            db.CreativeAssets.AddRange(list);
        }

        // ---------------- Campanhas, anúncios, orçamentos e métricas
        var campaigns = new List<(Campaign Entity, CampaignSeed Seed)>();
        var adIndex = 0;
        for (var i = 0; i < Campaigns.Length; i++)
        {
            var s = Campaigns[i];
            var client = clients[s.Client];
            var start = _today.AddDays(s.StartOffset);
            DateOnly? end = s.EndOffset is { } e ? _today.AddDays(e) : null;
            var duration = end is { } ed ? ed.DayNumber - start.DayNumber + 1 : 30;
            var share = Campaigns.Count(c => c.Client == s.Client) switch { 1 => 0.8m, 2 => 0.5m, _ => 0.36m };
            var planned = Math.Round(Clients[s.Client].Monthly * share * (end is null ? 1m : duration / 30m) / 100m) * 100m;
            var campaign = new Campaign
            {
                ClientId = client.Id, Name = s.Name, Objective = s.Objective, Platform = s.Platform, Status = s.Status, StartDate = start, EndDate = end,
                PlannedBudget = planned, CreatedAt = now.AddDays(Math.Min(s.StartOffset - 7, -1)), UpdatedAt = now.AddDays(Math.Min(s.StopOffset ?? -1, -1))
            };
            db.Campaigns.Add(campaign);
            campaigns.Add((campaign, s));

            var adCount = i < 10 ? 3 : 2;
            var clientCreatives = creativesByClient[client.Id];
            for (var a = 0; a < adCount; a++)
            {
                var ad = Advertisement(campaign, s, client, a, now);
                var visual = clientCreatives.Where(c => c.Type != CreativeType.Text).ToList();
                ad.Creatives.Add(new AdvertisementCreative { CreativeAssetId = visual[(adIndex + a) % visual.Count].Id, LinkedAt = ad.CreatedAt });
                ad.Creatives.Add(new AdvertisementCreative { CreativeAssetId = clientCreatives.Where(c => c.Type == CreativeType.Text).ElementAt(a % 2).Id, LinkedAt = ad.CreatedAt });
                campaign.Advertisements.Add(ad);
            }
            adIndex += adCount;

            // Orçamentos: período da campanha; campanhas contínuas recebem um orçamento por mês.
            if (s.Status is not CampaignStatus.Draft)
            {
                if (end is { } budgetEnd)
                    campaign.Budgets.Add(new CampaignBudget { PlannedAmount = planned, StartDate = start, EndDate = budgetEnd, CreatedAt = campaign.CreatedAt, Notes = "Verba total da campanha" });
                else
                {
                    for (var month = new DateOnly(start.Year, start.Month, 1); month <= _today; month = month.AddMonths(1))
                        campaign.Budgets.Add(new CampaignBudget
                        {
                            PlannedAmount = planned, StartDate = month < start ? start : month, EndDate = month.AddMonths(1).AddDays(-1),
                            CreatedAt = campaign.CreatedAt, Notes = $"Verba mensal {month:MM/yyyy}"
                        });
                }
            }

            if (s.Profile != Profile.None) GenerateMetrics(campaign, s, duration, planned, now);
        }

        await db.SaveChangesAsync(ct);

        // ---------------- Leads, histórico e conversões
        var leads = GenerateLeads(campaigns, clients, analyst, now);
        await db.SaveChangesAsync(ct);
        GenerateConversions(campaigns, leads, now);
        await db.SaveChangesAsync(ct);

        // ---------------- Tarefas
        GenerateTasks(campaigns, [manager, analyst, analyst2], now);

        db.AuditLogs.Add(new AuditLog { UserEmail = "system", Action = "Seed", Entity = "System", Details = "Dados de demonstração gerados", CreatedAt = now });
        await db.SaveChangesAsync(ct);

        // ---------------- Orçamentos sincronizados com as métricas + notificações
        await budgetSync.SyncAsync(campaigns.Select(c => c.Entity.Id), ct);
        foreach (var lead in leads.OrderByDescending(l => l.CreatedAt).Take(4))
        {
            var clientName = clients.First(c => c.Id == lead.ClientId).Name;
            await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager, UserRole.ClientViewer], NotificationType.NewLead,
                $"Novo lead: {lead.Name}", new { lead = lead.Name, leadId = lead.Id, client = clientName, source = lead.Source },
                $"/leads?leadId={lead.Id}", $"lead:{lead.Id}", lead.ClientId, ct);
        }
        await db.SaveChangesAsync(ct);
        await scanner.RunAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Seed concluído: {Clients} clientes, {Campaigns} campanhas, {Leads} leads.", clients.Count, campaigns.Count, leads.Count);
    }

    private User User(string name, string email, string password, UserRole role, Guid? clientId, DateTime now) => new()
    {
        FullName = name, Email = email, PasswordHash = hasher.Hash(password), Role = role, ClientId = clientId, CreatedAt = now.AddDays(-130)
    };

    private async Task<CreativeAsset> CreativeAsync(Client client, ClientSeed seed, CreativeType type, int k, DateTime createdAt, CancellationToken ct)
    {
        var names = type switch
        {
            CreativeType.Image => new[] { "Foto produto — fundo claro", "Lifestyle — uso real", "Depoimento — cliente", "Oferta da semana" }[k % 4],
            CreativeType.Banner => k % 2 == 0 ? "Banner display 728x90 adaptado" : "Banner topo do site",
            CreativeType.Carousel => "Carrossel — destaques",
            CreativeType.Video => "Vídeo 15s — vertical",
            _ => k % 2 == 0 ? "Copy — benefício principal" : "Copy — prova social"
        };
        var creative = new CreativeAsset
        {
            ClientId = client.Id, Name = $"{seed.Name} · {names}", Type = type, CreatedAt = createdAt,
            Description = type == CreativeType.Video ? "Vídeo de demonstração (arquivo não incluído na demo)." : $"Material de campanha de {seed.Name}."
        };
        if (type is CreativeType.Image or CreativeType.Banner or CreativeType.Carousel)
        {
            var (w, h) = type switch { CreativeType.Banner => (480, 160), CreativeType.Carousel => (320, 320), _ => (360, 240) };
            var (r, g, b) = seed.Color;
            var bytes = DemoImages.Gradient(w, h, (r, g, b), ((byte)(255 - r / 3), (byte)(200 - g / 4), (byte)Math.Min(255, b + 80)), k);
            using var ms = new MemoryStream(bytes);
            creative.FilePath = await storage.SaveAsync(ms, ".png", ct);
            creative.ContentType = "image/png";
            creative.FileSize = bytes.LongLength;
            creative.OriginalFileName = $"{Slug(seed.Name)}-{k + 1}.png";
        }
        else if (type == CreativeType.Video)
        {
            creative.ContentType = "video/mp4";
            creative.OriginalFileName = $"{Slug(seed.Name)}-video.mp4";
        }
        else
        {
            creative.TextContent = k % 2 == 0
                ? $"Descubra por que milhares de clientes escolhem a {seed.Name}. Qualidade, atendimento e condições especiais por tempo limitado."
                : $"\"Melhor decisão que tomei este ano!\" — avaliação 4,9★. Venha conhecer a {seed.Name} e comprove.";
        }
        return creative;
    }

    private Advertisement Advertisement(Campaign campaign, CampaignSeed s, Client client, int variant, DateTime now)
    {
        var cta = s.Objective switch
        {
            CampaignObjective.Sales => "Comprar agora",
            CampaignObjective.LeadGeneration => "Cadastre-se",
            CampaignObjective.Conversions => variant == 0 ? "Saiba mais" : "Agendar",
            CampaignObjective.Traffic => "Visitar site",
            _ => "Saiba mais"
        };
        var label = (char)('A' + variant);
        var headlines = new[] { $"{client.Name}: oferta exclusiva", $"Por que escolher a {client.Name}?", $"Últimos dias — {client.Name}" };
        return new Advertisement
        {
            Name = $"{s.Name} · Variação {label}", Headline = headlines[variant % 3],
            PrimaryText = variant switch
            {
                0 => $"Condições especiais para você. {client.Name} com qualidade comprovada e atendimento próximo.",
                1 => $"Mais de 2 mil clientes satisfeitos. Conheça a {client.Name} e veja a diferença.",
                _ => $"Não deixe para depois: benefícios exclusivos da {client.Name} por tempo limitado."
            },
            Description = "Anúncio de demonstração", CallToAction = cta, DestinationUrl = $"{client.Website}/campanha?utm_source={s.Platform.ToString().ToLowerInvariant()}&utm_content={label}",
            Status = s.Status switch
            {
                CampaignStatus.Active => variant == 2 && s.Profile == Profile.Poor ? AdStatus.Paused : AdStatus.Active,
                CampaignStatus.Paused => AdStatus.Paused,
                CampaignStatus.Completed or CampaignStatus.Cancelled => AdStatus.Archived,
                _ => AdStatus.Draft
            },
            CreatedAt = campaign.CreatedAt.AddHours(variant)
        };
    }

    private void GenerateMetrics(Campaign campaign, CampaignSeed s, int duration, decimal planned, DateTime now)
    {
        var first = campaign.StartDate < _today.AddDays(-89) ? _today.AddDays(-89) : campaign.StartDate;
        var last = _today.AddDays(-1);
        if (campaign.EndDate is { } end && end < last) last = end;
        if (s.StopOffset is { } stop && _today.AddDays(stop) < last) last = _today.AddDays(stop);
        if (first > last) return;

        var dailyBase = (campaign.EndDate is null ? planned / 30m : planned / duration) * s.Pace;
        var (cpmMin, cpmMax) = s.Platform switch
        {
            AdPlatform.GoogleAds => (20m, 34m), AdPlatform.TikTokAds => (8m, 14m), AdPlatform.LinkedInAds => (60m, 90m),
            AdPlatform.Other => (12m, 20m), _ => (14m, 24m)
        };
        var (ctrMin, ctrMax) = s.Profile switch { Profile.Good => (2.0, 3.0), Profile.Medium => (1.0, 1.8), _ => (0.4, 0.8) };
        if (s.Objective is CampaignObjective.Awareness) (ctrMin, ctrMax) = (ctrMin * 0.5, ctrMax * 0.5);
        var leadRate = s.Objective == CampaignObjective.LeadGeneration
            ? s.Profile switch { Profile.Good => 0.10, Profile.Medium => 0.06, _ => 0.02 }
            : s.Objective is CampaignObjective.Awareness or CampaignObjective.Engagement ? 0.002 : 0.02;
        var convRate = s.Objective switch
        {
            CampaignObjective.Sales or CampaignObjective.Conversions => s.Profile switch { Profile.Good => 0.035, Profile.Medium => 0.02, _ => 0.007 },
            CampaignObjective.LeadGeneration => leadRate * 0.25,
            CampaignObjective.Traffic => 0.004,
            _ => 0.001
        };
        var hasRevenue = s.Objective is CampaignObjective.Sales or CampaignObjective.Conversions or CampaignObjective.LeadGeneration;
        var (roasMin, roasMax) = s.Profile switch { Profile.Good => (3.6, 5.6), Profile.Medium => (1.5, 2.6), _ => (0.3, 0.9) };
        if (s.Objective == CampaignObjective.LeadGeneration) (roasMin, roasMax) = (roasMin * 0.6, roasMax * 0.6);

        var ads = campaign.Advertisements;
        var weights = ads.Select((_, i) => i switch { 0 => 0.45, 1 => 0.35, _ => 0.20 }).ToArray();
        var totalWeight = weights.Sum();

        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.85m : 1m;
            for (var i = 0; i < ads.Count; i++)
            {
                var spend = Math.Round(dailyBase * (decimal)(weights[i] / totalWeight) * weekend * (decimal)(0.8 + _rnd.NextDouble() * 0.4), 2);
                if (spend <= 0) continue;
                var cpm = cpmMin + (cpmMax - cpmMin) * (decimal)_rnd.NextDouble();
                var impressions = (long)(spend / cpm * 1000m);
                // A variação C de campanhas fracas tem desempenho ainda pior (ilustra a comparação de anúncios).
                var adFactor = i == 2 ? 0.75 : i == 0 ? 1.1 : 1.0;
                var clicks = Round(impressions * (ctrMin + (ctrMax - ctrMin) * _rnd.NextDouble()) / 100.0 * adFactor);
                var leads = Round(clicks * leadRate * (0.7 + _rnd.NextDouble() * 0.6));
                var conversions = Round(clicks * convRate * (0.6 + _rnd.NextDouble() * 0.8) * adFactor);
                var revenue = hasRevenue && conversions > 0 ? Math.Round(spend * (decimal)(roasMin + (roasMax - roasMin) * _rnd.NextDouble()), 2) : 0m;
                db.CampaignMetrics.Add(new CampaignMetric
                {
                    CampaignId = campaign.Id, AdvertisementId = ads[i].Id, Date = d, Impressions = impressions,
                    Reach = (long)(impressions * (0.6 + _rnd.NextDouble() * 0.2)), Clicks = Math.Min(clicks, impressions), Spend = spend,
                    Leads = leads, Conversions = conversions, AttributedRevenue = revenue, Source = DataSource.Demo, CreatedAt = now
                });
            }
        }
    }

    /// <summary>Arredondamento estocástico: volumes pequenos (ex.: 0,4 conversão/dia) ainda aparecem ao longo do período.</summary>
    private long Round(double value)
    {
        var floor = Math.Floor(value);
        return (long)(floor + (_rnd.NextDouble() < value - floor ? 1 : 0));
    }

    private List<Lead> GenerateLeads(List<(Campaign Entity, CampaignSeed Seed)> campaigns, List<Client> clients, User analyst, DateTime now)
    {
        var eligible = campaigns.Where(c => c.Seed.Profile != Profile.None
                                            && c.Seed.Objective is CampaignObjective.LeadGeneration or CampaignObjective.Conversions or CampaignObjective.Sales).ToList();
        var weights = eligible.Select(c => c.Seed.Objective == CampaignObjective.LeadGeneration ? 3 : 1).ToList();
        var sourceByPlatform = new Dictionary<AdPlatform, string[]>
        {
            [AdPlatform.MetaAds] = ["Formulário Meta", "Landing page", "WhatsApp"],
            [AdPlatform.GoogleAds] = ["Landing page", "Google Ads — Formulário", "Telefone"],
            [AdPlatform.LinkedInAds] = ["LinkedIn Lead Gen", "Landing page"],
            [AdPlatform.TikTokAds] = ["Landing page"],
            [AdPlatform.Other] = ["Landing page", "Indicação"]
        };
        var statusPool = Enumerable.Repeat(LeadStatus.New, 25).Concat(Enumerable.Repeat(LeadStatus.Contacted, 25))
            .Concat(Enumerable.Repeat(LeadStatus.Qualified, 20)).Concat(Enumerable.Repeat(LeadStatus.Converted, 18)).Concat(Enumerable.Repeat(LeadStatus.Lost, 12)).ToArray();

        var leads = new List<Lead>();
        for (var n = 0; n < 150; n++)
        {
            var pick = _rnd.Next(weights.Sum());
            var idx = 0;
            while (pick >= weights[idx]) pick -= weights[idx++];
            var (campaign, seed) = eligible[idx];
            var first = campaign.StartDate < _today.AddDays(-89) ? _today.AddDays(-89) : campaign.StartDate;
            var last = new[] { _today, campaign.EndDate ?? _today, seed.StopOffset is { } s ? _today.AddDays(s) : _today }.Min();
            var day = first.AddDays(_rnd.Next(Math.Max(1, last.DayNumber - first.DayNumber + 1)));
            var created = day.ToDateTime(new TimeOnly(_rnd.Next(8, 22), _rnd.Next(0, 60)), DateTimeKind.Utc).AddHours(3);
            if (created > now) created = now.AddMinutes(-_rnd.Next(30, 600));
            var firstName = FirstNames[_rnd.Next(FirstNames.Length)];
            var lastName = LastNames[_rnd.Next(LastNames.Length)];
            var status = statusPool[_rnd.Next(statusPool.Length)];
            // Leads muito recentes ainda não avançaram no funil.
            if ((now - created).TotalDays < 2) status = LeadStatus.New;
            var lead = new Lead
            {
                ClientId = campaign.ClientId, CampaignId = campaign.Id, Name = $"{firstName} {lastName}",
                Email = $"{Slug(firstName)}.{Slug(lastName)}{n + 1}@exemplo.com.br", Phone = $"(11) 9{_rnd.Next(1000, 9999)}-{_rnd.Next(1000, 9999)}",
                Source = sourceByPlatform[seed.Platform][_rnd.Next(sourceByPlatform[seed.Platform].Length)], Status = status, ConsentGiven = true,
                CreatedAt = created, UpdatedAt = status == LeadStatus.New ? null : created.AddDays(_rnd.Next(1, 6))
            };
            lead.Interactions.Add(new LeadInteraction { Type = LeadInteractionType.StatusChange, ToStatus = LeadStatus.New, Notes = "Lead captado pela campanha.", CreatedAt = created });
            var path = status switch
            {
                LeadStatus.Contacted => new[] { LeadStatus.Contacted },
                LeadStatus.Qualified => [LeadStatus.Contacted, LeadStatus.Qualified],
                LeadStatus.Converted => [LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Converted],
                LeadStatus.Lost => [LeadStatus.Contacted, LeadStatus.Lost],
                _ => []
            };
            var previous = LeadStatus.New;
            var at = created;
            foreach (var step in path)
            {
                at = at.AddHours(_rnd.Next(4, 60));
                if (at > now) at = now.AddMinutes(-5);
                if (step == LeadStatus.Contacted)
                    lead.Interactions.Add(new LeadInteraction { Type = _rnd.Next(2) == 0 ? LeadInteractionType.WhatsApp : LeadInteractionType.Call,
                        Notes = "Primeiro contato realizado; cliente pediu mais informações.", UserId = analyst.Id, UserName = analyst.FullName, CreatedAt = at });
                lead.Interactions.Add(new LeadInteraction
                {
                    Type = LeadInteractionType.StatusChange, FromStatus = previous, ToStatus = step, UserId = analyst.Id, UserName = analyst.FullName, CreatedAt = at,
                    Notes = step switch { LeadStatus.Qualified => "Perfil aderente e orçamento confirmado.", LeadStatus.Converted => "Fechamento confirmado.",
                        LeadStatus.Lost => "Sem interesse no momento.", _ => string.Empty }
                });
                previous = step;
            }
            lead.UpdatedAt = path.Length > 0 ? at : null;
            leads.Add(lead);
        }
        db.Leads.AddRange(leads);
        return leads;
    }

    private void GenerateConversions(List<(Campaign Entity, CampaignSeed Seed)> campaigns, List<Lead> leads, DateTime now)
    {
        var byId = campaigns.ToDictionary(c => c.Entity.Id);
        var appointmentIndustries = new[] { "Saúde e odontologia", "Pet shop", "Automotivo" };
        var orderNumber = 10000;

        decimal Ticket(CampaignSeed s) => s.Client switch
        {
            3 => 4500m + _rnd.Next(0, 3000), 5 => 2500m + _rnd.Next(0, 2000), 7 => 1800m + _rnd.Next(0, 1500), 9 => 2200m + _rnd.Next(0, 1800),
            4 => 890m + _rnd.Next(0, 600), 2 => 350m + _rnd.Next(0, 900), _ => 120m + _rnd.Next(0, 380)
        };

        foreach (var lead in leads)
        {
            var (campaign, seed) = byId[lead.CampaignId!.Value];
            // Evento "Lead" registrado para parte dos leads (um por lead — chave de deduplicação lead:{id}:Lead).
            if (_rnd.NextDouble() < 0.35)
                db.Conversions.Add(Conversion(campaign.Id, lead.Id, ConversionType.Lead, lead.CreatedAt.AddMinutes(1), 0m, null, now));
            if (lead.Status == LeadStatus.Converted)
            {
                var industry = Clients[seed.Client].Industry;
                var type = appointmentIndustries.Contains(industry) ? ConversionType.Appointment : ConversionType.Purchase;
                var date = (lead.UpdatedAt ?? lead.CreatedAt).AddMinutes(10);
                db.Conversions.Add(Conversion(campaign.Id, lead.Id, type, date > now ? now.AddMinutes(-1) : date, Ticket(seed), $"PED-{orderNumber++}", now));
            }
        }

        // Vendas de e-commerce sem lead vinculado (pedidos atribuídos às campanhas de vendas).
        var sales = campaigns.Where(c => c.Seed.Objective == CampaignObjective.Sales && c.Seed.Profile != Profile.None).ToList();
        var used = new HashSet<string>();
        for (var n = 0; n < 45; n++)
        {
            var (campaign, seed) = sales[n % sales.Count];
            var first = campaign.StartDate < _today.AddDays(-89) ? _today.AddDays(-89) : campaign.StartDate;
            var last = new[] { _today.AddDays(-1), campaign.EndDate ?? _today, seed.StopOffset is { } s ? _today.AddDays(s) : _today }.Min();
            var date = first.AddDays(_rnd.Next(Math.Max(1, last.DayNumber - first.DayNumber + 1))).ToDateTime(new TimeOnly(_rnd.Next(9, 23), _rnd.Next(60)), DateTimeKind.Utc);
            string order;
            do order = $"WEB-{_rnd.Next(100000, 999999)}"; while (!used.Add(order));
            db.Conversions.Add(Conversion(campaign.Id, null, ConversionType.Purchase, date, Ticket(seed), order, now));
        }
    }

    private static Conversion Conversion(Guid campaignId, Guid? leadId, ConversionType type, DateTime date, decimal revenue, string? externalId, DateTime now) => new()
    {
        CampaignId = campaignId, LeadId = leadId, ConversionType = type, ConversionDate = date, RevenueAmount = revenue, Source = DataSource.Demo,
        ExternalId = externalId, EventKey = ConversionKeys.For(leadId, type, date, revenue, externalId), CreatedAt = now
    };

    private void GenerateTasks(List<(Campaign Entity, CampaignSeed Seed)> campaigns, User[] team, DateTime now)
    {
        var titles = new[]
        {
            "Revisar segmentação de público", "Criar novas variações de copy", "Enviar relatório semanal ao cliente", "Ajustar lances por dispositivo",
            "Subir criativos aprovados", "Validar pixel e eventos de conversão", "Reunião de alinhamento mensal", "Analisar termos de pesquisa negativos",
            "Atualizar landing page com nova oferta", "Teste A/B de headline", "Revisar orçamento do mês", "Exportar leads para o CRM do cliente"
        };
        var active = campaigns.Where(c => c.Entity.Status is CampaignStatus.Active or CampaignStatus.Paused or CampaignStatus.Scheduled).ToList();
        for (var n = 0; n < 30; n++)
        {
            var (campaign, _) = active[n % active.Count];
            var due = _today.AddDays(_rnd.Next(-10, 21));
            var status = due < _today
                ? (_rnd.NextDouble() < 0.6 ? MarketingTaskStatus.Done : MarketingTaskStatus.Todo)
                : _rnd.NextDouble() < 0.25 ? MarketingTaskStatus.InProgress : _rnd.NextDouble() < 0.1 ? MarketingTaskStatus.Done : MarketingTaskStatus.Todo;
            db.MarketingTasks.Add(new MarketingTask
            {
                ClientId = campaign.ClientId, CampaignId = campaign.Id, Title = titles[n % titles.Length],
                Description = $"Atividade da campanha \"{campaign.Name}\".", AssignedToId = n % 7 == 6 ? null : team[n % team.Length].Id,
                Priority = (TaskPriority)(_rnd.Next(4)), DueDate = due, Status = status,
                CompletedAt = status == MarketingTaskStatus.Done ? now.AddDays(-_rnd.Next(0, 5)) : null, CreatedAt = now.AddDays(-_rnd.Next(5, 20))
            });
        }
    }

    private static string Slug(string s)
    {
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        return string.Join('-', new string(chars.ToArray()).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
