using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Data;

internal static class BloqueoSqlServer
{
    // El bloqueo se libera al confirmar o revertir la transacción del llamador.
    public static Task TomarAsync(DbContext contexto, string recurso, CancellationToken cancelacion) =>
        contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock
                @Resource = {recurso},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 1000;
            IF @resultado < 0 THROW 51000, 'La operacion esta en curso. Reintente con la misma clave.', 1;
            """, cancelacion);
}
