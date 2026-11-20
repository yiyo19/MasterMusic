using MasterMusic.Ventas.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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

var conexionVentas = builder.Configuration.GetConnectionString("MasterMusicVentas");

if (string.IsNullOrWhiteSpace(conexionVentas))
{
    throw new InvalidOperationException(
        "Falta configurar la variable ConnectionStrings__MasterMusicVentas.");
}

builder.Services.AddDbContext<MasterMusicVentasContext>(opciones =>
    opciones.UseSqlServer(conexionVentas));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
