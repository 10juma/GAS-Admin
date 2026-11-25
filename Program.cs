using GAS.Admin.Services.Soporte;
using GAS.Admin.Data.Context;
using GAS.Admin.Data.Interceptors;
using GAS.Admin.Data.Seed;
using GAS.Admin.Middleware;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Formatos de México (decimales con punto, fechas dd/MM/yyyy) en cualquier equipo.
var culturaMx = new System.Globalization.CultureInfo("es-MX");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaMx;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaMx;

var builder = WebApplication.CreateBuilder(args);

// Base de datos
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontró DefaultConnection.");

// Llaves capturadas en GAS-Admin (Resend, Stripe, integraciones, productos): cifradas en la BD con la
// llave maestra. Fuera de la BD solo viven la conexión (appsettings.Development.json o entorno) y esa llave
// (entorno o ~/.gas-admin/llave-maestra.key, que en Development se genera sola).
var cifrado = GAS.Admin.Security.CifradoConfiguracion.Crear(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(cifrado);
((IConfigurationBuilder)builder.Configuration).Add(new GAS.Admin.Infrastructure.Configuracion.FuenteConfiguracionBd(connectionString, cifrado));
builder.Services.AddScoped<GAS.Admin.Services.Configuracion.ServicioLlaves>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditoriaInterceptor>();
builder.Services.AddDbContext<GasDbContext>((sp, options) => options
    .UseSqlServer(connectionString)
    .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>()));

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<GasDbContext>()
    .AddSignInManager<GasSignInManager>()
    .AddClaimsPrincipalFactory<GasClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

// Cada 5 minutos se revalida la sesión: aplica cambios de permisos,
// desactivaciones y restablecimientos de contraseña.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "GAS.Admin.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    // "Recordarme": la sesión sobrevive al cerrar el navegador y dura 30 días sin uso (sin marcarlo, 8 horas
    // y termina al cerrar el navegador). El sello de seguridad se sigue revisando cada 5 minutos.
    options.Events.OnSigningIn = contexto =>
    {
        if (contexto.Properties.IsPersistent)
        {
            contexto.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
        }
        return Task.CompletedTask;
    };
});

// Enlaces de "¿Olvidaste tu contraseña?" (y restablecimientos): válidos 30 minutos.
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = GAS.Admin.Services.Seguridad.RecuperacionPassword.Vigencia);
builder.Services.Configure<GAS.Admin.Services.Seguridad.OpcionesSeguridad>(builder.Configuration.GetSection("Seguridad"));
builder.Services.AddScoped<GAS.Admin.Services.Seguridad.RecuperacionPassword>();

// Permisos por módulo: una política por permiso.
builder.Services.AddAuthorization(options =>
{
    foreach (var permiso in Permisos.Todos)
    {
        options.AddPolicy(permiso, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermisoRequirement(permiso)));
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, PermisoHandler>();

// Servicios
builder.Services.AddScoped<IAuditoria, ServicioAuditoria>();
builder.Services.AddMemoryCache();

// Llaves de Data Protection (cookie de sesión, antifalsificación, enlaces de recuperación de contraseña).
// Fuera de Development se guardan en disco (DataProtection:Ruta, por defecto /keys, un volumen de Docker):
// si no, cada despliegue o reinicio cerraría todas las sesiones e invalidaría los enlaces enviados.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:Ruta"] is { Length: > 0 } rutaLlaves ? rutaLlaves : "/keys"))
        .SetApplicationName("GAS-Admin");
    // En producción todo va por https: la cookie antifalsificación también solo viaja cifrada.
    builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.Always);
}

// Detrás de Nginx (TLS en Nginx, la app escucha en http): se toman el esquema y la IP real de
// X-Forwarded-Proto/For. Se confía en cualquier origen porque el contenedor solo publica su puerto
// en 127.0.0.1, así que únicamente Nginx puede llegar a él.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Archivos de proyectos: disco privado fuera de wwwroot (Archivos:Ruta), máximo 25 MB.
builder.Services.Configure<GAS.Admin.Services.Archivos.OpcionesArchivos>(builder.Configuration.GetSection("Archivos"));
builder.Services.AddSingleton<GAS.Admin.Services.Archivos.IAlmacenArchivos, GAS.Admin.Services.Archivos.AlmacenDisco>();
builder.Services.AddScoped<GAS.Admin.Services.Finanzas.ServicioFinanzas>();
builder.Services.AddScoped<GAS.Admin.Services.Finanzas.ReportesFinanzas>();
builder.Services.AddScoped<GAS.Admin.Services.Infraestructura.ServicioInfraestructura>();

// Stripe: una API key restringida (solo lectura) por producto en Stripe:Claves:{clave}; nunca en la BD.
builder.Services.Configure<GAS.Admin.Services.Stripe.OpcionesStripe>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddHttpClient<GAS.Admin.Services.Stripe.ClienteStripe>((sp, client) =>
{
    var opciones = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GAS.Admin.Services.Stripe.OpcionesStripe>>().Value;
    client.BaseAddress = new Uri(opciones.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<GAS.Admin.Services.Stripe.ServicioStripe>();
builder.Services.AddHostedService<GAS.Admin.Services.Stripe.SincronizadorStripe>();

// Integraciones técnicas (UptimeRobot, GitHub, Sentry): llaves de solo lectura en la sección Integraciones; nunca en la BD.
builder.Services.Configure<GAS.Admin.Services.Integraciones.OpcionesIntegraciones>(builder.Configuration.GetSection("Integraciones"));
builder.Services.AddHttpClient("integraciones", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<GAS.Admin.Services.Integraciones.ServicioIntegraciones>();
builder.Services.AddHostedService<GAS.Admin.Services.Integraciones.SincronizadorIntegraciones>();

// Conexión con el super-admin de cada producto (API GAS v1, docs/api-gas-v1.md). Secretos en Productos:Conexiones:{clave}:Secreto.
builder.Services.Configure<GAS.Admin.Services.Productos.OpcionesConexiones>(builder.Configuration.GetSection("Productos"));
builder.Services.AddHttpClient("productos", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<GAS.Admin.Services.Productos.ServicioConexiones>();
builder.Services.AddHostedService<GAS.Admin.Services.Productos.SincronizadorConexiones>();

// Atención al cliente: correo entrante (webhook de Resend) y tickets desde productos (API GAS).
builder.Services.Configure<GAS.Admin.Services.Soporte.OpcionesSoporte>(builder.Configuration.GetSection("Soporte"));
builder.Services.AddHttpClient("soporte", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<GAS.Admin.Services.Soporte.ServicioSoporte>();

// Dirección: indicadores ejecutivos y cierre mensual automático.
builder.Services.AddScoped<GAS.Admin.Services.Direccion.ServicioIndicadores>();
builder.Services.AddHostedService<GAS.Admin.Services.Direccion.CierreMensualAutomatico>();
builder.Services.AddScoped<GAS.Admin.Services.Proyectos.ServicioTrabajos>();

// Comercial: PDF de cotizaciones (QuestPDF, licencia Community) y correo con Resend.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddSingleton<GAS.Admin.Services.Comercial.GeneradorPdfCotizacion>();
builder.Services.Configure<GAS.Admin.Services.Correo.OpcionesResend>(builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient<GAS.Admin.Services.Correo.IServicioCorreo, GAS.Admin.Services.Correo.ServicioCorreoResend>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
// Despliegue automático: SSH con llave a los servidores, código desde GitHub, compilación en el servidor.
// Dirección, llave privada y variables de los contenedores van cifradas en LlavesConfiguracion (Despliegue:*).
builder.Services.AddHttpClient("despliegues", client => client.Timeout = TimeSpan.FromMinutes(10));
builder.Services.AddScoped<GAS.Admin.Services.Despliegues.ServicioServidores>();
builder.Services.AddScoped<GAS.Admin.Services.Despliegues.ServicioDespliegues>();
builder.Services.AddScoped<GAS.Admin.Services.Despliegues.EjecutorDespliegue>();
builder.Services.AddSingleton<GAS.Admin.Services.Despliegues.ColaDespliegues>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GAS.Admin.Services.Despliegues.ColaDespliegues>());

// Respaldos de las bases en Backblaze B2 (cifrados, con bloqueo contra borrado). Credenciales en LlavesConfiguracion (Respaldos:*).
builder.Services.AddHttpClient("respaldos", client => client.Timeout = TimeSpan.FromMinutes(30));
builder.Services.AddScoped<GAS.Admin.Services.Respaldos.ServicioRespaldos>();
builder.Services.AddSingleton<GAS.Admin.Services.Respaldos.ProgramadorRespaldos>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GAS.Admin.Services.Respaldos.ProgramadorRespaldos>());
builder.Services.AddScoped<GAS.Admin.Services.Navegacion.ContadoresMenu>();
builder.Services.AddScoped<GAS.Admin.Services.Correo.PlantillaCorreo>();

// Todas las páginas requieren sesión, salvo el login, acceso denegado y la de error.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Account/RecuperarPassword");
    options.Conventions.AllowAnonymousToPage("/Account/NuevaPassword");
    options.Conventions.AllowAnonymousToPage("/Error");
})
    .AddMvcOptions(o => o.Filters.Add<GAS.Admin.Infrastructure.Filtros.FiltroErroresDeDatos>());

var app = builder.Build();

// Solo se siembran roles y permisos; los catálogos (productos, servicios, departamentos, categorías,
// SLA, empresa) los captura el usuario desde cero.
await SeguridadSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(); // primero: el resto debe ver https y la IP del cliente
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    await AdminSeeder.SeedAsync(app.Services);
}

app.UseStatusCodePagesWithReExecute("/Error", "?codigo={0}");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CambioPasswordObligatorioMiddleware>();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapEndpointsSoporte();

app.Run();
