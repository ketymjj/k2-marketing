// Espelho dos DTOs da API v1 (enums serializados como texto).

export type UserRole = 'Admin' | 'Manager' | 'Analyst' | 'ClientViewer';
export type ClientStatus = 'Active' | 'Inactive';
export type CampaignObjective = 'Awareness' | 'Traffic' | 'Engagement' | 'LeadGeneration' | 'Conversions' | 'Sales';
export type AdPlatform = 'MetaAds' | 'GoogleAds' | 'TikTokAds' | 'LinkedInAds' | 'Other';
export type CampaignStatus = 'Draft' | 'Scheduled' | 'Active' | 'Paused' | 'Completed' | 'Cancelled';
export type AdStatus = 'Draft' | 'Active' | 'Paused' | 'Archived';
export type CreativeType = 'Image' | 'Video' | 'Banner' | 'Carousel' | 'Text';
export type LeadStatus = 'New' | 'Contacted' | 'Qualified' | 'Converted' | 'Lost';
export type LeadInteractionType = 'Note' | 'Call' | 'Email' | 'WhatsApp' | 'Meeting' | 'StatusChange';
export type ConversionType = 'Lead' | 'SignUp' | 'Contact' | 'Purchase' | 'Appointment' | 'Other';
export type DataSource = 'Manual' | 'CsvImport' | 'Integration' | 'Demo';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type TaskStatus = 'Todo' | 'InProgress' | 'Done' | 'Cancelled';
export type NotificationType = 'CampaignEnding' | 'BudgetThreshold' | 'TaskOverdue' | 'NewLead' | 'ReportAvailable' | 'System';
export type ImportRowStatus = 'New' | 'Duplicate' | 'DuplicateInFile' | 'Invalid';

export const OBJECTIVES: CampaignObjective[] = ['Awareness', 'Traffic', 'Engagement', 'LeadGeneration', 'Conversions', 'Sales'];
export const PLATFORMS: AdPlatform[] = ['MetaAds', 'GoogleAds', 'TikTokAds', 'LinkedInAds', 'Other'];
export const CAMPAIGN_STATUSES: CampaignStatus[] = ['Draft', 'Scheduled', 'Active', 'Paused', 'Completed', 'Cancelled'];
export const AD_STATUSES: AdStatus[] = ['Draft', 'Active', 'Paused', 'Archived'];
export const CREATIVE_TYPES: CreativeType[] = ['Image', 'Video', 'Banner', 'Carousel', 'Text'];
export const LEAD_STATUSES: LeadStatus[] = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];
export const INTERACTION_TYPES: LeadInteractionType[] = ['Note', 'Call', 'Email', 'WhatsApp', 'Meeting'];
export const CONVERSION_TYPES: ConversionType[] = ['Lead', 'SignUp', 'Contact', 'Purchase', 'Appointment', 'Other'];
export const DATA_SOURCES: DataSource[] = ['Manual', 'CsvImport', 'Integration', 'Demo'];
export const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];
export const TASK_STATUSES: TaskStatus[] = ['Todo', 'InProgress', 'Done', 'Cancelled'];
export const ROLES: UserRole[] = ['Admin', 'Manager', 'Analyst', 'ClientViewer'];

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; }
export interface ApiProblem { title?: string; status?: number; code?: string; errors?: Record<string, string[]>; }

// ---------- Auth / usuários
export interface CurrentUser { id: string; fullName: string; email: string; role: UserRole; clientId: string | null; clientName: string | null; preferredLanguage: string; }
export interface AuthResponse { accessToken: string; expiresAt: string; refreshToken: string; user: CurrentUser; }
export interface User { id: string; fullName: string; email: string; role: UserRole; clientId: string | null; clientName: string | null; active: boolean; createdAt: string; lastLoginAt: string | null; }
export interface UserLookup { id: string; fullName: string; role: UserRole; }

// ---------- Métricas
export interface Metrics {
  impressions: number; reach: number; clicks: number; spend: number; leads: number; conversions: number; attributedRevenue: number;
  ctr: number | null; cpc: number | null; cpm: number | null; cpl: number | null; cpa: number | null; roas: number | null;
}
export interface DailyPoint { date: string; impressions: number; clicks: number; spend: number; leads: number; conversions: number; revenue: number; roas: number | null; }

// ---------- Clientes
export interface ClientListItem { id: string; name: string; companyName: string; email: string; phone: string | null; industry: string | null; status: ClientStatus; monthlyBudget: number; activeCampaigns: number; totalCampaigns: number; createdAt: string; }
export interface Client { id: string; name: string; companyName: string; email: string; phone: string | null; document: string | null; industry: string | null; website: string | null; status: ClientStatus; monthlyBudget: number; createdAt: string; updatedAt: string | null; }
export interface ClientOverview { client: Client; totalCampaigns: number; activeCampaigns: number; totalAds: number; totalCreatives: number; leadsRegistered: number; conversionsRegistered: number; salesRevenue: number; allTime: Metrics; currentMonth: Metrics; monthlyBudgetUsage: number | null; }
export interface Lookup { id: string; name: string; }
export interface ClientLookup extends Lookup { status: ClientStatus; }
export interface HistoryItem { createdAt: string; userEmail: string; action: string; entity: string; entityId: string | null; details: string | null; }

// ---------- Campanhas
export interface CampaignListItem { id: string; clientId: string; clientName: string; name: string; objective: CampaignObjective; platform: AdPlatform; status: CampaignStatus; startDate: string; endDate: string | null; plannedBudget: number; adsCount: number; spend: number; clicks: number; conversions: number; roas: number | null; updatedAt: string; }
export interface Campaign { id: string; clientId: string; clientName: string; name: string; objective: CampaignObjective; platform: AdPlatform; status: CampaignStatus; startDate: string; endDate: string | null; plannedBudget: number; createdAt: string; updatedAt: string; allowedTransitions: CampaignStatus[]; adsCount: number; leadsRegistered: number; conversionsRegistered: number; metrics: Metrics; budgetPlanned: number; budgetSpent: number; budgetUsage: number | null; }
export interface CampaignLookup extends Lookup { clientId: string; platform: AdPlatform; status: CampaignStatus; }
export interface AdPerformance { advertisementId: string; name: string; status: AdStatus; metrics: Metrics; }
export interface CampaignPerformance { campaignId: string; from: string; to: string; totals: Metrics; daily: DailyPoint[]; byAd: AdPerformance[]; campaignLevelSpend: number; }

// ---------- Anúncios / criativos
export interface LinkedCreative { id: string; name: string; type: CreativeType; contentType: string | null; hasFile: boolean; textContent: string | null; }
export interface Advertisement { id: string; campaignId: string; campaignName: string; clientId: string; clientName: string; platform: AdPlatform; name: string; headline: string; primaryText: string; description: string | null; callToAction: string; destinationUrl: string; status: AdStatus; createdAt: string; updatedAt: string | null; creatives: LinkedCreative[]; metrics: Metrics; }
export interface Creative { id: string; clientId: string; clientName: string; name: string; type: CreativeType; originalFileName: string | null; contentType: string | null; fileSize: number; hasFile: boolean; description: string | null; textContent: string | null; adsCount: number; createdAt: string; }

// ---------- Orçamentos
export interface Budget { id: string; campaignId: string; campaignName: string; clientId: string; clientName: string; platform: AdPlatform; plannedAmount: number; actualSpend: number; remaining: number; usagePercent: number | null; threshold: number; startDate: string; endDate: string; notes: string | null; createdAt: string; }
export interface BudgetSummary { planned: number; spent: number; remaining: number; usagePercent: number | null; count: number; over80: number; over90: number; over100: number; items: Budget[]; }
export interface BudgetHistoryPoint { date: string; spend: number; cumulative: number; plannedCumulative: number; }
export interface BudgetDetail { budget: Budget; history: BudgetHistoryPoint[]; }

// ---------- Leads / conversões
export interface Lead { id: string; clientId: string; clientName: string; campaignId: string | null; campaignName: string | null; name: string; email: string | null; phone: string | null; source: string | null; status: LeadStatus; consentGiven: boolean; createdAt: string; updatedAt: string | null; }
export interface LeadInteraction { id: string; type: LeadInteractionType; notes: string; fromStatus: LeadStatus | null; toStatus: LeadStatus | null; userName: string | null; createdAt: string; }
export interface LeadDetail { lead: Lead; interactions: LeadInteraction[]; conversionsCount: number; conversionsRevenue: number; }
export interface BoardColumn { status: LeadStatus; count: number; items: Lead[]; }
export interface Conversion { id: string; campaignId: string; campaignName: string; clientId: string; clientName: string; leadId: string | null; leadName: string | null; conversionType: ConversionType; conversionDate: string; revenueAmount: number; source: DataSource; externalId: string | null; notes: string | null; createdAt: string; }
export interface ConversionSummary { totalConversions: number; uniqueLeadsConverted: number; sales: number; salesRevenue: number; totalRevenue: number; byType: { type: ConversionType; count: number; revenue: number }[]; }

// ---------- Métricas (linhas) / importação
export interface MetricRow extends Metrics { id: string; campaignId: string; campaignName: string; clientId: string; clientName: string; advertisementId: string | null; advertisementName: string | null; date: string; source: DataSource; createdAt: string; updatedAt: string | null; }
export interface ImportRow { line: number; status: ImportRowStatus; campaign: string | null; advertisement: string | null; date: string | null; values: Record<string, string>; errors: string[]; }
export interface ImportPreview { kind: string; totalRows: number; newRows: number; duplicates: number; duplicatesInFile: number; invalid: number; missingColumns: string[]; ignoredColumns: string[]; rows: ImportRow[]; validTotals: Metrics | null; validRevenue: number | null; }
export interface ImportResult { kind: string; inserted: number; updated: number; skipped: number; invalid: number; }

// ---------- Dashboard / comparação / relatórios
export interface CampaignRoas { campaignId: string; name: string; clientName: string; platform: AdPlatform; spend: number; revenue: number; conversions: number; roas: number | null; }
export interface PlatformBreakdown { platform: AdPlatform; metrics: Metrics; }
export interface BudgetSlice { key: string; planned: number; spent: number; }
export interface CrmSummary { leadsRegistered: number; uniqueLeadsConverted: number; conversionsRegistered: number; sales: number; salesRevenue: number; }
export interface Dashboard {
  from: string; to: string; previousFrom: string; previousTo: string; activeClients: number; activeCampaigns: number;
  metrics: Metrics; previous: Metrics; daily: DailyPoint[]; roasByCampaign: CampaignRoas[]; byPlatform: PlatformBreakdown[];
  budgetDistribution: BudgetSlice[]; crm: CrmSummary; dataSources: { source: DataSource; rows: number }[];
}
export interface CompareItem { campaignId: string; name: string; clientName: string; platform: AdPlatform; objective: CampaignObjective; status: CampaignStatus; metrics: Metrics; daysWithData: number; sufficientVolume: boolean; qualityFlags: string[]; daily: DailyPoint[]; }
export interface CompareResult { from: string; to: string; items: CompareItem[]; highlights: { metric: string; campaignId: string; value: number; higherIsBetter: boolean }[]; warnings: { code: string; campaign: string | null }[]; }
export interface PerformanceRow { key: string; label: string; detail: string | null; metrics: Metrics; }
export interface PerformanceReport { from: string; to: string; groupBy: string; rows: PerformanceRow[]; totals: Metrics; }
export interface CountItem { key: string; label: string | null; count: number; converted: number; }
export interface DayCount { date: string; count: number; revenue: number; }
export interface LeadsReport { from: string; to: string; total: number; converted: number; conversionRate: number | null; byStatus: CountItem[]; bySource: CountItem[]; byCampaign: CountItem[]; daily: DayCount[]; platformLeads: number; }
export interface ConversionsReport { from: string; to: string; total: number; uniqueLeads: number; sales: number; salesRevenue: number; totalRevenue: number; byType: CountItem[]; byCampaign: CountItem[]; daily: DayCount[]; }
export interface Observation { code: string; params: Record<string, unknown>; tone: 'positive' | 'negative' | 'neutral'; }
export interface AgencyBrand { agencyName: string; primaryColor: string; contactEmail: string | null; website: string | null; reportFooter: string | null; }
export interface ExecutiveReport {
  agency: AgencyBrand; from: string; to: string; previousFrom: string; previousTo: string; clientName: string | null;
  metrics: Metrics; previous: Metrics; daily: DailyPoint[]; byPlatform: PlatformBreakdown[]; topCampaigns: PerformanceRow[];
  budget: BudgetSummary; crm: CrmSummary; observations: Observation[]; generatedAt: string;
}

// ---------- Tarefas / notificações / configurações
export interface MarketingTask { id: string; clientId: string | null; clientName: string | null; campaignId: string | null; campaignName: string | null; title: string; description: string | null; assignedToId: string | null; assignedToName: string | null; priority: TaskPriority; dueDate: string; status: TaskStatus; overdue: boolean; completedAt: string | null; createdAt: string; }
export interface TaskStats { open: number; overdue: number; dueToday: number; doneThisWeek: number; }
export interface AppNotification { id: string; type: NotificationType; title: string; data: Record<string, unknown>; link: string | null; createdAt: string; readAt: string | null; }
export interface AgencySettings { agencyName: string; primaryColor: string; contactEmail: string | null; website: string | null; reportFooter: string | null; currency: string; }
export interface AuditLog { id: number; createdAt: string; userEmail: string; action: string; entity: string; entityId: string | null; details: string | null; }
export interface Integration { key: string; name: string; category: string; status: 'NotConfigured' | 'Configured' | 'Error'; description: string; }
