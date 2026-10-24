using System.Net;

namespace MasterMusic.Tests
{
    public class InstrumentosTests : IClassFixture<MasterMusicFactory>
    {
        private readonly HttpClient _client;

        public InstrumentosTests(MasterMusicFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetInstrumentos_SinToken_Devuelve200()
        {
            var respuesta = await _client.GetAsync("/api/v1.0/Instrumentos");

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }
}