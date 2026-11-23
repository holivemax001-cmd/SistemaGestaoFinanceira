
using System.ComponentModel.DataAnnotations;

namespace SistemaGestaoFinanceira.Models;

public class EditarUsuarioViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome do usuário.")]
    [StringLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione uma empresa.")]
    [Display(Name = "Empresa")]
    public int? ClienteId { get; set; }

    [Required(ErrorMessage = "Selecione um perfil.")]
    [Display(Name = "Perfil de acesso")]
    public string Perfil { get; set; } = "User";
}
