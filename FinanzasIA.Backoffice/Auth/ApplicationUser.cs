using Microsoft.AspNetCore.Identity;

namespace FinanzasIA.Backoffice.Auth;

public class ApplicationUser : IdentityUser
{
    public string? NombreCompleto { get; set; }

    public string? TelefonoWhatsApp { get; set; }
}
