using System.ComponentModel.DataAnnotations;

namespace SistemaGestaoFinanceira.Models;

public class Receita
{
    public int Id { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Required]
    [StringLength(150)]
    public string Descricao { get; set; } = string.Empty;

    [Required]
    public decimal Valor { get; set; }

    [Required]
    public DateTime Data { get; set; }

    public int ContaFinanceiraId { get; set; }

    public ContaFinanceira? ContaFinanceira { get; set; }

    public int CategoriaFinanceiraId { get; set; }

    public CategoriaFinanceira? CategoriaFinanceira { get; set; }

    [StringLength(500)]
    public string? Observacao { get; set; }

    public bool Recebida { get; set; } = false;

    public DateTime DataCadastro { get; set; } = DateTime.Now;
}