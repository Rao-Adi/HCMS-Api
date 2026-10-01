-- =============================================================================
-- ITX DOXERA / DMS -- database changes
-- Date    : 2026-09-23
-- Database: PostgreSQL
--
-- Run on every environment. Idempotent -- re-running it is safe.
-- Run BEFORE deploying the matching build.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1. Documents.DocumentNumber becomes optional
-- -----------------------------------------------------------------------------
-- BL-001: "The Document Number is system-generated ... The number is assigned upon
-- final approval of the Request for Creation/Revision."
--
-- A document created directly (no Request -- the Create/Update Document screen)
-- was being issued its number the moment it was drafted. That means a draft that
-- is never submitted, and a document that is rejected during approval, both hold
-- a number they were never entitled to, and both consume a value from the
-- per-cabinet sequence BL-002 defines. An audit reading the register finds gaps
-- it cannot account for.
--
-- The number is now assigned when the document completes its approval, so the
-- column has to accept NULL until then.
--
-- Postgres treats NULLs as distinct in a unique index, so any number of
-- unnumbered drafts coexist under uq_documents_company_documentnumber.
--
-- Existing rows are untouched: they already have numbers.

ALTER TABLE documents
    ALTER COLUMN documentnumber DROP NOT NULL;
