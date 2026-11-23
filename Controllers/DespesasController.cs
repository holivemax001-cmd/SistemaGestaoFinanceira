using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize]
public class DespesasController : Controller
{
    private readonly ApplicationDbContext _context;

    public DespesasController(ApplicationDbContext context)
    {
        _context = context;
    }

    private bool UsuarioEhAdmin()
    {
        return User.IsInRole("Admin");
    }

    private async Task<int?> ObterClienteIdUsuarioAsync()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(usuarioId))
            return null;

        var usuario = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new
            {
                u.ClienteId,
                ClienteAtivo = u.Cliente != null && u.Cliente.Ativo
            })
            .FirstOrDefaultAsync();

        if (usuario == null ||
            !usuario.ClienteId.HasValue ||
            !usuario.ClienteAtivo)
        {
            return null;
        }

        return usuario.ClienteId;
    }

    private async Task<IActionResult?> NegarSeSemClienteAsync()
    {
        if (UsuarioEhAdmin())
            return null;

        var clienteId = await ObterClienteIdUsuarioAsync();

        if (!clienteId.HasValue)
            return Forbid();

        return null;
    }

    private async Task<bool> UsuarioPodeAcessarAsync(Despesa despesa)
    {
        if (UsuarioEhAdmin())
            return true;

        var clienteId = await ObterClienteIdUsuarioAsync();

        return clienteId.HasValue &&
               despesa.ClienteId.HasValue &&
               despesa.ClienteId.Value == clienteId.Value;
    }

    private async Task CarregarClientesAtivosAsync()
    {
        if (UsuarioEhAdmin())
        {
            ViewBag.Clientes = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Ativo)
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }
    }

    private async Task CarregarDadosFormularioAsync(int? clienteId = null)
    {
        var contasQuery = _context.ContasFinanceiras
            .AsNoTracking()
            .Where(c => c.Ativa);

        var categoriasQuery = _context.CategoriasFinanceiras
            .AsNoTracking()
            .Where(c => c.Ativa && c.Tipo == "Despesa");

        if (!UsuarioEhAdmin())
        {
            var idUsuario = await ObterClienteIdUsuarioAsync();

            if (idUsuario.HasValue)
            {
                contasQuery = contasQuery.Where(c => c.ClienteId == idUsuario.Value);
                categoriasQuery = categoriasQuery.Where(c => c.ClienteId == idUsuario.Value);
            }
        }
        else if (clienteId.HasValue)
        {
            contasQuery = contasQuery.Where(c => c.ClienteId == clienteId.Value);
            categoriasQuery = categoriasQuery.Where(c => c.ClienteId == clienteId.Value);
        }

        ViewBag.Contas = await contasQuery
            .OrderBy(c => c.Nome)
            .ToListAsync();

        ViewBag.Categorias = await categoriasQuery
            .OrderBy(c => c.Nome)
            .ToListAsync();

        await CarregarClientesAtivosAsync();
    }

    private async Task<bool> ContaECategoriaValidasAsync(
        int contaId,
        int categoriaId,
        int clienteId)
    {
        var contaValida = await _context.ContasFinanceiras
            .AnyAsync(c =>
                c.Id == contaId &&
                c.ClienteId == clienteId &&
                c.Ativa);

        if (!contaValida)
            return false;

        return await _context.CategoriasFinanceiras
            .AnyAsync(c =>
                c.Id == categoriaId &&
                c.ClienteId == clienteId &&
                c.Ativa &&
                c.Tipo == "Despesa");
    }

    public async Task<IActionResult> Index()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var despesasQuery = _context.Despesas
           .AsNoTracking()
           .Include(d => d.Cliente)
           .Include(d => d.ContaFinanceira)
           .Include(d => d.CategoriaFinanceira)
           .AsQueryable();

        if (!UsuarioEhAdmin())
        {
            var clienteId = await ObterClienteIdUsuarioAsync();

            despesasQuery = despesasQuery
                .Where(d => d.ClienteId == clienteId);
        }

        var despesas = await despesasQuery
            .OrderByDescending(d => d.Data)
            .ToListAsync();

        return View(despesas);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        await CarregarDadosFormularioAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Despesa despesa)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        int clienteId;

        if (UsuarioEhAdmin())
        {
            if (!despesa.ClienteId.HasValue ||
                !await _context.Clientes.AnyAsync(c =>
                    c.Id == despesa.ClienteId.Value && c.Ativo))
            {
                ModelState.AddModelError(
                    nameof(despesa.ClienteId),
                    "Selecione um cliente ativo válido.");
                
                await CarregarDadosFormularioAsync();
                return View(despesa);
            }

            clienteId = despesa.ClienteId.Value;
        }
        else
        {
            var clienteUsuario = await ObterClienteIdUsuarioAsync();

            if (!clienteUsuario.HasValue)
                return Forbid();

            clienteId = clienteUsuario.Value;
            despesa.ClienteId = clienteId;
        }

        if (!await ContaECategoriaValidasAsync(
                despesa.ContaFinanceiraId,
                despesa.CategoriaFinanceiraId,
                clienteId))
        {
            ModelState.AddModelError(
                string.Empty,
                "A conta e a categoria devem estar ativas e pertencer ao mesmo cliente da despesa. A categoria deve ser do tipo Despesa.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarDadosFormularioAsync(
                UsuarioEhAdmin() ? despesa.ClienteId : null);

            return View(despesa);
        }

        despesa.DataCadastro = DateTime.Now;
        despesa.ClienteId = clienteId;

        _context.Despesas.Add(despesa);

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Despesa cadastrada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var despesa = await _context.Despesas
            .FirstOrDefaultAsync(d => d.Id == id);

        if (despesa == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(despesa))
            return Forbid();

        if (!despesa.ClienteId.HasValue ||
            !await _context.Clientes.AnyAsync(c =>
                c.Id == despesa.ClienteId.Value && c.Ativo))
        {
            return BadRequest(
                "A despesa não possui um cliente ativo válido. Solicite ao administrador a regularização do cadastro.");
        }

        await CarregarDadosFormularioAsync(despesa.ClienteId);

        return View(despesa);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Despesa despesa)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        if (id != despesa.Id)
            return NotFound();

        var existente = await _context.Despesas
            .FirstOrDefaultAsync(d => d.Id == id);

        if (existente == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(existente))
            return Forbid();

        if (!existente.ClienteId.HasValue)
            return BadRequest(
                "A despesa não possui cliente associado. Solicite ao administrador a regularização do cadastro.");

        var clienteId = existente.ClienteId.Value;

        if (!await _context.Clientes.AnyAsync(c =>
                c.Id == clienteId && c.Ativo))
        {
            return BadRequest("O cliente associado à despesa está inativo.");
        }

        if (!await ContaECategoriaValidasAsync(
                despesa.ContaFinanceiraId,
                despesa.CategoriaFinanceiraId,
                clienteId))
        {
            ModelState.AddModelError(
                string.Empty,
                "A conta e a categoria devem estar ativas e pertencer ao cliente da despesa. A categoria deve ser do tipo Despesa.");
        }

        if (!ModelState.IsValid)
        {
            despesa.ClienteId = clienteId;
            await CarregarDadosFormularioAsync(clienteId);
            return View(despesa);
        }

        existente.Descricao = despesa.Descricao;
        existente.Valor = despesa.Valor;
        existente.Data = despesa.Data;
        existente.ContaFinanceiraId = despesa.ContaFinanceiraId;
        existente.CategoriaFinanceiraId = despesa.CategoriaFinanceiraId;
        existente.Observacao = despesa.Observacao;
        existente.Paga = despesa.Paga;

        // O ClienteId original é preservado e nunca vem do formulário.
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Despesa atualizada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarComoPaga(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var despesa = await _context.Despesas
            .FirstOrDefaultAsync(d => d.Id == id);

        if (despesa == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(despesa))
            return Forbid();

        despesa.Paga = true;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Despesa marcada como paga.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bloqueio = await NegarSeSemClienteAsync();

        if (bloqueio != null)
            return bloqueio;

        var despesa = await _context.Despesas
            .FirstOrDefaultAsync(d => d.Id == id);

        if (despesa == null)
            return NotFound();

        if (!await UsuarioPodeAcessarAsync(despesa))
            return Forbid();

        _context.Despesas.Remove(despesa);

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Despesa excluída com sucesso.";

        return RedirectToAction(nameof(Index));
    }
}