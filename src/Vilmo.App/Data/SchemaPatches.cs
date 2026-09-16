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

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE product ADD COLUMN IF NOT EXISTS source_url text NULL;
            ALTER TABLE product ADD COLUMN IF NOT EXISTS linked_cart_product_id uuid NULL;
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cart_import_batch (
              id uuid PRIMARY KEY,
              company_id uuid NOT NULL,
              created_by_user_id uuid NOT NULL,
              file_name varchar(260) NOT NULL,
              status varchar(32) NOT NULL,
              row_count int NOT NULL DEFAULT 0,
              image_ok int NOT NULL DEFAULT 0,
              image_failed int NOT NULL DEFAULT 0,
              error text NULL,
              csv_text text NOT NULL,
              created_at timestamptz NOT NULL,
              processed_at timestamptz NULL
            );
            CREATE INDEX IF NOT EXISTS ix_cart_import_batch_company_created
              ON cart_import_batch (company_id, created_at);
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cart_import_log (
              id uuid PRIMARY KEY,
              company_id uuid NOT NULL,
              batch_id uuid NOT NULL,
              step_code varchar(64) NOT NULL,
              "level" varchar(16) NOT NULL,
              user_message text NOT NULL,
              technical_json text NOT NULL,
              created_at timestamptz NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_cart_import_log_batch
              ON cart_import_log (batch_id, created_at);
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cart_product (
              id uuid PRIMARY KEY,
              company_id uuid NOT NULL,
              batch_id uuid NOT NULL,
              source_id varchar(64) NOT NULL,
              sku varchar(120) NOT NULL,
              name varchar(500) NOT NULL,
              quantity numeric(18,4) NOT NULL,
              unit_price numeric(18,4) NOT NULL,
              line_total numeric(18,4) NOT NULL,
              source_url text NULL,
              description text NULL,
              linked_product_id uuid NULL,
              linked_nfe_document_id uuid NULL,
              linked_nfe_n_item int NULL,
              created_at timestamptz NOT NULL,
              updated_at timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_cart_product_company_source
              ON cart_product (company_id, source_id);
            CREATE INDEX IF NOT EXISTS ix_cart_product_company_sku
              ON cart_product (company_id, sku);
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cart_product_image (
              id uuid PRIMARY KEY,
              cart_product_id uuid NOT NULL,
              company_id uuid NOT NULL,
              source_url text NOT NULL,
              kind varchar(16) NOT NULL,
              sort_order int NOT NULL,
              content_type varchar(128) NULL,
              relative_path text NOT NULL DEFAULT '',
              byte_length int NOT NULL DEFAULT 0,
              status varchar(16) NOT NULL,
              error text NULL
            );
            CREATE INDEX IF NOT EXISTS ix_cart_product_image_product_sort
              ON cart_product_image (cart_product_id, sort_order);
            """, ct);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS product_image (
              id uuid PRIMARY KEY,
              product_id uuid NOT NULL,
              company_id uuid NOT NULL,
              cart_product_image_id uuid NULL,
              sort_order int NOT NULL,
              relative_path text NOT NULL,
              content_type varchar(128) NULL
            );
            CREATE INDEX IF NOT EXISTS ix_product_image_product_sort
              ON product_image (product_id, sort_order);
            """, ct);
    }
}
