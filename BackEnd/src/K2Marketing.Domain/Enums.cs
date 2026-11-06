namespace K2Marketing.Domain;

/// <summary>Perfis de acesso (RBAC). O valor numérico é a chave da tabela Roles.</summary>
public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Analyst = 3,
    ClientViewer = 4
}

public enum ClientStatus { Active, Inactive }

public enum CampaignObjective { Awareness, Traffic, Engagement, LeadGeneration, Conversions, Sales }

public enum AdPlatform { MetaAds, GoogleAds, TikTokAds, LinkedInAds, Other }

public enum CampaignStatus { Draft, Scheduled, Active, Paused, Completed, Cancelled }

public enum AdStatus { Draft, Active, Paused, Archived }

public enum CreativeType { Image, Video, Banner, Carousel, Text }

public enum LeadStatus { New, Contacted, Qualified, Converted, Lost }

public enum LeadInteractionType { Note, Call, Email, WhatsApp, Meeting, StatusChange }

public enum ConversionType { Lead, SignUp, Contact, Purchase, Appointment, Other }

/// <summary>Origem de um dado: digitado, importado (CSV), integração futura ou demonstração.</summary>
public enum DataSource { Manual, CsvImport, Integration, Demo }

public enum TaskPriority { Low, Medium, High, Urgent }

public enum MarketingTaskStatus { Todo, InProgress, Done, Cancelled }

public enum NotificationType { CampaignEnding, BudgetThreshold, TaskOverdue, NewLead, ReportAvailable, System }
