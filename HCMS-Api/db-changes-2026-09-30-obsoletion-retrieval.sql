-- DMS schema change, 2026-09-30 (Obsoletion distribution retrieval tracking)
--
-- Client requirement (Ayesha Naz): before submitting an Obsoletion for approval, the user must
-- go through the document's Distribution List and confirm each entry's copy has actually been
-- retrieved -- digitally disabled, or the physical copy collected -- and the approver must be
-- able to see that retrieval status when reviewing the Obsoletion. No such tracking existed
-- anywhere in the schema before this (confirmed: the only related thing was a one-way
-- notification email to Document Control Associates).
--
-- Added to DocumentRoleDistributions specifically (not DocumentUserDistributions): that table
-- already carries DistributionType (Physical/Digital), which the retrieval ACTION depends on,
-- and it is literally "the Distribution List" grid the client's workflow refers to.
--
-- Safe to re-run.
ALTER TABLE documentroledistributions
    ADD COLUMN IF NOT EXISTS isretrieved boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS retrievedat timestamp without time zone,
    ADD COLUMN IF NOT EXISTS retrievedby character varying(100);
