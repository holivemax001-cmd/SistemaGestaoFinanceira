using System.ComponentModel.DataAnnotations;

namespace SistemaGestaoFinanceira.Models;

public class CategoriaFinanceira
{
    public int Id { get; set; }
    
    public int? ClienteId { get; set; }
    
    public Cliente? Cliente { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descricao { get; set; }

    public bool Ativa { get; set; } = true;

    public DateTime DataCadastro { get; set; } = DateTime.Now;
}