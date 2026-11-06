using K2Marketing.Api.Infrastructure;
using K2Marketing.Application.Auth;
using K2Marketing.Application.Common;
using K2Marketing.Application.Integrations;
using K2Marketing.Application.Settings;
using K2Marketing.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K2Marketing.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase;

[Route("api/v1/auth")]
public class AuthController(AuthService auth, ICurrentUser user) : ApiControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request, ct);

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) => auth.MeAsync(user.UserId, ct);

    [HttpPut("me/preferences")]
    public Task<CurrentUserDto> Preferences(PreferencesRequest request, CancellationToken ct) => auth.UpdatePreferencesAsync(user.UserId, request, ct);

    [HttpPost("change-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(user.UserId, request, ct);
        return NoContent();
    }
}

[Route("api/v1/users")]
public class UsersController(UserService users) : ApiControllerBase
{
    [HttpGet, Authorize(Roles = Roles.Admin)]
    public Task<PagedResult<UserDto>> List([FromQuery] UserQuery q, CancellationToken ct) => users.ListAsync(q, ct);

    [HttpGet("roles"), Authorize(Roles = Roles.Admin)]
    public Task<List<RoleDto>> RolesList(CancellationToken ct) => users.RolesAsync(ct);

    /// <summary>Usuários da agência que podem ser responsáveis por tarefas.</summary>
    [HttpGet("assignable"), Authorize(Roles = Roles.Operations)]
    public Task<List<UserLookupDto>> Assignable(CancellationToken ct) => users.AssignableAsync(ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/reset-password"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }
}

[Route("api/v1/settings")]
public class SettingsController(SettingsService settings, IntegrationCatalog integrations) : ApiControllerBase
{
    /// <summary>Identidade visual da agência (usada no cabeçalho e nos relatórios de todos os perfis).</summary>
    [HttpGet("agency")]
    public Task<AgencySettingsDto> Agency(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut("agency"), Authorize(Roles = Roles.Admin)]
    public Task<AgencySettingsDto> UpdateAgency(AgencySettingsDto request, CancellationToken ct) => settings.UpdateAsync(request, ct);

    [HttpGet("audit"), Authorize(Roles = Roles.Admin)]
    public Task<PagedResult<AuditLogDto>> Audit([FromQuery] AuditQuery q, CancellationToken ct) => settings.AuditAsync(q, ct);

    /// <summary>Status das integrações futuras (todas "não configuradas" nesta versão).</summary>
    [HttpGet("integrations"), Authorize(Roles = Roles.Agency)]
    public List<IntegrationInfoDto> Integrations() => integrations.List();
}
