using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize(Roles = "Admin")]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClientesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var clientes = await _context.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .ToListAsync();

        return View(clientes);
    }

    [HttpGet]
    public IActionResult Criar()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            ModelState.AddModelError("nome", "Informe o nome do cliente.");
            return View();
        }

        nome = nome.Trim();

        if (nome.Length > 150)
        {
            ModelState.AddModelError(
                "nome",
                "O nome deve ter no máximo 150 caracteres.");

            return View();
        }

        var cliente = new Cliente
        {
            Nome = nome,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            $"Cliente {cliente.Nome} cadastrado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        string nome,
        bool ativo)
    {
        nome = nome?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nome))
        {
            ModelState.AddModelError("Nome", "Informe o nome do cliente.");
        }
        else if (nome.Length > 150)
        {
            ModelState.AddModelError(
                "Nome",
                "O nome deve ter no máximo 150 caracteres.");
        }

        if (!ModelState.IsValid)
        {
            var clienteParaExibir = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (clienteParaExibir == null)
            {
                return NotFound();
            }

            clienteParaExibir.Nome = nome;
            clienteParaExibir.Ativo = ativo;

            return View(clienteParaExibir);
        }

        // Preserva a data de cadastro original.
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
        {
            return NotFound();
        }

        cliente.Nome = nome;
        cliente.Ativo = ativo;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            $"Cliente {cliente.Nome} atualizado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
        {
            TempData["Erro"] = "Cliente não encontrado.";
            return RedirectToAction(nameof(Index));
        }

        // Verifica se existem usuários vinculados.
        var possuiUsuarios = await _context.Users
            .AnyAsync(u => u.ClienteId == id);

        // Verifica os registros financeiros vinculados.
        var possuiContas = await _context.ContasFinanceiras
            .AnyAsync(c => c.ClienteId == id);

        var possuiCategorias = await _context.CategoriasFinanceiras
            .AnyAsync(c => c.ClienteId == id);

        var possuiReceitas = await _context.Receitas
            .AnyAsync(r => r.ClienteId == id);

        var possuiDespesas = await _context.Despesas
            .AnyAsync(d => d.ClienteId == id);

        if (possuiUsuarios ||
            possuiContas ||
            possuiCategorias ||
            possuiReceitas ||
            possuiDespesas)
        {
            TempData["Erro"] =
                "Este cliente não pode ser excluído porque possui usuários " +
                "ou registros financeiros vinculados. Desative o cliente " +
                "em vez de excluí-lo.";

            return RedirectToAction(nameof(Index));
        }

        _context.Clientes.Remove(cliente);

        try
        {
            await _context.SaveChangesAsync();

            TempData["Sucesso"] =
                $"Cliente {cliente.Nome} excluído com sucesso.";
        }
        catch (DbUpdateException)
        {
            // Proteção adicional contra vínculos existentes no banco.
            TempData["Erro"] =
                "Não foi possível excluir este cliente porque há vínculos " +
                "no banco de dados. Nenhum registro relacionado foi " +
                "excluído automaticamente.";
        }

        return RedirectToAction(nameof(Index));
    }
}