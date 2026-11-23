namespace SistemaGestaoFinanceira.Models;

public class Cliente
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? TipoPessoa { get; set; }

    public string? Documento { get; set; }

    public string? EmailContato { get; set; }

    public string? NomeResponsavel { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUser> Usuarios { get; set; }
        = new List<ApplicationUser>();
}
