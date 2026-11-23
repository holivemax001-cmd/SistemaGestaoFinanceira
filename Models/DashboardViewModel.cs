namespace SistemaGestaoFinanceira.Models;

public class DashboardViewModel
{
    public decimal SaldoTotal { get; set; }

    public decimal TotalReceitas { get; set; }

    public decimal TotalDespesas { get; set; }

    public int TotalContas { get; set; }


    // =====================================================
    // TRANSAÇÕES RECENTES
    // =====================================================

    public List<TransacaoRecenteViewModel> TransacoesRecentes { get; set; } = new();


    // =====================================================
    // RESUMO MENSAL
    // =====================================================

    public List<ResumoMensalViewModel> ResumoMensal { get; set; } = new();


    // =====================================================
    // FILTROS
    // =====================================================

    public string FiltroTipo { get; set; } = "Todas";

    public string FiltroPeriodo { get; set; } = "Todos";
}


public class TransacaoRecenteViewModel
{
    public string Descricao { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public DateTime Data { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public string Conta { get; set; } = string.Empty;

    public bool Status { get; set; }
}


public class ResumoMensalViewModel
{
    public string Mes { get; set; } = string.Empty;

    public decimal Receitas { get; set; }

    public decimal Despesas { get; set; }
}