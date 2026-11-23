
using Microsoft.AspNetCore.Identity;

namespace SistemaGestaoFinanceira.Models;

public class ApplicationUser : IdentityUser
{
    public string? Nome { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }
}
