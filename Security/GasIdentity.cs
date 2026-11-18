using System.Security.Claims;
using GAS.Admin.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Security;

/// <summary>Agrega a la sesión el nombre y si el usuario debe cambiar su contraseña.</summary>
public class GasClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public GasClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.NombreCompleto))
        {
            identity.AddClaim(new Claim(GasClaims.Nombre, user.NombreCompleto));
        }

        if (user.DebeCambiarPassword)
        {
            identity.AddClaim(new Claim(GasClaims.DebeCambiarPassword, "true"));
        }

        return identity;
    }
}

/// <summary>Impide iniciar sesión a usuarios desactivados.</summary>
public class GasSignInManager : SignInManager<ApplicationUser>
{
    public GasSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.Activo && await base.CanSignInAsync(user);
}
