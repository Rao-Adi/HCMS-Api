-- DMS schema change, 2026-09-28
--
-- Ad-hoc approvers picked on the Create/Update Document screen were never persisted anywhere
-- until the document's WorkflowExecutionSteps row got created at Submit time. A "Save as Draft"
-- in between silently dropped the selection -- SaveDocumentAsDraftAsync never even read
-- input.AdHocApprovers, and there was no WorkflowExecution yet for it to attach to even if it
-- had. Confirmed live on IT-II-SOP-012 (document id 255): an ad-hoc approver picked at creation
-- time never made it into WorkflowExecutionSteps at all, so after the document was reverted,
-- Workflow Authorities only ever showed the policy-defined approver.
--
-- This table is the durable store that survives the Draft round-trip -- same role
-- DocumentUserTraining already plays for Training Users. DocumentComponent.SaveDocumentAsDraftAsync
-- and SubmitDocumentAsync both write to it now; GetCarriedOverAdHocApproverAsync (the Workflow
-- Authorities preview) and SubmitDocumentAsync's own carry-over-on-resubmit logic both read from
-- it first, falling back to the old WorkflowExecutionSteps scan only for documents that predate
-- this table.
--
-- Safe to re-run.
CREATE TABLE IF NOT EXISTS documentadhocapprovers (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isdeleted boolean NOT NULL DEFAULT false,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL,
    CONSTRAINT fk_daa_company FOREIGN KEY (companyid) REFERENCES companies(id),
    CONSTRAINT fk_daa_document FOREIGN KEY (documentid, companyid) REFERENCES documents(id, companyid)
);

CREATE INDEX IF NOT EXISTS idx_documentadhocapprovers_document ON documentadhocapprovers(documentid, companyid);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger WHERE tgname = 'trg_audit_documentadhocapprovers'
    ) THEN
        CREATE TRIGGER trg_audit_documentadhocapprovers
        AFTER INSERT OR DELETE OR UPDATE ON documentadhocapprovers
        FOR EACH ROW EXECUTE FUNCTION fn_dms_audit_log();
    END IF;
END $$;
