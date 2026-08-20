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
            CREATE TABLE IF NOT EXISTS advertisement (
              id uuid PRIMARY KEY,
              company_id uuid NOT NULL,
              vendor_user_id uuid NOT NULL,
              kind varchar(16) NOT NULL,
              sku varchar(64) NOT NULL,
              title text NOT NULL,
              description text NULL,
              price numeric NOT NULL,
              currency varchar(8) NOT NULL,
              available_quantity numeric NOT NULL,
              condition varchar(16) NOT NULL,
              brand varchar(128) NULL,
              gtin varchar(32) NULL,
              weight_grams numeric NULL,
              height_cm numeric NULL,
              width_cm numeric NULL,
              length_cm numeric NULL,
              created_at timestamptz NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_company_vendor_sku
              ON advertisement (company_id, vendor_user_id, sku);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS advertisement_item (
              id uuid PRIMARY KEY,
              advertisement_id uuid NOT NULL,
              sku varchar(64) NOT NULL,
              quantity numeric NOT NULL,
              sort_order int NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_item_ad_sku
              ON advertisement_item (advertisement_id, sku);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS advertisement_attribute (
              id uuid PRIMARY KEY,
              advertisement_id uuid NOT NULL,
              marketplace_code varchar(64) NOT NULL,
              field_name varchar(64) NOT NULL,
              field_value text NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_attribute_ad_mkt_field
              ON advertisement_attribute (advertisement_id, marketplace_code, field_name);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS marketplace_listing_field_definition (
              id uuid PRIMARY KEY,
              marketplace_code varchar(64) NOT NULL,
              field_key varchar(64) NOT NULL,
              label varchar(128) NOT NULL,
              value_kind varchar(16) NOT NULL,
              is_common boolean NOT NULL,
              required boolean NOT NULL,
              sort_order int NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_mkt_listing_field_code_key
              ON marketplace_listing_field_definition (marketplace_code, field_key);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE listing ADD COLUMN IF NOT EXISTS advertisement_id uuid NULL;
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_listing_advertisement_id
              ON listing (advertisement_id);
            """, ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_title text NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_price numeric NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_quantity numeric NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_status varchar(32) NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_permalink text NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS last_synced_at timestamptz NULL;", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE listing ADD COLUMN IF NOT EXISTS last_sync_json text NULL;", ct);
    }
}
