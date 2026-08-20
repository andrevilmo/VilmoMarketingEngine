using Microsoft.EntityFrameworkCore;

namespace Vilmo.Data;

public static class SchemaPatches
{
    /// <summary>
    /// EnsureCreated does not add tables to an existing database. Live Postgres
    /// already has the original schema, so new tables are created here.
    /// </summary>
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsNpgsql())
            return;

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS nfe_ingest_log (
              id uuid PRIMARY KEY,
              company_id uuid NOT NULL,
              run_id uuid NOT NULL,
              nfe_document_id uuid NULL,
              chave_acesso varchar(44) NULL,
              step_code varchar(64) NOT NULL,
              "level" varchar(16) NOT NULL,
              user_message text NOT NULL,
              technical_json text NOT NULL,
              created_at timestamptz NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_company_id_created_at
              ON nfe_ingest_log (company_id, created_at);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_company_chave
              ON nfe_ingest_log (company_id, chave_acesso, created_at);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_run_id
              ON nfe_ingest_log (run_id);
            """, ct);
    }
}
