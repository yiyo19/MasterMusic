using MasterMusic.Api.Interfaces;
using MasterMusic.Api.Repositories;
using MasterMusic.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
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
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
