using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Auth;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Users;

public record UserDto(Guid Id, string FullName, string Email, UserRole Role, Guid? ClientId, string? ClientName,
    bool Active, DateTime CreatedAt, DateTime? LastLoginAt);
public record UserLookupDto(Guid Id, string FullName, UserRole Role);
public record CreateUserRequest(string FullName, string Email, string Password, UserRole Role, Guid? ClientId);
public record UpdateUserRequest(string FullName, string Email, UserRole Role, Guid? ClientId, bool Active);
public record ResetPasswordRequest(string NewPassword);
public record RoleDto(UserRole Id, string Name, string Description);

public class UserQuery : PageQuery
{
    public UserRole? Role { get; set; }
    public bool? Active { get; set; }
}

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).SetValidator(new PasswordPolicyValidator());
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ClientId).NotNull().When(x => x.Role == UserRole.ClientViewer)
            .WithMessage("Usuários do perfil Cliente precisam estar vinculados a um cliente.");
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ClientId).NotNull().When(x => x.Role == UserRole.ClientViewer)
            .WithMessage("Usuários do perfil Cliente precisam estar vinculados a um cliente.");
    }
}

public class UserService(
    IAppDbContext db, IPasswordHasher hasher, IClock clock, ICurrentUser current, AuditService audit,
    IValidator<CreateUserRequest> createValidator, IValidator<UpdateUserRequest> updateValidator)
{
    private static readonly Dictionary<string, Expression<Func<User, object?>>> Sort = new()
    {
        ["fullName"] = u => u.FullName, ["email"] = u => u.Email, ["role"] = u => u.Role,
        ["createdAt"] = u => u.CreatedAt, ["lastLoginAt"] = u => u.LastLoginAt
    };

    public async Task<PagedResult<UserDto>> ListAsync(UserQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s));
        }
        if (q.Role is { } role) query = query.Where(u => u.Role == role);
        if (q.Active is { } active) query = query.Where(u => u.Active == active);
        return await query.ApplySort(q, Sort, "fullName")
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.ClientId, u.Client != null ? u.Client.Name : null,
                u.Active, u.CreatedAt, u.LastLoginAt))
            .ToPagedAsync(q, ct);
    }

    /// <summary>Usuários da agência que podem receber tarefas.</summary>
    public Task<List<UserLookupDto>> AssignableAsync(CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Active && u.Role != UserRole.ClientViewer).OrderBy(u => u.FullName)
            .Select(u => new UserLookupDto(u.Id, u.FullName, u.Role)).ToListAsync(ct);

    public Task<List<RoleDto>> RolesAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RoleDto(r.Id, r.Name, r.Description)).ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserRequest r, CancellationToken ct)
    {
        await createValidator.ValidateOrThrowAsync(r, ct);
        var email = r.Email.Trim().ToLowerInvariant();
        await EnsureEmailFreeAsync(email, null, ct);
        var clientId = await ResolveClientAsync(r.Role, r.ClientId, ct);
        var user = new User
        {
            FullName = r.FullName.Trim(), Email = email, PasswordHash = hasher.Hash(r.Password), Role = r.Role,
            ClientId = clientId, CreatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        audit.Log("Create", "User", user.Id, $"{email} ({r.Role})");
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest r, CancellationToken ct)
    {
        await updateValidator.ValidateOrThrowAsync(r, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuário", id);
        if (id == current.UserId && (!r.Active || r.Role != UserRole.Admin))
            throw new DomainException("user.selfLockout", "Você não pode desativar ou rebaixar o próprio usuário.");
        var email = r.Email.Trim().ToLowerInvariant();
        await EnsureEmailFreeAsync(email, id, ct);
        user.FullName = r.FullName.Trim();
        user.Email = email;
        user.Role = r.Role;
        user.ClientId = await ResolveClientAsync(r.Role, r.ClientId, ct);
        if (user.Active && !r.Active) await RevokeSessionsAsync(id, ct);
        user.Active = r.Active;
        audit.Log("Update", "User", id, $"{email} ({r.Role}, ativo={r.Active})");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest r, CancellationToken ct)
    {
        var result = await new PasswordPolicyValidator().ValidateAsync(r.NewPassword ?? string.Empty, ct);
        if (!result.IsValid)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["newPassword"] = result.Errors.Select(e => e.ErrorMessage).ToArray() });
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuário", id);
        user.PasswordHash = hasher.Hash(r.NewPassword!);
        await RevokeSessionsAsync(id, ct);
        audit.Log("ResetPassword", "User", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<UserDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.ClientId, u.Client != null ? u.Client.Name : null,
                u.Active, u.CreatedAt, u.LastLoginAt))
            .FirstAsync(ct);

    private async Task EnsureEmailFreeAsync(string email, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != exceptId, ct))
            throw new ConflictException("user.emailTaken", "Já existe um usuário com este e-mail.");
    }

    /// <summary>Somente CLIENT_VIEWER fica vinculado a um cliente; os perfis da agência veem todos.</summary>
    private async Task<Guid?> ResolveClientAsync(UserRole role, Guid? clientId, CancellationToken ct)
    {
        if (role != UserRole.ClientViewer) return null;
        if (clientId is null || !await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            throw new NotFoundException("Cliente", clientId ?? Guid.Empty);
        return clientId;
    }

    private Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        return db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
