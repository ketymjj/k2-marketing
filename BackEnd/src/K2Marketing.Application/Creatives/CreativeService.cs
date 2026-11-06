using System.Linq.Expressions;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Creatives;

public record CreativeDto(Guid Id, Guid ClientId, string ClientName, string Name, CreativeType Type, string? OriginalFileName,
    string? ContentType, long FileSize, bool HasFile, string? Description, string? TextContent, int AdsCount, DateTime CreatedAt);

public record UploadCreativeCommand(Guid ClientId, string Name, CreativeType Type, string? Description, string? TextContent,
    Stream? Content, string? FileName, string? ContentType, long Length);

public record UpdateCreativeRequest(string Name, string? Description, string? TextContent);

public record CreativeFile(Stream Content, string ContentType, string FileName);

public class CreativeQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public CreativeType? Type { get; set; }
}

/// <summary>
/// Regras de upload: extensões permitidas por tipo, tamanho máximo e verificação da assinatura binária
/// (magic bytes), para que um executável renomeado para .png seja rejeitado.
/// </summary>
public static class CreativeUploadPolicy
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly Dictionary<string, string> ImageTypes = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif", [".webp"] = "image/webp"
    };
    private static readonly Dictionary<string, string> VideoTypes = new()
    {
        [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".mov"] = "video/quicktime"
    };

    public static IReadOnlyDictionary<string, string> AllowedFor(CreativeType type) => type switch
    {
        CreativeType.Video => VideoTypes,
        CreativeType.Image or CreativeType.Banner or CreativeType.Carousel => ImageTypes,
        _ => new Dictionary<string, string>()
    };

    public static long MaxBytesFor(CreativeType type) => type == CreativeType.Video ? MaxVideoBytes : MaxImageBytes;

    /// <summary>Valida e retorna (extensão, content-type normalizado). Lança RequestValidationException.</summary>
    public static (string Extension, string ContentType) Validate(CreativeType type, string? fileName, long length, ReadOnlySpan<byte> header)
    {
        var allowed = AllowedFor(type);
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (allowed.Count == 0) Fail("Criativos do tipo Texto não aceitam arquivo.");
        if (!allowed.TryGetValue(ext, out var contentType))
            Fail($"Extensão não permitida para {type}. Aceitas: {string.Join(", ", allowed.Keys)}.");
        if (length <= 0) Fail("Arquivo vazio.");
        if (length > MaxBytesFor(type)) Fail($"Arquivo excede o limite de {MaxBytesFor(type) / 1024 / 1024} MB.");
        if (!SignatureMatches(ext, header)) Fail("O conteúdo do arquivo não corresponde à extensão informada.");
        return (ext, contentType!);
    }

    public static bool SignatureMatches(string ext, ReadOnlySpan<byte> h) => ext switch
    {
        ".jpg" or ".jpeg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".gif" => h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8)),
        ".webp" => h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8),
        ".mp4" or ".mov" => h.Length >= 8 && h[4..8].SequenceEqual("ftyp"u8),
        ".webm" => h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3,
        _ => false
    };

    private static void Fail(string message) =>
        throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = [message] });
}

public class CreativeService(IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, IFileStorage storage)
{
    private static readonly Dictionary<string, Expression<Func<CreativeAsset, object?>>> Sort = new()
    {
        ["name"] = c => c.Name, ["type"] = c => c.Type, ["client"] = c => c.Client!.Name,
        ["fileSize"] = c => c.FileSize, ["createdAt"] = c => c.CreatedAt
    };

    public async Task<PagedResult<CreativeDto>> ListAsync(CreativeQuery q, CancellationToken ct)
    {
        var query = scope.Creatives(db.CreativeAssets.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || (c.Description != null && c.Description.ToLower().Contains(s)));
        }
        if (q.ClientId is { } clientId) query = query.Where(c => c.ClientId == clientId);
        if (q.Type is { } type) query = query.Where(c => c.Type == type);
        return await Project(query.ApplySort(q, Sort, "createdAt", defaultDescending: true)).ToPagedAsync(q, ct);
    }

    public async Task<CreativeDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(scope.Creatives(db.CreativeAssets.AsNoTracking()).Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Criativo", id);

    public async Task<CreativeDto> CreateAsync(UploadCreativeCommand cmd, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 200) errors["name"] = ["Informe um nome com até 200 caracteres."];
        if (!Enum.IsDefined(cmd.Type)) errors["type"] = ["Tipo inválido."];
        if (cmd.Description?.Length > 1000) errors["description"] = ["Máximo de 1000 caracteres."];
        if (cmd.Type == CreativeType.Text && string.IsNullOrWhiteSpace(cmd.TextContent)) errors["textContent"] = ["Informe o texto do criativo."];
        if (cmd.TextContent?.Length > 5000) errors["textContent"] = ["Máximo de 5000 caracteres."];
        if (cmd.Type != CreativeType.Text && cmd.Content is null) errors["file"] = ["Selecione um arquivo."];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        scope.EnsureClient(cmd.ClientId);
        if (!await db.Clients.AnyAsync(c => c.Id == cmd.ClientId, ct)) throw new NotFoundException("Cliente", cmd.ClientId);

        var creative = new CreativeAsset
        {
            ClientId = cmd.ClientId, Name = cmd.Name.Trim(), Type = cmd.Type, Description = Clean(cmd.Description),
            TextContent = Clean(cmd.TextContent), CreatedAt = clock.UtcNow
        };

        if (cmd.Type != CreativeType.Text && cmd.Content is not null)
        {
            // Lê o cabeçalho para checar a assinatura e regrava o fluxo completo a partir de um buffer.
            await using var buffer = new MemoryStream();
            await cmd.Content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            var (ext, contentType) = CreativeUploadPolicy.Validate(cmd.Type, cmd.FileName, bytes.LongLength, bytes.AsSpan(0, Math.Min(16, bytes.Length)));
            buffer.Position = 0;
            creative.FilePath = await storage.SaveAsync(buffer, ext, ct);
            creative.ContentType = contentType;
            creative.FileSize = bytes.LongLength;
            creative.OriginalFileName = Path.GetFileName(cmd.FileName)?[..Math.Min(Path.GetFileName(cmd.FileName)!.Length, 255)];
        }

        db.CreativeAssets.Add(creative);
        audit.Log("Create", "CreativeAsset", creative.Id, $"{creative.Name} ({creative.Type})");
        await db.SaveChangesAsync(ct);
        return await GetAsync(creative.Id, ct);
    }

    public async Task<CreativeDto> UpdateAsync(Guid id, UpdateCreativeRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["name"] = ["Informe um nome com até 200 caracteres."] });
        var creative = await db.CreativeAssets.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Criativo", id);
        creative.Name = r.Name.Trim();
        creative.Description = Clean(r.Description);
        if (creative.Type == CreativeType.Text) creative.TextContent = Clean(r.TextContent);
        audit.Log("Update", "CreativeAsset", id, creative.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var creative = await db.CreativeAssets.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Criativo", id);
        db.CreativeAssets.Remove(creative);
        audit.Log("Delete", "CreativeAsset", id, creative.Name);
        await db.SaveChangesAsync(ct);
        if (creative.FilePath is not null) storage.Delete(creative.FilePath);
    }

    /// <summary>Arquivo do criativo, liberado apenas para quem pode ver o cliente dono.</summary>
    public async Task<CreativeFile> OpenFileAsync(Guid id, CancellationToken ct)
    {
        var creative = await scope.Creatives(db.CreativeAssets.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Criativo", id);
        if (creative.FilePath is null) throw new NotFoundException("Arquivo", id);
        var stream = storage.OpenRead(creative.FilePath) ?? throw new NotFoundException("Arquivo", id);
        return new CreativeFile(stream, creative.ContentType ?? "application/octet-stream", creative.OriginalFileName ?? $"{creative.Id}");
    }

    private static IQueryable<CreativeDto> Project(IQueryable<CreativeAsset> q) =>
        q.Select(c => new CreativeDto(c.Id, c.ClientId, c.Client!.Name, c.Name, c.Type, c.OriginalFileName, c.ContentType, c.FileSize,
            c.FilePath != null, c.Description, c.TextContent, c.Advertisements.Count, c.CreatedAt));

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
