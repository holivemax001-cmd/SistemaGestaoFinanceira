using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize]
public class ReceitasController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReceitasController(ApplicationDbContext context)
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

        if (usuario == null || !usuario.ClienteId.HasValue)
        return null;

        var clienteId = usuario.ClienteId.Value;

        var clienteAtivo = await _context.Clientes
            .AnyAsync(c => c.Id == clienteId && c.Ativo);

        return clienteAtivo ? clienteId : null;
    }

    private async Task<IActionResult?> NegarSeSemClienteAsync()
    {
        if (User.IsInRole("Admin"))
            return null;

        var clienteId = await ObterClienteIdUsuarioAsync();

        return clienteId.HasValue ? null : Forbid();
    }

    private async Task CarregarClientesAtivosAsync()
    {
        ViewBag.Clientes = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    private async Task CarregarDadosFormularioAsync(int? clienteId)
    {
        var contasQuery = _context.ContasFinanceiras
            .AsNoTracking()
            .Where(c => c.Ativa)
            .AsQueryable();

        var categoriasQuery = _context.CategoriasFinanceiras
            .AsNoTracking()
            .Where(c => c.Ativa && c.Tipo == "Receita")
            .AsQueryable();

        if (User.IsInRole("Admin"))
        {
            await CarregarClientesAtivosAsync();

            if (clienteId.HasValue)
            {
                contasQuery = contasQuery
                    .Where(c => c.ClienteId == clienteId.Value);

                categoriasQuery = categoriasQuery
                    .Where(c => c.ClienteId == clienteId.Value);
            }
        }
        else
        {
            if (!clienteId.HasValue)
            {
                ViewBag.Contas = new List<ContaFinanceira>();
                ViewBag.Categorias = new List<CategoriaFinanceira>();
                return;
            }

            contasQuery = contasQuery
                .Where(c => c.ClienteId == clienteId.Value);

            categoriasQuery = categoriasQuery
                .Where(c => c.ClienteId == clienteId.Value);
        }

        ViewBag.Contas = await contasQuery
            .OrderBy(c => c.Nome)
            .ToListAsync();

        ViewBag.Categorias = await categoriasQuery
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    private async Task<bool> ContaECategoriaValidasAsync(
        int clienteId,
        int contaId,
        int categoriaId)
    {
        var contaValida = await _context.ContasFinanceiras
            .AnyAsync(c =>
                c.Id == contaId &&
                c.ClienteId == clienteId &&
                c.Ativa);

        var categoriaValida = await _context.CategoriasFinanceiras
            .AnyAsync(c =>
                c.Id == categoriaId &&
                c.ClienteId == clienteId &&
                c.Ativa &&
                c.Tipo == "Receita");

        return contaValida && categoriaValida;
    }

    public async Task<IActionResult> Index()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var receitasQuery = _context.Receitas
            .AsNoTracking()
            .Include(r => r.ContaFinanceira)
            .Include(r => r.CategoriaFinanceira)
            .Include(r => r.Cliente)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();

            receitasQuery = receitasQuery
                .Where(r => r.ClienteId == clienteId.Value);
        }

        var receitas = await receitasQuery
            .OrderByDescending(r => r.Data)
            .ToListAsync();

        return View(receitas);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        int? clienteId = null;

        if (!User.IsInRole("Admin"))
        {
            clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();
        }

        await CarregarDadosFormularioAsync(clienteId);

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Receita receita)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        int? clienteId;

        if (User.IsInRole("Admin"))
        {
            clienteId = receita.ClienteId;

            if (!clienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == clienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(receita.ClienteId),
                    "Selecione um cliente ativo válido.");
            }
        }
        else
        {
            clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();

            receita.ClienteId = clienteId;
        }

        if (clienteId.HasValue &&
            !await ContaECategoriaValidasAsync(
                clienteId.Value,
                receita.ContaFinanceiraId,
                receita.CategoriaFinanceiraId))
        {
            ModelState.AddModelError(
                string.Empty,
                "A conta e a categoria devem estar ativas e pertencer ao cliente selecionado. A categoria deve ser do tipo Receita.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarDadosFormularioAsync(clienteId);
            return View(receita);
        }

        receita.ClienteId = clienteId;
        receita.DataCadastro = DateTime.Now;

        _context.Receitas.Add(receita);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Receita criada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        int? clienteIdUsuario = null;

        if (!User.IsInRole("Admin"))
        {
            clienteIdUsuario = await ObterClienteIdUsuarioAsync();

            if (!clienteIdUsuario.HasValue)
                return Forbid();
        }

        var receitaQuery = _context.Receitas
            .AsQueryable();

        if (clienteIdUsuario.HasValue)
        {
            receitaQuery = receitaQuery
                .Where(r => r.ClienteId == clienteIdUsuario.Value);
        }

        var receita = await receitaQuery
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receita == null)
            return NotFound();

        if (!receita.ClienteId.HasValue ||
            !await _context.Clientes.AnyAsync(c =>
                c.Id == receita.ClienteId.Value && c.Ativo))
        {
            return BadRequest(
                "A receita não possui um cliente ativo válido. Solicite ao administrador a regularização do cadastro.");
        }

        await CarregarDadosFormularioAsync(receita.ClienteId);

        return View(receita);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Receita receita)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (id != receita.Id)
            return NotFound();

        int? clienteIdUsuario = null;

        if (!User.IsInRole("Admin"))
        {
            clienteIdUsuario = await ObterClienteIdUsuarioAsync();

            if (!clienteIdUsuario.HasValue)
                return Forbid();
        }

        var receitaQuery = _context.Receitas
            .AsQueryable();

        if (clienteIdUsuario.HasValue)
        {
            receitaQuery = receitaQuery
                .Where(r => r.ClienteId == clienteIdUsuario.Value);
        }

        var receitaExistente = await receitaQuery
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receitaExistente == null)
            return NotFound();

        // Preserva o cliente original, sem confiar no formulário.
        var clienteId = receitaExistente.ClienteId;

        if (!clienteId.HasValue ||
            !await _context.Clientes.AnyAsync(c =>
                c.Id == clienteId.Value && c.Ativo))
        {
            return BadRequest(
                "A receita não possui um cliente ativo válido. Solicite ao administrador a regularização do cadastro.");
        }

        receita.ClienteId = clienteId;

        if (!await ContaECategoriaValidasAsync(
                clienteId.Value,
                receita.ContaFinanceiraId,
                receita.CategoriaFinanceiraId))
        {
            ModelState.AddModelError(
                string.Empty,
                "A conta e a categoria devem estar ativas e pertencer ao cliente desta receita. A categoria deve ser do tipo Receita.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarDadosFormularioAsync(clienteId);
            return View(receita);
        }

        receitaExistente.Descricao = receita.Descricao;
        receitaExistente.Valor = receita.Valor;
        receitaExistente.Data = receita.Data;
        receitaExistente.ContaFinanceiraId = receita.ContaFinanceiraId;
        receitaExistente.CategoriaFinanceiraId = receita.CategoriaFinanceiraId;
        receitaExistente.Observacao = receita.Observacao;
        receitaExistente.Recebida = receita.Recebida;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Receita atualizada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var receitaQuery = _context.Receitas
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();

            receitaQuery = receitaQuery
                .Where(r => r.ClienteId == clienteId.Value);
        }

        var receita = await receitaQuery
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receita == null)
            return NotFound();

        _context.Receitas.Remove(receita);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Receita excluída com sucesso.";

        return RedirectToAction(nameof(Index));
    }
}