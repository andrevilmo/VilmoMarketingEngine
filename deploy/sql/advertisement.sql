-- Live Postgres already exists; EnsureCreated will not add these tables.
-- Also applied at API/worker/NFE startup via SchemaPatches.EnsureAsync.

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

CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_company_vendor_sku
  ON advertisement (company_id, vendor_user_id, sku);

CREATE TABLE IF NOT EXISTS advertisement_item (
  id uuid PRIMARY KEY,
  advertisement_id uuid NOT NULL,
  sku varchar(64) NOT NULL,
  quantity numeric NOT NULL,
  sort_order int NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_item_ad_sku
  ON advertisement_item (advertisement_id, sku);

CREATE TABLE IF NOT EXISTS advertisement_attribute (
  id uuid PRIMARY KEY,
  advertisement_id uuid NOT NULL,
  marketplace_code varchar(64) NOT NULL,
  field_name varchar(64) NOT NULL,
  field_value text NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_advertisement_attribute_ad_mkt_field
  ON advertisement_attribute (advertisement_id, marketplace_code, field_name);

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

CREATE UNIQUE INDEX IF NOT EXISTS ix_mkt_listing_field_code_key
  ON marketplace_listing_field_definition (marketplace_code, field_key);

ALTER TABLE listing ADD COLUMN IF NOT EXISTS advertisement_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_listing_advertisement_id
  ON listing (advertisement_id);

ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_title text NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_price numeric NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_quantity numeric NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_status varchar(32) NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS remote_permalink text NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS last_synced_at timestamptz NULL;
ALTER TABLE listing ADD COLUMN IF NOT EXISTS last_sync_json text NULL;
