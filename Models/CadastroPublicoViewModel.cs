using System.ComponentModel.DataAnnotations;
using SistemaGestaoFinanceira.Validacoes;

namespace SistemaGestaoFinanceira.Models;

public class CadastroPublicoViewModel
{
    [Required(ErrorMessage = "Informe o tipo de pessoa.")]
    [RegularExpression("^(PF|PJ)$", ErrorMessage = "Selecione PF ou PJ.")]
    public string TipoPessoa { get; set; } = "PF";

    [Required(ErrorMessage = "Informe seu nome ou a razão social.")]
    [StringLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
    [Display(Name = "Nome completo ou razão social")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF ou CNPJ.")]
    [StringLength(18, ErrorMessage = "Documento inválido.")]
    [Display(Name = "CPF ou CNPJ")]
    [DocumentoFiscal(ErrorMessage = "Informe um CPF ou CNPJ válido.")]
    public string Documento { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Nome do responsável")]
    public string? NomeResponsavel { get; set; }

    [Required(ErrorMessage = "Informe uma senha.")]
    [StringLength(100, MinimumLength = 6,
        ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "As senhas não conferem.")]
    [Display(Name = "Confirmar senha")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
