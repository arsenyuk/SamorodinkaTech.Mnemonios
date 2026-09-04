-- =============================================================================
-- ЕДИН (MPI) — Canonical Database Schema
-- =============================================================================
-- Database-First: this file is the single source of truth for the database schema.
-- No EF migrations. No EnsureCreated.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- persons — unified record of a physical person (only hashes + timestamps, no PII)
-- -----------------------------------------------------------------------------
CREATE TABLE persons (
    id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at               timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at               timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- -----------------------------------------------------------------------------
-- person_identification_keys — HMAC keys for deterministic matching
-- -----------------------------------------------------------------------------
CREATE TABLE person_identification_keys (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id             uuid NOT NULL REFERENCES persons(id) ON DELETE RESTRICT,
    key_type              varchar(50) NOT NULL,
    key_value             varchar(255) NOT NULL,
    normalization_version integer NOT NULL DEFAULT 1,
    created_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX ux_person_identification_keys_type_value_person
    ON person_identification_keys (key_type, key_value, person_id);
CREATE INDEX ix_person_identification_keys_person_id
    ON person_identification_keys (person_id);

-- -----------------------------------------------------------------------------
-- person_documents — DUL documents (type + hash only, no PII)
-- -----------------------------------------------------------------------------
CREATE TABLE person_documents (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id       uuid NOT NULL REFERENCES persons(id) ON DELETE RESTRICT,
    document_type   varchar(50) NOT NULL,
    document_hash   varchar(255) NOT NULL,
    created_at      timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX ix_person_documents_person_id ON person_documents (person_id);
CREATE UNIQUE INDEX ux_person_documents_person_id_hash
    ON person_documents (person_id, document_hash);

-- -----------------------------------------------------------------------------
-- person_external_ids — links to external information systems
-- -----------------------------------------------------------------------------
CREATE TABLE person_external_ids (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id             uuid NOT NULL REFERENCES persons(id) ON DELETE RESTRICT,
    source_system_id      varchar(100) NOT NULL,
    external_person_id    varchar(255) NOT NULL,
    external_person_type  varchar(255),
    organization_unit_key varchar(100),
    created_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX ux_person_external_ids_system_extid
    ON person_external_ids (source_system_id, external_person_id);
CREATE INDEX ix_person_external_ids_person_id
    ON person_external_ids (person_id);
CREATE INDEX ix_person_external_ids_source_system_id
    ON person_external_ids (source_system_id);

-- -----------------------------------------------------------------------------
-- person_defects — дефекты данных при идентификации
-- -----------------------------------------------------------------------------
CREATE TABLE person_defects (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id       uuid NOT NULL REFERENCES persons(id) ON DELETE RESTRICT,
    defect_type     varchar(50) NOT NULL,
    defect_message  varchar(500) NOT NULL,
    field_name      varchar(100),
    created_at      timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX ix_person_defects_person_id ON person_defects (person_id);
CREATE INDEX ix_person_defects_defect_type ON person_defects (defect_type);

-- -----------------------------------------------------------------------------
-- person_deferred_cessations — scheduled cessation of personal data processing
-- -----------------------------------------------------------------------------
CREATE TABLE person_deferred_cessations (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id               uuid NOT NULL REFERENCES persons(id) ON DELETE RESTRICT,
    source_system_id        varchar(100) NOT NULL,
    external_person_id      varchar(255) NOT NULL,
    organization_unit_key   varchar(100) NOT NULL,
    scheduled_deletion_date timestamp with time zone NOT NULL,
    status                  varchar(20) NOT NULL DEFAULT 'pending',
    created_at              timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX ux_person_deferred_cessations_system_extid
    ON person_deferred_cessations (source_system_id, external_person_id)
    WHERE status = 'pending';

CREATE INDEX ix_person_deferred_cessations_scheduled_date
    ON person_deferred_cessations (scheduled_deletion_date)
    WHERE status = 'pending';

CREATE INDEX ix_person_deferred_cessations_person_id
    ON person_deferred_cessations (person_id);

-- =============================================================================
-- Staging tables (ext_*) — raw incoming data for audit
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ext_persons — raw person resolution request data
-- -----------------------------------------------------------------------------
CREATE TABLE ext_persons (
    id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id                uuid REFERENCES persons(id) ON DELETE SET NULL,
    source_system_id         varchar(100) NOT NULL,
    external_person_id       varchar(255) NOT NULL,
    external_person_type     varchar(255),
    requested_person_id      uuid,
    key_inn                  varchar(255),
    key_snils                varchar(255),
    key_dul                  varchar(255),
    key_inn_fio              varchar(255),
    key_snils_fio            varchar(255),
    key_dul_fio              varchar(255),
    created_at               timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    processed_at             timestamp with time zone,
    source_ip                varchar(45)
);

CREATE INDEX ix_ext_persons_person_id ON ext_persons (person_id);
CREATE INDEX ix_ext_persons_source_system_id ON ext_persons (source_system_id);
CREATE INDEX ix_ext_persons_processing_status ON ext_persons (processing_status);
CREATE INDEX ix_ext_persons_created_at ON ext_persons (created_at);

-- -----------------------------------------------------------------------------
-- ext_person_defects — raw defect data from incoming request
-- -----------------------------------------------------------------------------
CREATE TABLE ext_person_defects (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ext_person_id       uuid NOT NULL REFERENCES ext_persons(id) ON DELETE RESTRICT,
    defect_type         varchar(50) NOT NULL,
    defect_message      varchar(500) NOT NULL,
    field_name          varchar(100),
    created_at          timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX ix_ext_person_defects_ext_person_id ON ext_person_defects (ext_person_id);

-- -----------------------------------------------------------------------------
-- ext_person_cessations — raw cessation request data
-- -----------------------------------------------------------------------------
CREATE TABLE ext_person_cessations (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id             uuid NOT NULL REFERENCES ext_persons(id) ON DELETE RESTRICT,
    source_system_id      varchar(100) NOT NULL,
    external_person_id    varchar(255) NOT NULL,
    organization_unit_key varchar(100) NOT NULL,
    processing_status     varchar(20) NOT NULL DEFAULT 'pending',
    created_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    processed_at          timestamp with time zone,
    source_ip             varchar(45)
);

CREATE INDEX ix_ext_person_cessations_person_id ON ext_person_cessations (person_id);
CREATE INDEX ix_ext_person_cessations_source_system_id ON ext_person_cessations (source_system_id);

-- -----------------------------------------------------------------------------
-- ext_person_deferred_cessations — raw deferred cessation request data
-- -----------------------------------------------------------------------------
CREATE TABLE ext_person_deferred_cessations (
    id                        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_id                 uuid NOT NULL REFERENCES ext_persons(id) ON DELETE RESTRICT,
    source_system_id          varchar(100) NOT NULL,
    external_person_id        varchar(255) NOT NULL,
    scheduled_deletion_date   timestamp with time zone NOT NULL,
    organization_unit_key     varchar(100) NOT NULL,
    processing_status         varchar(20) NOT NULL DEFAULT 'pending',
    created_at                timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    processed_at              timestamp with time zone,
    source_ip                 varchar(45)
);

CREATE INDEX ix_ext_person_deferred_cessations_person_id ON ext_person_deferred_cessations (person_id);
CREATE INDEX ix_ext_person_deferred_cessations_source_system_id ON ext_person_deferred_cessations (source_system_id);

-- -----------------------------------------------------------------------------
-- person_external_ids: add ext_person_id column
-- -----------------------------------------------------------------------------
ALTER TABLE person_external_ids
    ADD COLUMN ext_person_id uuid REFERENCES ext_persons(id) ON DELETE RESTRICT;

CREATE INDEX ix_person_external_ids_ext_person_id ON person_external_ids (ext_person_id);

-- -----------------------------------------------------------------------------
-- person_identification_keys: add organization_unit_key column
-- -----------------------------------------------------------------------------
ALTER TABLE person_identification_keys
    ADD COLUMN organization_unit_key varchar(100);

CREATE INDEX ix_person_identification_keys_organization_unit_key ON person_identification_keys (organization_unit_key);

-- -----------------------------------------------------------------------------
-- person_review_queue — очередь на ручную обработку стюардом (Ambiguous)
-- -----------------------------------------------------------------------------
CREATE TABLE person_review_queue (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    person_a_id       uuid NOT NULL REFERENCES persons(id) ON DELETE CASCADE,
    person_b_id       uuid NOT NULL REFERENCES persons(id) ON DELETE CASCADE,
    shared_key_type   varchar(50) NOT NULL,
    conflict_key_type varchar(50) NOT NULL,
    status            varchar(20) NOT NULL DEFAULT 'pending',
    created_at        timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    reviewed_at       timestamp with time zone
);

CREATE INDEX ix_person_review_queue_status ON person_review_queue (status) WHERE status = 'pending';

-- -----------------------------------------------------------------------------
-- url_masks — URL-маски для триад (ЮЛ, Система, Тип объекта)
-- -----------------------------------------------------------------------------
CREATE TABLE url_masks (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_unit_key varchar(100) NOT NULL DEFAULT '',
    source_system_id      varchar(100) NOT NULL,
    external_person_type  varchar(255) NOT NULL DEFAULT '',
    url_pattern           varchar(500) NOT NULL,
    created_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at            timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX ux_url_masks_triad
    ON url_masks (organization_unit_key, source_system_id, external_person_type);
