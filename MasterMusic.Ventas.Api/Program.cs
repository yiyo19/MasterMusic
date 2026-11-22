using MasterMusic.Ventas.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions(opciones =>
{
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        var problema = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(contexto.ModelState)
        {
            Type = "about:blank", Status = 400, Title = "Datos inválidos",
            Detail = "Revisa los campos de la solicitud."
        };
        problema.Extensions["codigo"] = "VALIDACION";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problema)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        var clave = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta configurar Jwt__Key para la API de pedidos.");

        opciones.MapInboundClaims = true;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        opciones.Events = new JwtBearerEvents
        {
            OnTokenValidated = contexto =>
            {
                var identidad = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(identidad, NumberStyles.None, CultureInfo.InvariantCulture,
                    out var idUsuario) || idUsuario <= 0)
                {
                    contexto.Fail("El token requiere un identificador de usuario válido.");
                }
                return Task.CompletedTask;
            },
            OnChallenge = contexto =>
            {
                contexto.HandleResponse();
                contexto.Response.Headers.WWWAuthenticate = "Bearer";
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Autenticación requerida",
                    detail: "Debes iniciar sesión con una cuenta válida.",
                    type: "about:blank",
                    extensions: new Dictionary<string, object?> { ["codigo"] = "NO_AUTENTICADO" })
                    .ExecuteAsync(contexto.HttpContext);
            },
            OnForbidden = contexto => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acceso denegado",
                detail: "Tu cuenta no tiene permiso para realizar esta operación.",
                type: "about:blank",
                extensions: new Dictionary<string, object?> { ["codigo"] = "PROHIBIDO" })
                .ExecuteAsync(contexto.HttpContext)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<MasterMusic.Ventas.Api.Services.CreacionPedidosService>();
builder.Services.AddScoped<MasterMusic.Ventas.Api.Services.CambiosPedidosService>();
builder.Services.AddHttpClient<MasterMusic.Ventas.Api.Services.InventarioClient>((servicios, cliente) =>
{
    var configuracion = servicios.GetRequiredService<IConfiguration>();
    var direccion = new Uri(configuracion["Inventario:BaseUrl"] ?? "https://localhost:7052/");
    if (direccion.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("Inventario__BaseUrl debe usar HTTPS.");
    cliente.BaseAddress = direccion;
    cliente.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var conexionVentas = builder.Configuration.GetConnectionString("MasterMusicVentas");

if (string.IsNullOrWhiteSpace(conexionVentas))
{
    throw new InvalidOperationException(
        "Falta configurar la variable ConnectionStrings__MasterMusicVentas.");
}

builder.Services.AddDbContext<MasterMusicVentasContext>(opciones =>
    opciones.UseSqlServer(conexionVentas));

var origenesCliente = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (builder.Environment.IsDevelopment() ? new[] { "http://localhost:5173" } : Array.Empty<string>());
builder.Services.AddCors(opciones => opciones.AddPolicy("ClienteReact", politica =>
    politica.WithOrigins(origenesCliente).AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("Location", "Retry-After")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("ClienteReact");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
