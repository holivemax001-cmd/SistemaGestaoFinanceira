using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize]
public class CategoriasFinanceirasController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriasFinanceirasController(ApplicationDbContext context)
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

    private async Task<IActionResult> NegarSeSemClienteAsync()
    {
        if (User.IsInRole("Admin"))
            return null!;

        var clienteId = await ObterClienteIdUsuarioAsync();

        return clienteId.HasValue ? null! : Forbid();
    }

    private async Task CarregarClientesAtivosAsync()
    {
        ViewBag.Clientes = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id,
                c.Nome
            })
            .ToListAsync();
    }

    public async Task<IActionResult> Index()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var categoriasQuery = _context.CategoriasFinanceiras
            .AsNoTracking()
            .Include(c => c.Cliente)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue)
                return Forbid();

            categoriasQuery = categoriasQuery
                .Where(c => c.ClienteId == clienteId.Value);
        }

        var categorias = await categoriasQuery
            .OrderBy(c => c.Tipo)
            .ThenBy(c => c.Nome)
            .ToListAsync();

        return View(categorias);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (User.IsInRole("Admin"))
            await CarregarClientesAtivosAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoriaFinanceira categoria)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (User.IsInRole("Admin"))
        {
            if (!categoria.ClienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == categoria.ClienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(categoria.ClienteId),
                    "Selecione um cliente ativo válido.");
            }
        }
        else
        {
            categoria.ClienteId = await ObterClienteIdUsuarioAsync();

            if (!categoria.ClienteId.HasValue)
                return Forbid();
        }

        if (!ModelState.IsValid)
        {
            if (User.IsInRole("Admin"))
                await CarregarClientesAtivosAsync();

            return View(categoria);
        }

        categoria.DataCadastro = DateTime.Now;
        categoria.Ativa = true;

        _context.CategoriasFinanceiras.Add(categoria);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Categoria criada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var categoria = await _context.CategoriasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue ||
                categoria.ClienteId != clienteId.Value)
                return Forbid();
        }
        else
        {
            await CarregarClientesAtivosAsync();
        }

        return View(categoria);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CategoriaFinanceira categoria)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (id != categoria.Id)
            return NotFound();

        var categoriaExistente = await _context.CategoriasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoriaExistente == null)
            return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue ||
                categoriaExistente.ClienteId != clienteId.Value)
                return Forbid();
        }
        else
        {
            if (!categoria.ClienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == categoria.ClienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(categoria.ClienteId),
                    "Selecione um cliente ativo válido.");
            }

            if (categoria.ClienteId.HasValue &&
                categoria.ClienteId != categoriaExistente.ClienteId)
            {
                var possuiReceitas = await _context.Receitas
                    .AnyAsync(r => r.CategoriaFinanceiraId == id);

                var possuiDespesas = await _context.Despesas
                    .AnyAsync(d => d.CategoriaFinanceiraId == id);

                if (possuiReceitas || possuiDespesas)
                {
                    ModelState.AddModelError(
                        nameof(categoria.ClienteId),
                        "Não é possível transferir esta categoria porque existem lançamentos financeiros vinculados.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            if (User.IsInRole("Admin"))
                await CarregarClientesAtivosAsync();

            return View(categoria);
        }

        if (User.IsInRole("Admin"))
            categoriaExistente.ClienteId = categoria.ClienteId;

        categoriaExistente.Nome = categoria.Nome;
        categoriaExistente.Tipo = categoria.Tipo;
        categoriaExistente.Descricao = categoria.Descricao;
        categoriaExistente.Ativa = categoria.Ativa;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Categoria atualizada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var categoria = await _context.CategoriasFinanceiras
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            if (!clienteId.HasValue ||
                categoria.ClienteId != clienteId.Value)
                return Forbid();
        }

        var possuiReceitas = await _context.Receitas
            .AnyAsync(r => r.CategoriaFinanceiraId == id);

        var possuiDespesas = await _context.Despesas
            .AnyAsync(d => d.CategoriaFinanceiraId == id);

        if (possuiReceitas || possuiDespesas)
        {
            TempData["Erro"] =
                "Esta categoria não pode ser excluída porque possui lançamentos financeiros vinculados.";

            return RedirectToAction(nameof(Index));
        }

        _context.CategoriasFinanceiras.Remove(categoria);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Categoria excluída com sucesso.";

        return RedirectToAction(nameof(Index));
    }
}