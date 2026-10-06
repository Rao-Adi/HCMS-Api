-- Upload Old Documents (single upload): legacy documents must be Effective, not Draft.
--
-- DocumentComponent.CreateAsync used to write a DRAFT state and a VersionType = 1 (pending draft)
-- version for an uploaded legacy document, so it showed under Draft/Reverted Documents. It now
-- writes EFFECTIVE / VersionType = 2, like the bulk import. This repairs the ones already uploaded.
--
-- A legacy upload is recognisable because nothing else creates a document this way: no Request
-- (RequestId NULL), no ActivityTypeCode, and a single DRAFT history row that never entered a
-- workflow. The DRAFT row is rewritten in place (rather than adding a row) so the document keeps
-- the import marker the Uploaded Documents tab looks for.

BEGIN;

UPDATE DocumentVersions v
   SET VersionType = 2
 WHERE v.VersionType = 1
   AND v.IsActive = TRUE
   AND v.DocumentId IN (
        SELECT d.Id
          FROM Documents d
         WHERE d.RequestId IS NULL
           AND d.ActivityTypeCode IS NULL
           AND (SELECT COUNT(*) FROM DocumentStateHistory h WHERE h.DocumentId = d.Id) = 1
           AND EXISTS (SELECT 1 FROM DocumentStateHistory h
                         JOIN DocumentStates s ON s.Id = h.ToStateId
                        WHERE h.DocumentId = d.Id AND s.Code = 'DRAFT' AND h.WorkflowExecutionId IS NULL));

UPDATE DocumentStateHistory h
   SET ToStateId = (SELECT Id FROM DocumentStates WHERE Code = 'EFFECTIVE'),
       FromStateId = NULL,
       Comments = 'Legacy upload -- Effective per policy'
 WHERE h.WorkflowExecutionId IS NULL
   AND h.ToStateId = (SELECT Id FROM DocumentStates WHERE Code = 'DRAFT')
   AND h.DocumentId IN (
        SELECT d.Id
          FROM Documents d
         WHERE d.RequestId IS NULL
           AND d.ActivityTypeCode IS NULL
           AND (SELECT COUNT(*) FROM DocumentStateHistory x WHERE x.DocumentId = d.Id) = 1);

COMMIT;
