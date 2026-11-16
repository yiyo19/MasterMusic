using MasterMusic.Ventas.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
