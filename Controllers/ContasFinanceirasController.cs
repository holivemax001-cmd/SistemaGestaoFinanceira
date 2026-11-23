using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize]
public class ContasFinanceirasController : Controller
{
    private readonly ApplicationDbContext _context;

    public ContasFinanceirasController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<int?> ObterClienteIdUsuarioAsync()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(usuarioId))
            return null;

        var usuario = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.ClienteId })
            .SingleOrDefaultAsync();

        if (usuario?.ClienteId == null)
            return null;

        var clienteAtivo = await _context.Clientes
            .AnyAsync(c => c.Id == usuario.ClienteId.Value && c.Ativo);

        return clienteAtivo ? usuario.ClienteId : null;
    }

    private async Task<bool> UsuarioPodeAcessarAsync(int? clienteId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var clienteUsuario = await ObterClienteIdUsuarioAsync();

        return clienteUsuario.HasValue &&
               clienteId.HasValue &&
               clienteUsuario.Value == clienteId.Value;
    }

    private async Task<IActionResult> NegarSeSemClienteAsync()
    {
        if (User.IsInRole("Admin"))
            return null!;

        var clienteId = await ObterClienteIdUsuarioAsync();

        if (clienteId.HasValue)
            return null!;

        return Forbid();
    }

    private async Task CarregarClientesAtivosAsync()
    {
        if (User.IsInRole("Admin"))
        {
            ViewBag.Clientes = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Ativo)
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }
    }

    // Lista somente as contas autorizadas e calcula os saldos.
    public async Task<IActionResult> Index()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var contasQuery = _context.ContasFinanceiras
           .AsNoTracking()
           .Include(c => c.Cliente)
           .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();

            contasQuery = contasQuery
                .Where(c => c.ClienteId == clienteId.Value);
        }

        var contas = await contasQuery
            .OrderBy(c => c.Nome)
            .ToListAsync();

        foreach (var conta in contas)
        {
            var totalReceitas = await _context.Receitas
                .Where(r =>
                    r.ClienteId == conta.ClienteId &&
                    r.ContaFinanceiraId == conta.Id &&
                    r.Recebida)
                .SumAsync(r => (decimal?)r.Valor) ?? 0;

            var totalDespesas = await _context.Despesas
                .Where(d =>
                    d.ClienteId == conta.ClienteId &&
                    d.ContaFinanceiraId == conta.Id &&
                    d.Paga)
                .SumAsync(d => (decimal?)d.Valor) ?? 0;

            ViewData[$"Receitas_{conta.Id}"] = totalReceitas;
            ViewData[$"Despesas_{conta.Id}"] = totalDespesas;
            ViewData[$"SaldoAtual_{conta.Id}"] =
                conta.SaldoInicial + totalReceitas - totalDespesas;
        }

        return View(contas);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        await CarregarClientesAtivosAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContaFinanceira conta)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (User.IsInRole("Admin"))
        {
            if (!conta.ClienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == conta.ClienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(conta.ClienteId),
                    "Selecione um cliente ativo válido.");
            }
        }
        else
        {
            // O cliente é definido pelo servidor, nunca pelo formulário.
            conta.ClienteId = await ObterClienteIdUsuarioAsync();

            if (!conta.ClienteId.HasValue)
                return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await CarregarClientesAtivosAsync();
            return View(conta);
        }

        conta.DataCadastro = DateTime.Now;
        conta.Ativa = true;

        _context.ContasFinanceiras.Add(conta);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Conta criada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var conta = await _context.ContasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conta == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(conta.ClienteId))
            return Forbid();

        await CarregarClientesAtivosAsync();

        return View(conta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContaFinanceira conta)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (id != conta.Id)
            return NotFound();

        var contaExistente = await _context.ContasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contaExistente == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(contaExistente.ClienteId))
            return Forbid();

        if (User.IsInRole("Admin"))
        {
            if (!conta.ClienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == conta.ClienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(conta.ClienteId),
                    "Selecione um cliente ativo válido.");
            }

            // Não permite transferir uma conta que tenha lançamentos.
            if (conta.ClienteId.HasValue &&
                conta.ClienteId.Value != contaExistente.ClienteId)
            {
                var possuiReceitas = await _context.Receitas
                    .AnyAsync(r => r.ContaFinanceiraId == id);

                var possuiDespesas = await _context.Despesas
                    .AnyAsync(d => d.ContaFinanceiraId == id);

                if (possuiReceitas || possuiDespesas)
                {
                    ModelState.AddModelError(
                        nameof(conta.ClienteId),
                        "Não é possível transferir esta conta porque existem lançamentos financeiros vinculados.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await CarregarClientesAtivosAsync();
            return View(conta);
        }

        // Somente o Admin pode alterar o cliente de uma conta.
        if (User.IsInRole("Admin"))
            contaExistente.ClienteId = conta.ClienteId;

        contaExistente.Nome = conta.Nome;
        contaExistente.Tipo = conta.Tipo;
        contaExistente.Instituicao = conta.Instituicao;
        contaExistente.SaldoInicial = conta.SaldoInicial;
        contaExistente.Ativa = conta.Ativa;
        contaExistente.Observacao = conta.Observacao;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Conta atualizada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var conta = await _context.ContasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conta == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(conta.ClienteId))
            return Forbid();

        var possuiReceitas = await _context.Receitas
            .AnyAsync(r => r.ContaFinanceiraId == id);

        var possuiDespesas = await _context.Despesas
            .AnyAsync(d => d.ContaFinanceiraId == id);

        if (possuiReceitas || possuiDespesas)
        {
            TempData["Erro"] =
                "Esta conta não pode ser excluída porque possui lançamentos financeiros vinculados.";

            return RedirectToAction(nameof(Index));
        }

        _context.ContasFinanceiras.Remove(conta);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Conta excluída com sucesso.";

        return RedirectToAction(nameof(Index));
    }
}