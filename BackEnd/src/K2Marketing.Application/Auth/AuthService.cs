using FluentValidation;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Auth;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record PreferencesRequest(string Language);
public record CurrentUserDto(Guid Id, string FullName, string Email, UserRole Role, Guid? ClientId, string? ClientName, string PreferredLanguage);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, string RefreshToken, CurrentUserDto User);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).SetValidator(new PasswordPolicyValidator());
    }
}

/// <summary>Senha com no mínimo 8 caracteres, letras maiúsculas, minúsculas e números.</summary>
public class PasswordPolicyValidator : AbstractValidator<string>
{
    public PasswordPolicyValidator()
    {
        RuleFor(x => x).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("A senha deve conter uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("A senha deve conter uma letra minúscula.")
            .Matches("[0-9]").WithMessage("A senha deve conter um número.")
            .OverridePropertyName("password");
    }
}

public class AuthService(
    IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IClock clock, AuditService audit,
    IValidator<LoginRequest> loginValidator, IValidator<ChangePasswordRequest> passwordValidator)
{
    public static readonly string[] Languages = ["pt-BR", "en-US"];

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateOrThrowAsync(request, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.Active || !hasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("auth.invalidCredentials", "E-mail ou senha inválidos.");
        if (user.Role == UserRole.ClientViewer && user.Client?.Status != ClientStatus.Active)
            throw new UnauthorizedException("auth.clientInactive", "O acesso deste cliente está desativado.");

        user.LastLoginAt = clock.UtcNow;
        return await IssueAsync(user, ct);
    }

    /// <summary>
    /// Rotação de refresh token: cada token é de uso único. Reutilizar um token já rotacionado
    /// indica vazamento, e todos os tokens do usuário são revogados.
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashToken(request.RefreshToken ?? string.Empty);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("auth.invalidRefresh", "Sessão inválida. Faça login novamente.");

        var now = clock.UtcNow;
        if (stored.RevokedAt is not null)
        {
            await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            throw new UnauthorizedException("auth.refreshReused", "Sessão revogada por segurança. Faça login novamente.");
        }
        if (!stored.IsActive(now))
            throw new UnauthorizedException("auth.refreshExpired", "Sessão expirada. Faça login novamente.");

        var user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.Active)
            throw new UnauthorizedException("auth.invalidRefresh", "Sessão inválida. Faça login novamente.");

        return await IssueAsync(user, ct, stored);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashToken(request.RefreshToken ?? string.Empty);
        var now = clock.UtcNow;
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public async Task<CurrentUserDto> MeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Usuário", userId);
        return ToDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        await passwordValidator.ValidateOrThrowAsync(request, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Usuário", userId);
        if (!hasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new DomainException("auth.wrongPassword", "Senha atual incorreta.");
        user.PasswordHash = hasher.Hash(request.NewPassword);
        var now = clock.UtcNow;
        // Encerra as demais sessões após a troca de senha.
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        audit.Log("ChangePassword", "User", userId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUserDto> UpdatePreferencesAsync(Guid userId, PreferencesRequest request, CancellationToken ct)
    {
        if (!Languages.Contains(request.Language))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["language"] = ["Idioma não suportado."] });
        var user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Usuário", userId);
        user.PreferredLanguage = request.Language;
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        var refreshHash = tokens.HashToken(refresh);
        var now = clock.UtcNow;

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByHash = refreshHash;
        }
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = refreshHash, CreatedAt = now, ExpiresAt = now.Add(tokens.RefreshTokenLifetime)
        });
        await db.SaveChangesAsync(ct);
        return new AuthResponse(access, expires, refresh, ToDto(user));
    }

    private static CurrentUserDto ToDto(User u) =>
        new(u.Id, u.FullName, u.Email, u.Role, u.ClientId, u.Client?.Name, u.PreferredLanguage);
}
