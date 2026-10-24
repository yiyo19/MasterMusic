using MasterMusic.Api.Data;
using MasterMusic.Api.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace MasterMusic.Tests
{
    
    public static class DatosPrueba
    {
        
        public static (int IdCategoria, int IdMarca) CrearCatalogo(MasterMusicFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterMusicContext>();

            var categoria = new Categoria { Nombre = "Guitarras", Descripcion = "Instrumentos de cuerda" };
            var marca = new Marca { Nombre = "Fender", Pais = "Estados Unidos" };
            db.Categorias.Add(categoria);
            db.Marcas.Add(marca);
            db.SaveChanges();

            return (categoria.IdCategoria, marca.IdMarca);
        }

        
        public static int CrearInstrumento(MasterMusicFactory factory)
        {
            var (idCategoria, idMarca) = CrearCatalogo(factory);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterMusicContext>();

            var instrumento = new Instrumento
            {
                Nombre = "Guitarra Eléctrica Stratocaster",
                Modelo = "Player Series",
                Precio = 499990,
                Stock = 5,
                Descripcion = "Instrumento de prueba",
                Foto = "guitarra.jpg",
                Activo = true,
                IdCategoria = idCategoria,
                IdMarca = idMarca
            };
            db.Instrumentos.Add(instrumento);
            db.SaveChanges();

            return instrumento.IdInstrumento;
        }
    }
}