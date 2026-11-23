using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<RegisterModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public CadastroPublicoViewModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.TipoPessoa == "PJ" &&
            string.IsNullOrWhiteSpace(Input.NomeResponsavel))
        {
            ModelState.AddModelError(
                "Input.NomeResponsavel",
                "Informe o nome do responsável pela empresa.");
        }

        if (!ModelState.IsValid)
            return Page();

        // Normalizar o documento antes de consultar ou gravar.
        Input.Documento = new string(
            Input.Documento.Where(char.IsDigit).ToArray());

        // Garantir correspondência entre tipo de pessoa e documento.
        if (Input.TipoPessoa == "PF" &&
            Input.Documento.Length != 11)
        {
            ModelState.AddModelError(
                "Input.Documento",
                "Pessoa Física deve informar um CPF válido.");
        }

        if (Input.TipoPessoa == "PJ" &&
            Input.Documento.Length != 14)
        {
            ModelState.AddModelError(
                "Input.Documento",
                "Pessoa Jurídica deve informar um CNPJ válido.");
        }

        if (!ModelState.IsValid)
            return Page();

        Input.Email = Input.Email.Trim();

        // Impedir duplicidade de documento.
        bool documentoExiste = await _context.Clientes
            .AnyAsync(c => c.Documento == Input.Documento);

        if (documentoExiste)
        {
            ModelState.AddModelError(
                "Input.Documento",
                "Este CPF ou CNPJ já está cadastrado.");

            return Page();
        }

        // Verificar se o e-mail já pertence a um usuário.
        var usuarioExistente =
            await _userManager.FindByEmailAsync(Input.Email);

        if (usuarioExistente != null)
        {
            ModelState.AddModelError(
                "Input.Email",
                "Este e-mail já possui uma conta.");

            return Page();
        }

        await using var transacao =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var cliente = new Cliente
            {
                Nome = Input.Nome.Trim(),
                TipoPessoa = Input.TipoPessoa,
                Documento = Input.Documento,
                EmailContato = Input.Email,
                NomeResponsavel = Input.TipoPessoa == "PJ"
                    ? Input.NomeResponsavel?.Trim()
                    : null,
                Ativo = true,
                DataCadastro = DateTime.UtcNow
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var usuario = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                Nome = Input.Nome.Trim(),
                ClienteId = cliente.Id
            };

            var resultadoCriacao = await _userManager.CreateAsync(
                usuario,
                Input.Password);

            if (!resultadoCriacao.Succeeded)
            {
                foreach (var erro in resultadoCriacao.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        erro.Description);
                }

                return Page();
            }

            // O cadastro público recebe somente a função User.
            var resultadoRole = await _userManager.AddToRoleAsync(
                usuario,
                "User");

            if (!resultadoRole.Succeeded)
            {
                foreach (var erro in resultadoRole.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        erro.Description);
                }

                return Page();
            }

            await transacao.CommitAsync();

            return RedirectToPage("./Login");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Falha de persistência durante o cadastro público.");

            ModelState.AddModelError(
                string.Empty,
                "Não foi possível concluir o cadastro. Verifique se o documento ou e-mail já está cadastrado e tente novamente.");

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha durante o cadastro público.");

            ModelState.AddModelError(
                string.Empty,
                "Ocorreu um erro ao criar a conta. Tente novamente.");

            return Page();
        }
    }
}
