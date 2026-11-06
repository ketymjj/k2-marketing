using K2Marketing.Application.Common;
using Microsoft.Extensions.Options;

namespace K2Marketing.Infrastructure.Storage;

public class StorageOptions
{
    public const string Section = "Storage";
    public string RootPath { get; set; } = "storage";
}

/// <summary>
/// Grava arquivos com nome aleatório (GUID) em subpastas por mês. O nome original nunca é usado no caminho,
/// evitando path traversal e colisões.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        var folder = DateTime.UtcNow.ToString("yyyy-MM");
        Directory.CreateDirectory(Path.Combine(_root, folder));
        var relative = $"{folder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var file = File.Create(Resolve(relative));
        await content.CopyToAsync(file, ct);
        return relative;
    }

    public Stream? OpenRead(string relativePath)
    {
        var full = Resolve(relativePath);
        return File.Exists(full) ? File.OpenRead(full) : null;
    }

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full)) File.Delete(full);
    }

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de arquivo inválido.");
        return full;
    }
}
