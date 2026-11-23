using System.ComponentModel.DataAnnotations;

namespace SistemaGestaoFinanceira.Models;

public class ContaFinanceira
{
    public int Id { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Tipo { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Instituicao { get; set; }

    [DataType(DataType.Currency)]
    public decimal SaldoInicial { get; set; }

    public bool Ativa { get; set; } = true;

    [StringLength(500)]
    public string? Observacao { get; set; }

    public DateTime DataCadastro { get; set; } = DateTime.Now;
}