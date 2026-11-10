-- Approval Workflow Policy: "Head of Division / Department / Sub-Department" authority types.
-- A policy step of this kind names no person. HeadScope says which unit's head approves, and the head is worked out when the
-- request / document is submitted, from the cabinet it sits in (role 'Division Head' / 'Department Head' / 'Sub-Department Head'
-- plus a User Access Level on that unit). Existing rows are untouched (HeadScope stays NULL). Safe to run more than once.
ALTER TABLE WorkflowStepDefinitions ADD COLUMN IF NOT EXISTS HeadScope VARCHAR(20) NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_workflowstepdefinitions_headscope') THEN
        ALTER TABLE WorkflowStepDefinitions
            ADD CONSTRAINT chk_workflowstepdefinitions_headscope
            CHECK (HeadScope IS NULL OR HeadScope IN ('DIVISION', 'DEPARTMENT', 'SUBDEPARTMENT'));
    END IF;
END $$;

-- Which role names count as "Division Head" / "Department Head" / "Sub-Department Head".
-- Role names differ between companies and get renamed, so they are rules (SQL ILIKE patterns on the role name, setup 189), not code.
-- "{unit}" in a pattern stands for the name of the Division / Department / Sub-Department the document is filed under.
-- To change what counts as a head: UPDATE / INSERT / switch off (IsActive = FALSE) rows here -- no deployment needed.
CREATE TABLE IF NOT EXISTS WorkflowHeadRoleRules (
    Id              SERIAL PRIMARY KEY,
    CompanyId       INT          NOT NULL,
    HeadScope       VARCHAR(20)  NOT NULL CHECK (HeadScope IN ('DIVISION', 'DEPARTMENT', 'SUBDEPARTMENT')),
    RoleNamePattern VARCHAR(200) NOT NULL,
    IsActive        BOOLEAN      NOT NULL DEFAULT TRUE,
    CreatedAt       TIMESTAMP    NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_workflowheadrolerules UNIQUE (CompanyId, HeadScope, RoleNamePattern)
);

-- Starting rules, matching the role names found in the client's role master:
--   Division head      "Group Head of <Division>"   (e.g. Group Head of Supply Chain)      [and a role literally named Division Head]
--   Department head    "Head of <Department>..."    (e.g. Head of FG Warehouse)             [and a role literally named Department Head]
--   Sub-Department     "<Sub-Department> Manager"   (e.g. Claim Processing Manager)         [and a role literally named Sub-Department Head]
-- A rule that names the unit ({unit}) is enough on its own: the role already says which unit the person heads, so no User Access Level is needed. A generic rule
-- (the literal 'Division Head' / 'Department Head' / 'Sub-Department Head') says nothing about the unit, so for those the person must also have a User Access Level on it.
INSERT INTO WorkflowHeadRoleRules (CompanyId, HeadScope, RoleNamePattern)
SELECT c.Id, v.HeadScope, v.RoleNamePattern
FROM Companies c
CROSS JOIN (VALUES
    ('DIVISION',      'Group Head of {unit}%'),
    ('DIVISION',      'Division Head'),
    ('DEPARTMENT',    'Head of {unit}%'),
    ('DEPARTMENT',    'Department Head'),
    ('SUBDEPARTMENT', '{unit} Manager'),
    ('SUBDEPARTMENT', 'Sub-Department Head')
) AS v (HeadScope, RoleNamePattern)
ON CONFLICT (CompanyId, HeadScope, RoleNamePattern) DO NOTHING;
