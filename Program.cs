using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoFinanceira.Models;

var builder = WebApplication.CreateBuilder(args);

// Banco de dados
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// MVC + Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// ============================================================
// CONFIGURAÇÃO INICIAL DO IDENTITY
// Cria as roles Admin/User e define o usuário administrador
// ============================================================

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
    .GetRequiredService<UserManager<ApplicationUser>>();

    // Criar role Admin
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        var resultadoAdmin = await roleManager.CreateAsync(
            new IdentityRole("Admin"));

        if (!resultadoAdmin.Succeeded)
        {
            foreach (var erro in resultadoAdmin.Errors)
            {
                Console.WriteLine(
                    $"ERRO AO CRIAR ROLE ADMIN: {erro.Description}");
            }
        }
    }

    // Criar role User
    if (!await roleManager.RoleExistsAsync("User"))
    {
        var resultadoUser = await roleManager.CreateAsync(
            new IdentityRole("User"));

        if (!resultadoUser.Succeeded)
        {
            foreach (var erro in resultadoUser.Errors)
            {
                Console.WriteLine(
                    $"ERRO AO CRIAR ROLE USER: {erro.Description}");
            }
        }
    }

    // Usuário administrador principal
    var adminEmail = "holivemax001@gmail.com";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser != null)
    {
        // Garantir que o usuário seja Admin
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            var resultadoRole = await userManager.AddToRoleAsync(
                adminUser,
                "Admin");

            if (resultadoRole.Succeeded)
            {
                Console.WriteLine(
                    $"USUÁRIO {adminEmail} ADICIONADO À ROLE ADMIN.");
            }
            else
            {
                foreach (var erro in resultadoRole.Errors)
                {
                    Console.WriteLine(
                        $"ERRO AO ATRIBUIR ADMIN: {erro.Description}");
                }
            }
        }
        else
        {
            Console.WriteLine(
                $"USUÁRIO {adminEmail} JÁ POSSUI A ROLE ADMIN.");
        }
    }
    else
    {
        Console.WriteLine(
            $"AVISO: Usuário {adminEmail} não encontrado.");
    }
}

// ============================================================
// PIPELINE HTTP
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Autenticação deve vir antes da autorização
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();