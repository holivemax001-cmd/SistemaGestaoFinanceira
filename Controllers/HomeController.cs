
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string filtroTipo = "Todas",
        string filtroPeriodo = "Todos")
    {
        var ehAdmin = User.IsInRole("Admin");

        int? clienteId = null;

        if (!ehAdmin)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(usuarioId))
                return Forbid();

            var usuario = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => new { u.ClienteId })
                .SingleOrDefaultAsync();

            if (usuario?.ClienteId == null)
                return Forbid();

            clienteId = usuario.ClienteId;

            var clienteAtivo = await _context.Clientes
                .AnyAsync(c => c.Id == clienteId.Value && c.Ativo);

            if (!clienteAtivo)
                return Forbid();
        }

        var contasQuery = _context.ContasFinanceiras.AsNoTracking();
        var receitasQuery = _context.Receitas.AsNoTracking();
        var despesasQuery = _context.Despesas.AsNoTracking();

        if (!ehAdmin)
        {
            contasQuery = contasQuery.Where(c => c.ClienteId == clienteId);
            receitasQuery = receitasQuery.Where(r => r.ClienteId == clienteId);
            despesasQuery = despesasQuery.Where(d => d.ClienteId == clienteId);
        }

        var saldoInicial = await contasQuery
            .Where(c => c.Ativa)
            .SumAsync(c => (decimal?)c.SaldoInicial) ?? 0;

        var totalReceitas = await receitasQuery
            .Where(r => r.Recebida)
            .SumAsync(r => (decimal?)r.Valor) ?? 0;

        var totalDespesas = await despesasQuery
            .Where(d => d.Paga)
            .SumAsync(d => (decimal?)d.Valor) ?? 0;

        var totalContas = await contasQuery
            .Where(c => c.Ativa)
            .CountAsync();

        var receitas = await receitasQuery
            .Include(r => r.ContaFinanceira)
            .Include(r => r.CategoriaFinanceira)
            .OrderByDescending(r => r.Data)
            .Select(r => new TransacaoRecenteViewModel
            {
                Descricao = r.Descricao,
                Valor = r.Valor,
                Data = r.Data,
                Tipo = "Receita",
                Categoria = r.CategoriaFinanceira != null
                    ? r.CategoriaFinanceira.Nome
                    : "Sem categoria",
                Conta = r.ContaFinanceira != null
                    ? r.ContaFinanceira.Nome
                    : "Sem conta",
                Status = r.Recebida
            })
            .ToListAsync();

        var despesas = await despesasQuery
            .Include(d => d.ContaFinanceira)
            .Include(d => d.CategoriaFinanceira)
            .OrderByDescending(d => d.Data)
            .Select(d => new TransacaoRecenteViewModel
            {
                Descricao = d.Descricao,
                Valor = d.Valor,
                Data = d.Data,
                Tipo = "Despesa",
                Categoria = d.CategoriaFinanceira != null
                    ? d.CategoriaFinanceira.Nome
                    : "Sem categoria",
                Conta = d.ContaFinanceira != null
                    ? d.ContaFinanceira.Nome
                    : "Sem conta",
                Status = d.Paga
            })
            .ToListAsync();

        IEnumerable<TransacaoRecenteViewModel> transacoes =
            receitas.Concat(despesas);

        if (filtroTipo == "Receitas")
        {
            transacoes = transacoes.Where(t => t.Tipo == "Receita");
        }
        else if (filtroTipo == "Despesas")
        {
            transacoes = transacoes.Where(t => t.Tipo == "Despesa");
        }

        var hoje = DateTime.Today;

        if (filtroPeriodo == "7")
        {
            var dataInicial = hoje.AddDays(-7);
            transacoes = transacoes.Where(t => t.Data.Date >= dataInicial);
        }
        else if (filtroPeriodo == "30")
        {
            var dataInicial = hoje.AddDays(-30);
            transacoes = transacoes.Where(t => t.Data.Date >= dataInicial);
        }

        var transacoesRecentes = transacoes
            .OrderByDescending(t => t.Data)
            .ThenByDescending(t => t.Tipo)
            .Take(5)
            .ToList();

        var inicioPeriodo = new DateTime(
            hoje.Year,
            hoje.Month,
            1).AddMonths(-5);

        var receitasGrafico = await receitasQuery
            .Where(r => r.Data >= inicioPeriodo && r.Recebida)
            .GroupBy(r => new
            {
                r.Data.Year,
                r.Data.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(x => x.Valor)
            })
            .ToListAsync();

        var despesasGrafico = await despesasQuery
            .Where(d => d.Data >= inicioPeriodo && d.Paga)
            .GroupBy(d => new
            {
                d.Data.Year,
                d.Data.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(x => x.Valor)
            })
            .ToListAsync();

        var resumoMensal = new List<ResumoMensalViewModel>();

        for (int i = 0; i < 6; i++)
        {
            var mes = inicioPeriodo.AddMonths(i);

            var receitaMes = receitasGrafico
                .Where(x => x.Year == mes.Year && x.Month == mes.Month)
                .Select(x => x.Total)
                .FirstOrDefault();

            var despesaMes = despesasGrafico
                .Where(x => x.Year == mes.Year && x.Month == mes.Month)
                .Select(x => x.Total)
                .FirstOrDefault();

            resumoMensal.Add(new ResumoMensalViewModel
            {
                Mes = mes.ToString("MMM"),
                Receitas = receitaMes,
                Despesas = despesaMes
            });
        }

        var dashboard = new DashboardViewModel
        {
            SaldoTotal = saldoInicial + totalReceitas - totalDespesas,
            TotalReceitas = totalReceitas,
            TotalDespesas = totalDespesas,
            TotalContas = totalContas,
            TransacoesRecentes = transacoesRecentes,
            ResumoMensal = resumoMensal,
            FiltroTipo = filtroTipo,
            FiltroPeriodo = filtroPeriodo
        };

        return View(dashboard);
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
