using FluentValidation;
using K2Marketing.Domain;

namespace K2Marketing.Application.Common;

public class NotFoundException(string entity, object key)
    : DomainException("notFound", $"{entity} '{key}' não encontrado.")
{
    public string Entity { get; } = entity;
}

public class ForbiddenException(string message = "Acesso não autorizado a este recurso.")
    : DomainException("forbidden", message);

public class UnauthorizedException(string code, string message) : DomainException(code, message);

public class ConflictException(string code, string message) : DomainException(code, message);

public class RequestValidationException(IDictionary<string, string[]> errors)
    : DomainException("validation", "Um ou mais campos são inválidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public static class ValidationExtensions
{
    /// <summary>Executa o validador e lança <see cref="RequestValidationException"/> (HTTP 400) se houver erros.</summary>
    public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid) return;
        throw new RequestValidationException(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
    }
}
