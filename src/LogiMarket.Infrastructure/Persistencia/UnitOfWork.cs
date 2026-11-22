using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

internal sealed class UnitOfWork(LogiMarketDbContext db) : IUnitOfWork
{
    private const int SqliteBusy = 5, SqliteLocked = 6, SqliteConstraint = 19;

    public async Task<int> GuardarCambiosAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (Traducir(ex) is { } traducida)
        {
            throw traducida;
        }
    }

    public async Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await operacion(ct);
            await GuardarCambiosAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            try { await transaccion.RollbackAsync(CancellationToken.None); } catch (InvalidOperationException) { }
            db.ChangeTracker.Clear(); // el estado en memoria ya no refleja la base
            if (Traducir(ex) is { } traducida) throw traducida;
            throw;
        }
    }

    /// <summary>Traduce fallos de EF/SQLite a excepciones de la aplicacion; null si no hay traduccion.</summary>
    private static Exception? Traducir(Exception ex)
    {
        if (ex is ConflictoConcurrenciaException or ViolacionUnicidadException or IntegridadReferencialException) return null;
        if (ex is DbUpdateConcurrencyException)
            return new ConflictoConcurrenciaException("El registro fue modificado por otra operacion.", ex);

        var sqlite = ex as SqliteException ?? ex.InnerException as SqliteException;
        if (sqlite is null) return null;
        return sqlite.SqliteErrorCode switch
        {
            SqliteBusy or SqliteLocked => new ConflictoConcurrenciaException("La base de datos esta ocupada por otra operacion.", ex),
            SqliteConstraint when sqlite.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                => new ViolacionUnicidadException("Violacion de restriccion de unicidad.", ex),
            SqliteConstraint when sqlite.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                => new IntegridadReferencialException("El registro esta referenciado por otros datos.", ex),
            _ => null
        };
    }
}