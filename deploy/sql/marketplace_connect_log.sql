-- Live Postgres already exists; EnsureCreated will not add this table.
-- Also applied at API startup via SchemaPatches.EnsureAsync.

CREATE TABLE IF NOT EXISTS marketplace_connect_log (
  id uuid PRIMARY KEY,
  company_id uuid NOT NULL,
  run_id uuid NOT NULL,
  marketplace_code varchar(64) NOT NULL,
  action varchar(16) NOT NULL,
  step_code varchar(64) NOT NULL,
  "level" varchar(16) NOT NULL,
  user_message text NOT NULL,
  technical_json text NOT NULL,
  created_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_marketplace_connect_log_company_code_created
  ON marketplace_connect_log (company_id, marketplace_code, created_at);

CREATE INDEX IF NOT EXISTS ix_marketplace_connect_log_run_id
  ON marketplace_connect_log (run_id);
