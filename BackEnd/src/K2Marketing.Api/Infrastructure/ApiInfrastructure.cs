using System.Security.Claims;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace K2Marketing.Api.Infrastructure;

/// <summary>Perfis aceitos por cada grupo de endpoints (RBAC). Leituras são liberadas a todos e filtradas pelo AccessScope.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    /// <summary>Gestão de clientes, orçamentos e exclusões.</summary>
    public const string Management = "Admin,Manager";
    /// <summary>Operação: campanhas, anúncios, criativos, métricas, leads, conversões e tarefas.</summary>
    public const string Operations = "Admin,Manager,Analyst";
    public const string Agency = Operations;
    public const string Any = "Admin,Manager,Analyst,ClientViewer";
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    public string Email => Principal?.FindFirstValue("email") ?? string.Empty;
    public string Name => Principal?.FindFirstValue("name") ?? string.Empty;
    /// <summary>Sem perfil válido, assume o mais restrito.</summary>
    public UserRole Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : UserRole.ClientViewer;
    public Guid? ClientId => Guid.TryParse(Principal?.FindFirstValue(K2Claims.Client), out var id) ? id : null;
}

/// <summary>Converte exceções em ProblemDetails com um código estável (traduzido no frontend).</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, code, title) = ex switch
            {
                RequestValidationException v => (StatusCodes.Status400BadRequest, v.Code, v.Message),
                NotFoundException n => (StatusCodes.Status404NotFound, n.Code, n.Message),
                ForbiddenException f => (StatusCodes.Status403Forbidden, f.Code, f.Message),
                UnauthorizedException u => (StatusCodes.Status401Unauthorized, u.Code, u.Message),
                ConflictException c => (StatusCodes.Status409Conflict, c.Code, c.Message),
                DomainException d => (StatusCodes.Status422UnprocessableEntity, d.Code, d.Message),
                DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                    (StatusCodes.Status409Conflict, "conflict", "Registro duplicado."),
                DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                    (StatusCodes.Status409Conflict, "inUse", "O registro está em uso por outros dados."),
                BadHttpRequestException b => (b.StatusCode, "badRequest", "Requisição inválida."),
                OperationCanceledException => (499, "cancelled", "Requisição cancelada."),
                _ => (StatusCodes.Status500InternalServerError, "internal", "Ocorreu um erro inesperado.")
            };
            if (status >= 500) logger.LogError(ex, "Erro não tratado em {Path}", context.Request.Path);
            else logger.LogInformation("Requisição rejeitada {Status} {Code}: {Message}", status, code, ex.Message);

            var problem = new ProblemDetails { Status = status, Title = title, Instance = context.Request.Path };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (ex is RequestValidationException rv) problem.Extensions["errors"] = rv.Errors;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        }
    }
}

/// <summary>Cabeçalhos de segurança aplicados a todas as respostas da API.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}
