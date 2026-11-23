using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemaGestaoFinanceira.Models;

namespace SistemaGestaoFinanceira.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme> ExternalLogins { get; set; }
        = new List<AuthenticationScheme>();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar-me")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        ExternalLogins = (
            await _signInManager
                .GetExternalAuthenticationSchemesAsync()
        ).ToList();
    }

    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null)
    {
        Console.WriteLine("====================================");
        Console.WriteLine("ONPOST FOI EXECUTADO");
        Console.WriteLine($"EMAIL: [{Input.Email}]");
        Console.WriteLine(
            $"PASSWORD RECEBIDA: [{!string.IsNullOrEmpty(Input.Password)}]");
        Console.WriteLine("====================================");

        ReturnUrl = returnUrl;

        ExternalLogins = (
            await _signInManager
                .GetExternalAuthenticationSchemesAsync()
        ).ToList();

        Input.Email = Input.Email?.Trim() ?? string.Empty;

        _logger.LogInformation(
            "Tentativa de login recebida para o usuario: {Email}",
            Input.Email);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "Falha na validacao do formulario de login. Email: {Email}",
                Input.Email);

            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Usuario autenticado com sucesso: {Email}",
                Input.Email);

            if (!string.IsNullOrEmpty(ReturnUrl) &&
                Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("/Index");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning(
                "Conta bloqueada: {Email}",
                Input.Email);

            ModelState.AddModelError(
                string.Empty,
                "Esta conta esta temporariamente bloqueada.");

            return Page();
        }

        if (result.IsNotAllowed)
        {
            _logger.LogWarning(
                "Login nao permitido para: {Email}",
                Input.Email);

            ModelState.AddModelError(
                string.Empty,
                "O acesso deste usuario nao esta permitido.");

            return Page();
        }

        if (result.RequiresTwoFactor)
        {
            _logger.LogInformation(
                "Usuario requer autenticacao de dois fatores: {Email}",
                Input.Email);

            return RedirectToPage(
                "./LoginWith2fa",
                new
                {
                    ReturnUrl = ReturnUrl,
                    RememberMe = Input.RememberMe
                });
        }

        ModelState.AddModelError(
            string.Empty,
            "E-mail ou senha invalidos.");

        _logger.LogWarning(
            "Falha de autenticacao para: {Email}",
            Input.Email);

        return Page();
    }
}
