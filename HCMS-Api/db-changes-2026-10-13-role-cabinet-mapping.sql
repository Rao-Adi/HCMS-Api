-- Role <-> DMS Cabinet mapping (client request, 2026-10-13).
--
-- Setups configurations > Users > "Role Cabinet Mapping". Maps a People Partners ROLE to a DMS Cabinet
-- scope: Division is required; Department / Sub-Department / Business Domain are optional (blank = the
-- whole level below, the same convention a workflow policy's cabinet scope uses).
--
-- Run once. Safe to re-run (IF NOT EXISTS).

CREATE TABLE IF NOT EXISTS RoleCabinetMappings
(
    Id                  serial PRIMARY KEY,
    CompanyId           integer               NOT NULL,
    -- People Partners role: tblsetupsdetail.sdlid (smsid = 189), the same id tblempjobprofile.roleid holds.
    RoleId              integer               NOT NULL,
    DivisionCode        character varying(100) NOT NULL,
    DepartmentCode      character varying(100) NULL,
    SubDepartmentCode   character varying(100) NULL,
    BusinessDomainCode  character varying(100) NULL,
    IsActive            boolean               NOT NULL DEFAULT TRUE,
    IsDeleted           boolean               NOT NULL DEFAULT FALSE,
    CreatedAt           timestamp             NOT NULL DEFAULT NOW(),
    CreatedBy           character varying(50) NULL,
    LastModifiedAt      timestamp             NOT NULL DEFAULT NOW(),
    LastModifiedBy      character varying(50) NULL
);

-- The same role cannot be mapped to the same cabinet scope twice (blank levels compare equal).
CREATE UNIQUE INDEX IF NOT EXISTS ux_rolecabinetmappings_scope
    ON RoleCabinetMappings (CompanyId, RoleId, DivisionCode,
                            COALESCE(DepartmentCode, ''), COALESCE(SubDepartmentCode, ''), COALESCE(BusinessDomainCode, ''))
    WHERE IsDeleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_rolecabinetmappings_role
    ON RoleCabinetMappings (CompanyId, RoleId) WHERE IsDeleted = FALSE;
