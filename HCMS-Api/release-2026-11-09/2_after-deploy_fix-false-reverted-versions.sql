-- Data fix for revisions that were wrongly numbered / wrongly marked "Reverted" by the old release.
--
-- What went wrong: a document that had EVER been sent back for rework (for example while it was first being created) had every later
-- Revision treated as another rework when it was submitted from Create/Update. So a revision proposed as 2.0 became:
--     2.0  "Reverted"  (archived, never really sent back)      <- false
--     2.1  the live draft / approved version                    <- should be 2.0
-- The code is fixed; this script repairs the rows the old code already produced. It touches only a pair of rows that has ALL of:
--   * an archived "Reverted" version R and the very next version N of the same document, N being R's number + one minor (2.0 -> 2.1),
--   * the document was already Effective before R was opened (so this is a Revision, not a first-time creation), and
--   * NO rework (send-back) recorded for the document between R being opened and N being created.
-- A genuine rework (someone really sent it back) always leaves that send-back record, so those pairs are left alone.
--
-- Repair: N takes R's number (2.1 -> 2.0) and R is soft-deleted (IsDeleted = TRUE, IsActive = FALSE; nothing is physically removed, and
-- the original rows are copied to DocumentVersions_fix_20261108 first). Several false reverts in a row are collapsed one pair at a time.
--
-- HOW TO USE
--   1. Run PART 1 (read-only) and look at what it lists.
--   2. Run PART 2 as it stands: it ends with ROLLBACK, so it only shows what it WOULD do.
--   3. If the list is what you expect, change the last line to COMMIT and run PART 2 again.
-- Safe to run more than once: once repaired, nothing matches any more.


-- ===================================== PART 1: what would be repaired (read-only) =====================================
SELECT d.DocumentNumber,
       r.Id          AS reverted_row_id,  r.Version AS shown_as_reverted,
       n.Id          AS live_row_id,      n.Version AS live_version_now,
       r.Version     AS live_version_after_fix,
       CASE WHEN n.VersionType = 2 THEN 'Effective' WHEN n.VersionType = 1 THEN 'pending / approved' ELSE 'archived' END AS live_row_state
FROM DocumentVersions r
JOIN Documents d ON d.Id = r.DocumentId AND d.CompanyId = r.CompanyId
JOIN LATERAL (
    SELECT x.* FROM DocumentVersions x
    WHERE x.CompanyId = r.CompanyId AND x.DocumentId = r.DocumentId AND x.Id > r.Id AND COALESCE(x.IsDeleted, FALSE) = FALSE
    ORDER BY x.Id LIMIT 1
) n ON TRUE
WHERE r.ArchiveReason = 'Reverted' AND r.VersionType = 3 AND COALESCE(r.IsDeleted, FALSE) = FALSE
  AND r.Version ~ '^[0-9]+\.[0-9]+$' AND n.Version ~ '^[0-9]+\.[0-9]+$'
  AND split_part(n.Version, '.', 1) = split_part(r.Version, '.', 1)
  AND split_part(n.Version, '.', 2)::int = split_part(r.Version, '.', 2)::int + 1
  AND EXISTS (SELECT 1 FROM DocumentStateHistory h JOIN DocumentStates s ON s.Id = h.ToStateId
              WHERE h.CompanyId = r.CompanyId AND h.DocumentId = r.DocumentId AND s.Code = 'EFFECTIVE' AND h.ChangedAt < r.CreatedAt)
  AND NOT EXISTS (SELECT 1 FROM DocumentStateHistory h
                  WHERE h.CompanyId = r.CompanyId AND h.DocumentId = r.DocumentId
                    AND h.ToStateId = 1 AND h.WorkflowExecutionId IS NOT NULL
                    AND h.ChangedAt >= r.CreatedAt AND h.ChangedAt <= n.CreatedAt)
ORDER BY d.DocumentNumber, r.Id;


-- ===================================== PART 2: the repair (ends with ROLLBACK) =====================================
BEGIN;

CREATE TABLE IF NOT EXISTS DocumentVersions_fix_20261108 AS SELECT * FROM DocumentVersions WHERE FALSE;

DO $$
DECLARE
    rec RECORD;
    fixed INT := 0;
BEGIN
    LOOP
        -- One pair per pass, the LAST one of a chain first, so 2.0 R / 2.1 R / 2.2 live collapses to a single 2.0.
        SELECT r.Id AS r_id, n.Id AS n_id, r.Version AS r_version, n.Version AS n_version, r.DocumentId, r.CompanyId
          INTO rec
        FROM DocumentVersions r
        JOIN LATERAL (
            SELECT x.* FROM DocumentVersions x
            WHERE x.CompanyId = r.CompanyId AND x.DocumentId = r.DocumentId AND x.Id > r.Id AND COALESCE(x.IsDeleted, FALSE) = FALSE
            ORDER BY x.Id LIMIT 1
        ) n ON TRUE
        WHERE r.ArchiveReason = 'Reverted' AND r.VersionType = 3 AND COALESCE(r.IsDeleted, FALSE) = FALSE
          AND r.Version ~ '^[0-9]+\.[0-9]+$' AND n.Version ~ '^[0-9]+\.[0-9]+$'
          AND split_part(n.Version, '.', 1) = split_part(r.Version, '.', 1)
          AND split_part(n.Version, '.', 2)::int = split_part(r.Version, '.', 2)::int + 1
          AND EXISTS (SELECT 1 FROM DocumentStateHistory h JOIN DocumentStates s ON s.Id = h.ToStateId
                      WHERE h.CompanyId = r.CompanyId AND h.DocumentId = r.DocumentId AND s.Code = 'EFFECTIVE' AND h.ChangedAt < r.CreatedAt)
          AND NOT EXISTS (SELECT 1 FROM DocumentStateHistory h
                          WHERE h.CompanyId = r.CompanyId AND h.DocumentId = r.DocumentId
                            AND h.ToStateId = 1 AND h.WorkflowExecutionId IS NOT NULL
                            AND h.ChangedAt >= r.CreatedAt AND h.ChangedAt <= n.CreatedAt)
        ORDER BY r.Id DESC
        LIMIT 1;

        EXIT WHEN NOT FOUND;

        INSERT INTO DocumentVersions_fix_20261108 SELECT * FROM DocumentVersions WHERE Id IN (rec.r_id, rec.n_id);

        UPDATE DocumentVersions SET IsDeleted = TRUE, IsActive = FALSE, LastModifiedAt = NOW(), LastModifiedBy = 'data-fix-20261108'
         WHERE Id = rec.r_id;
        UPDATE DocumentVersions SET Version = rec.r_version, LastModifiedAt = NOW(), LastModifiedBy = 'data-fix-20261108'
         WHERE Id = rec.n_id;

        fixed := fixed + 1;
        RAISE NOTICE 'Document %: version % (live) renumbered to %, false "Reverted" row % removed from the history',
            rec.DocumentId, rec.n_version, rec.r_version, rec.r_id;
    END LOOP;

    RAISE NOTICE 'Pairs repaired: %', fixed;
END $$;

-- What the affected documents' versions look like now (compare with PART 1):
SELECT d.DocumentNumber, v.Id, v.Version, v.VersionType, v.IsActive, v.ArchiveReason, COALESCE(v.IsDeleted, FALSE) AS removed
FROM DocumentVersions v
JOIN Documents d ON d.Id = v.DocumentId AND d.CompanyId = v.CompanyId
WHERE v.DocumentId IN (SELECT DocumentId FROM DocumentVersions_fix_20261108)
ORDER BY d.DocumentNumber, v.Id;

ROLLBACK;   -- <<< change to COMMIT once the output above is what you expect
