using Asp.Versioning;
using MasterMusic.Api.Data;
using MasterMusic.Api.Interfaces;
using MasterMusic.Api.Middlewares;
using MasterMusic.Api.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

// 1. Configurar Versionamiento de API
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
    options.ReportApiVersions = true;
}).AddMvc();

// 2. Configurar Swagger con soporte JWT (.NET 10)
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MasterMusic API",
        Version = "v1"
    });

    // Definición del esquema de seguridad Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa únicamente tu token JWT devuelto por el Login."
    });

    // Requerimiento global de seguridad compatible con .NET 10
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});





// 3. Configurar Autenticación con JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Falta la variable de entorno Jwt__Key.");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = contexto =>
        {
            contexto.HandleResponse();
            contexto.Response.Headers.WWWAuthenticate = "Bearer";
            return Results.Problem(statusCode: 401, type: "about:blank",
                title: "Autenticación requerida", detail: "Debes iniciar sesión con una cuenta válida.",
                extensions: new Dictionary<string, object?> { ["codigo"] = "NO_AUTENTICADO" })
                .ExecuteAsync(contexto.HttpContext);
        },
        OnForbidden = contexto => Results.Problem(statusCode: 403, type: "about:blank",
            title: "Acceso denegado", detail: "Tu cuenta no tiene permiso para esta operación.",
            extensions: new Dictionary<string, object?> { ["codigo"] = "PROHIBIDO" })
            .ExecuteAsync(contexto.HttpContext)
    };
});

builder.Services.AddOpenApi();
builder.Services.AddScoped<MasterMusic.Api.Filters.ClaveServicioFilter>();

// Configuración de la conexión a SQL Server
builder.Services.AddDbContext<MasterMusicContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterMusic")));

builder.Services.AddScoped<IInstrumentoRepository, InstrumentoRepository>();

var origenesCliente = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (builder.Environment.IsDevelopment() ? new[] { "http://localhost:5173" } : Array.Empty<string>());
builder.Services.AddCors(opciones => opciones.AddPolicy("ClienteReact", politica =>
    politica.WithOrigins(origenesCliente).AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("Location", "Retry-After")));

var app = builder.Build();
if (string.IsNullOrWhiteSpace(app.Configuration["Jwt:Key"]))
    throw new InvalidOperationException("Falta la variable de entorno Jwt__Key.");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MasterMusicContext>();
    await SeedData.InicializarAsync(db, app.Configuration);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

app.UseCors("ClienteReact");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }