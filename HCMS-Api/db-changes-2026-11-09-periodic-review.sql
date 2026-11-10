-- Periodic review: an approver of an Effective document that is due for review (Next Review Date within 60 days, or past) can
-- "Re-approve" it without any change. No new version, no workflow: the review is recorded here and Documents.NextReviewDate moves on
-- by the document type's Review Years (DocumentReviewPolicies). Safe to run more than once.
CREATE TABLE IF NOT EXISTS DocumentReviews (
    Id                    SERIAL PRIMARY KEY,
    CompanyId             INT          NOT NULL,
    DocumentId            INT          NOT NULL,
    Version               VARCHAR(20)  NULL,          -- the version that was re-approved (it stays the version in force)
    ReviewedBy            VARCHAR(50)  NOT NULL,      -- employee code of the approver
    ReviewedAt            TIMESTAMP    NOT NULL DEFAULT NOW(),
    Remarks               TEXT         NULL,
    PreviousNextReviewDate DATE        NULL,
    NewNextReviewDate     DATE         NULL,
    ReviewPeriodYears     INT          NULL,
    IsDeleted             BOOLEAN      NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS ix_documentreviews_document ON DocumentReviews (CompanyId, DocumentId);
