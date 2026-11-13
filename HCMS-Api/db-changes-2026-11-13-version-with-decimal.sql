-- 2026-11-13: versions that are a whole number ("4", "3", "1") get ".0" -- the same "1.0", "1.1" format the system writes itself.
-- Cause: documents brought in as already in force (Active Archival upload, Bulk Import) stored the version exactly as typed.
-- The code now adds ".0" at entry (NormalizeLegacyVersion in DocumentComponent.cs); this fixes the rows already saved.
-- Safe to run more than once: it only touches versions made of digits alone.

-- Look first (expected on UAT: documents 37 "3", 41 "1", 48 "4"):
-- SELECT dv.Id, dv.DocumentId, d.DocumentNumber, dv.Version FROM DocumentVersions dv JOIN Documents d ON d.Id = dv.DocumentId WHERE dv.Version ~ '^[0-9]+$';

UPDATE DocumentVersions
SET Version = Version || '.0'
WHERE Version ~ '^[0-9]+$';
