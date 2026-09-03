-- =============================================================================
-- ЕДИН (MPI) — Schema Reset
-- =============================================================================
-- Drops all MPI tables and re-applies the canonical schema.
-- WARNING: This destroys all data.
-- =============================================================================

DROP TABLE IF EXISTS ext_person_defects CASCADE;
DROP TABLE IF EXISTS ext_person_cessations CASCADE;
DROP TABLE IF EXISTS ext_person_deferred_cessations CASCADE;
DROP TABLE IF EXISTS ext_persons CASCADE;
DROP TABLE IF EXISTS person_defects CASCADE;
DROP TABLE IF EXISTS person_deferred_cessations CASCADE;
DROP TABLE IF EXISTS person_external_ids CASCADE;
DROP TABLE IF EXISTS person_documents CASCADE;
DROP TABLE IF EXISTS person_identification_keys CASCADE;
DROP TABLE IF EXISTS person_review_queue CASCADE;
DROP TABLE IF EXISTS persons CASCADE;

\i /docker-entrypoint-initdb.d/01_schema.sql
