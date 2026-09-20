-- =============================================================================
-- ITX DOXERA / DMS -- database changes
-- Date    : 2026-09-20
-- Database: PostgreSQL (ATCO_DMS / DMS_ATCO_LIVE)
--
-- Run this on every environment (QA, Production). Every statement is
-- idempotent -- re-running it is safe.
--
-- Scope: SCHEMA + a one-time backfill of the new column from existing data.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1. Documents: record WHICH activity created the document
-- -----------------------------------------------------------------------------
-- Creation (DRT-0001), Revision (DRT-0002) and Obsoletion (DRT-0003) all produce
-- a row in Documents, and until now nothing on that row said which one it was.
-- Both Revision and Obsoletion set ParentDocumentId, so the two were
-- indistinguishable after creation.
--
-- That is why an Obsoletion was pushed into the training/authorization workflow:
-- HandlePostApprovalAsync could only look at the Document Type's training policy,
-- so obsoleting an SOP asked for training proof on a document being retired
-- (FSD 4.1.3 disables the Users section for Obsoletion precisely because no users
-- are assigned; neither Process Map has a training step).
--
-- Nullable: rows created before this change, and the legacy/bulk-import paths,
-- leave it NULL, which is read as ordinary Creation.

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS activitytypecode character varying(20);


-- -----------------------------------------------------------------------------
-- 2. Backfill from the request that produced each document
-- -----------------------------------------------------------------------------
-- Request-driven documents carry RequestId, and the request already records its
-- own type. This is exact, not inferred.

UPDATE documents d
   SET activitytypecode = dr.documentrequesttypecode
  FROM documentrequests dr
 WHERE dr.id = d.requestid
   AND d.activitytypecode IS NULL
   AND dr.documentrequesttypecode IS NOT NULL;


-- -----------------------------------------------------------------------------
-- 3. Backfill the direct (Create/Update Document) path from the parent's history
-- -----------------------------------------------------------------------------
-- A direct Revision/Obsoletion has no RequestId, but it did transition its parent
-- at creation time -- REVISED for a revision, OBSOLETE for an obsoletion -- and
-- that transition is stamped with the same timestamp as the child's creation.
-- Matching on the parent AND that timestamp keeps this from mis-reading an
-- unrelated later transition on the same parent.

UPDATE documents d
   SET activitytypecode = x.code
  FROM (
        SELECT c.id AS child_id,
               CASE s.code WHEN 'REVISED' THEN 'DRT-0002'
                           WHEN 'OBSOLETE' THEN 'DRT-0003' END AS code
          FROM documents c
          JOIN documentstatehistory h ON h.documentid = c.parentdocumentid
          JOIN documentstates s ON s.id = h.tostateid
         WHERE c.parentdocumentid IS NOT NULL
           AND c.requestid IS NULL
           AND s.code IN ('REVISED', 'OBSOLETE')
           AND h.changedat = c.createdat
       ) x
 WHERE x.child_id = d.id
   AND x.code IS NOT NULL
   AND d.activitytypecode IS NULL;


-- -----------------------------------------------------------------------------
-- 4. Everything else is an ordinary Creation
-- -----------------------------------------------------------------------------
-- Includes legacy uploads and bulk imports, which have no parent and no request.

UPDATE documents
   SET activitytypecode = 'DRT-0001'
 WHERE activitytypecode IS NULL
   AND parentdocumentid IS NULL;


-- -----------------------------------------------------------------------------
-- 5. WorkflowExecutions: record which activity the execution is running
-- -----------------------------------------------------------------------------
-- An Obsoletion no longer creates a Document of its own (BL-001 assigns a document
-- number only for Creation/Revision, and BL-003 reserves the "-A" suffix for
-- Annexures), so its approval workflow runs against the document being retired.
-- That document's own ActivityTypeCode describes how IT was created, not what this
-- execution is doing -- so the activity has to live on the execution.
--
-- Nullable: every existing execution predates this and is read as Creation/Revision,
-- which is what they were.

ALTER TABLE workflowexecutions
    ADD COLUMN IF NOT EXISTS activitytypecode character varying(20);


-- Backfill: executions that ran against a document which was itself an obsoletion
-- record (the old child-document model) were obsoletion workflows.
UPDATE workflowexecutions we
   SET activitytypecode = d.activitytypecode
  FROM documents d
 WHERE d.id = we.entityid
   AND d.companyid = we.companyid
   AND we.entitytype = 'Document'
   AND we.activitytypecode IS NULL
   AND d.activitytypecode IS NOT NULL;
