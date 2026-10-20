using Asp.Versioning;
using MasterMusic.Api.Middlewares;
using MasterMusic.Api.Interfaces;
using MasterMusic.Api.Repositories;
using MasterMusic.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
// 1. Configurar Versionamiento de API
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// 2. Configurar Swagger
builder.Services.AddSwaggerGen();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuración de la conexión a SQL Server usando la variable de entorno
builder.Services.AddDbContext<MasterMusicContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterMusic")));

builder.Services.AddScoped<IInstrumentoRepository, InstrumentoRepository>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
