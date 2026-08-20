-- Live Postgres already exists; EnsureCreated will not add this table.
-- Also applied at API/NFE startup via SchemaPatches.EnsureAsync.

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

CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_company_id_created_at
  ON nfe_ingest_log (company_id, created_at);

CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_company_chave
  ON nfe_ingest_log (company_id, chave_acesso, created_at);

CREATE INDEX IF NOT EXISTS ix_nfe_ingest_log_run_id
  ON nfe_ingest_log (run_id);
