using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Usuarios()
    {
        var usuarios = await _userManager.Users
            .Include(u => u.Cliente)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var papeisUsuarios = new Dictionary<string, IList<string>>();

        foreach (var usuario in usuarios)
        {
            papeisUsuarios[usuario.Id] =
                await _userManager.GetRolesAsync(usuario);
        }

        ViewBag.PapeisUsuarios = papeisUsuarios;

        return View(usuarios);
    }

    private void CarregarClientes()
    {
        ViewBag.Clientes = _context.Clientes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Nome
            })
            .ToList();
    }

    [HttpGet]
    public IActionResult CriarUsuario()
    {
        CarregarClientes();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarUsuario(
        string nome,
        string email,
        string senha,
        string confirmarSenha,
        int clienteId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            ModelState.AddModelError("nome", "Informe o nome do usuário.");
        else if (nome.Trim().Length > 150)
            ModelState.AddModelError("nome", "O nome deve ter no máximo 150 caracteres.");

        if (string.IsNullOrWhiteSpace(email))
            ModelState.AddModelError("email", "Informe o e-mail.");

        if (string.IsNullOrWhiteSpace(senha))
            ModelState.AddModelError("senha", "Informe a senha.");

        if (!string.Equals(senha, confirmarSenha, StringComparison.Ordinal))
            ModelState.AddModelError("confirmarSenha", "As senhas não são iguais.");

        var clienteValido = clienteId > 0 &&
            await _context.Clientes.AnyAsync(c => c.Id == clienteId && c.Ativo);

        if (!clienteValido)
            ModelState.AddModelError("clienteId", "Selecione uma empresa válida e ativa.");

        if (!ModelState.IsValid)
        {
            CarregarClientes();
            return View();
        }

        nome = nome.Trim();
        email = email.Trim();

        if (await _userManager.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError("email", "Este e-mail já está cadastrado.");
            CarregarClientes();
            return View();
        }

        var usuario = new ApplicationUser
        {
            Nome = nome,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ClienteId = clienteId
        };

        var resultado = await _userManager.CreateAsync(usuario, senha);

        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
                ModelState.AddModelError(string.Empty, erro.Description);

            CarregarClientes();
            return View();
        }

        var resultadoPapel = await _userManager.AddToRoleAsync(usuario, "User");

        if (!resultadoPapel.Succeeded)
        {
            await _userManager.DeleteAsync(usuario);

            foreach (var erro in resultadoPapel.Errors)
                ModelState.AddModelError(string.Empty, erro.Description);

            CarregarClientes();
            return View();
        }

        var nomeCliente = await _context.Clientes
            .Where(c => c.Id == clienteId)
            .Select(c => c.Nome)
            .FirstAsync();

        TempData["Sucesso"] = $"Usuário {email} criado para a empresa {nomeCliente}.";
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpGet]
    public async Task<IActionResult> EditarUsuario(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Erro"] = "Usuário não informado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var papeis = await _userManager.GetRolesAsync(usuario);

        var model = new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            Nome = usuario.Nome ?? string.Empty,
            Email = usuario.Email ?? string.Empty,
            ClienteId = usuario.ClienteId,
            Perfil = papeis.Contains("Admin") ? "Admin" : "User"
        };

        CarregarClientes();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel model)
    {
        if (!ModelState.IsValid)
        {
            CarregarClientes();
            return View(model);
        }

        var usuario = await _userManager.FindByIdAsync(model.Id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var nome = model.Nome.Trim();
        var email = model.Email.Trim();

        var usuarioComEmail = await _userManager.FindByEmailAsync(email);

        if (usuarioComEmail != null && usuarioComEmail.Id != usuario.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "Este e-mail já está cadastrado.");
            CarregarClientes();
            return View(model);
        }

        if (model.ClienteId is not int clienteId ||
            !await _context.Clientes.AnyAsync(c => c.Id == clienteId && c.Ativo))
        {
            ModelState.AddModelError(nameof(model.ClienteId), "Selecione uma empresa válida e ativa.");
            CarregarClientes();
            return View(model);
        }

        if (model.Perfil != "Admin" && model.Perfil != "User")
        {
            ModelState.AddModelError(nameof(model.Perfil), "Perfil de acesso inválido.");
            CarregarClientes();
            return View(model);
        }

        var papeisAtuais = await _userManager.GetRolesAsync(usuario);
        var eraAdmin = papeisAtuais.Contains("Admin");
        var seraAdmin = model.Perfil == "Admin";

        if (eraAdmin && !seraAdmin)
        {
            var administradores = await _userManager.GetUsersInRoleAsync("Admin");

            if (administradores.Count <= 1)
            {
                ModelState.AddModelError(
                    nameof(model.Perfil),
                    "Não é permitido remover o último administrador do sistema.");

                CarregarClientes();
                return View(model);
            }
        }

        usuario.Nome = nome;
        usuario.Email = email;
        usuario.UserName = email;
        usuario.ClienteId = clienteId;

        var resultadoAtualizacao = await _userManager.UpdateAsync(usuario);

        if (!resultadoAtualizacao.Succeeded)
        {
            foreach (var erro in resultadoAtualizacao.Errors)
                ModelState.AddModelError(string.Empty, erro.Description);

            CarregarClientes();
            return View(model);
        }

        IdentityResult resultadoPapel = IdentityResult.Success;

        if (!eraAdmin && seraAdmin)
            resultadoPapel = await _userManager.AddToRoleAsync(usuario, "Admin");
        else if (eraAdmin && !seraAdmin)
            resultadoPapel = await _userManager.RemoveFromRoleAsync(usuario, "Admin");

        if (!resultadoPapel.Succeeded)
        {
            TempData["Erro"] =
                "Os dados foram atualizados, mas houve uma falha ao alterar o perfil. Confira o usuário antes de tentar novamente.";

            return RedirectToAction(nameof(Usuarios));
        }

        TempData["Sucesso"] = $"Usuário {email} atualizado com sucesso.";
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpGet]
    public async Task<IActionResult> AlterarSenha(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Erro"] = "Usuário não informado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        ViewBag.Email = usuario.Email;
        ViewBag.UsuarioId = usuario.Id;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSenha(
        string id,
        string novaSenha,
        string confirmarSenha)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Erro"] = "Usuário não informado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        ViewBag.Email = usuario.Email;
        ViewBag.UsuarioId = usuario.Id;

        if (string.IsNullOrWhiteSpace(novaSenha))
            ModelState.AddModelError("novaSenha", "Informe a nova senha.");

        if (string.IsNullOrWhiteSpace(confirmarSenha))
            ModelState.AddModelError("confirmarSenha", "Confirme a nova senha.");

        if (!string.Equals(novaSenha, confirmarSenha, StringComparison.Ordinal))
            ModelState.AddModelError("confirmarSenha", "As senhas não são iguais.");

        if (!ModelState.IsValid)
            return View();

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, novaSenha);

        if (resultado.Succeeded)
        {
            TempData["Sucesso"] = $"Senha do usuário {usuario.Email} alterada com sucesso.";
            return RedirectToAction(nameof(Usuarios));
        }

        foreach (var erro in resultado.Errors)
            ModelState.AddModelError(string.Empty, erro.Description);

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesativarUsuario(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuarioLogadoId = _userManager.GetUserId(User);

        if (usuario.Id == usuarioLogadoId)
        {
            TempData["Erro"] = "Você não pode desativar sua própria conta.";
            return RedirectToAction(nameof(Usuarios));
        }

        var papeis = await _userManager.GetRolesAsync(usuario);

        if (papeis.Contains("Admin"))
        {
            var administradores = await _userManager.GetUsersInRoleAsync("Admin");

            if (administradores.Count <= 1)
            {
                TempData["Erro"] = "Não é permitido desativar o último administrador.";
                return RedirectToAction(nameof(Usuarios));
            }
        }

        await _userManager.SetLockoutEnabledAsync(usuario, true);
        await _userManager.SetLockoutEndDateAsync(
            usuario,
            DateTimeOffset.UtcNow.AddYears(100));

        TempData["Sucesso"] = $"Usuário {usuario.Email} desativado com sucesso.";
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarUsuario(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        await _userManager.SetLockoutEndDateAsync(usuario, null);

        TempData["Sucesso"] = $"Usuário {usuario.Email} ativado com sucesso.";
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirUsuario(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Erro"] = "Usuário não informado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuario = await _userManager.FindByIdAsync(id);

        if (usuario == null)
        {
            TempData["Erro"] = "Usuário não encontrado.";
            return RedirectToAction(nameof(Usuarios));
        }

        var usuarioLogadoId = _userManager.GetUserId(User);

        if (usuario.Id == usuarioLogadoId)
        {
            TempData["Erro"] = "Você não pode excluir sua própria conta.";
            return RedirectToAction(nameof(Usuarios));
        }

        var papeis = await _userManager.GetRolesAsync(usuario);

        if (papeis.Contains("Admin"))
        {
            var administradores = await _userManager.GetUsersInRoleAsync("Admin");

            if (administradores.Count <= 1)
            {
                TempData["Erro"] =
                    "Não é permitido excluir o último administrador do sistema.";

                return RedirectToAction(nameof(Usuarios));
            }
        }

        var resultado = await _userManager.DeleteAsync(usuario);

        if (!resultado.Succeeded)
        {
            TempData["Erro"] = "Não foi possível excluir o usuário: " +
                string.Join("; ", resultado.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Usuarios));
        }

        TempData["Sucesso"] =
            $"Usuário {usuario.Email} excluído com sucesso.";

        return RedirectToAction(nameof(Usuarios));
    }
}