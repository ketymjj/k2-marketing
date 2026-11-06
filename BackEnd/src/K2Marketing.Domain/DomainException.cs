namespace K2Marketing.Domain;

/// <summary>Violação de regra de negócio. O <see cref="Code"/> é estável e traduzido no frontend.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
