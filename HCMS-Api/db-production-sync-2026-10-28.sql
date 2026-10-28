-- ============================================================================
-- ATCO DMS -- production database synchronisation script
-- Generated 2026-10-28 from the UAT database (PostgreSQL 17).
--
-- ONE file. Run it against the production DMS database (an empty one, or one that already holds data):
--   psql -h <host> -U <user> -d <database> -f db-production-sync-2026-10-28.sql
-- (or open it in pgAdmin's Query Tool and run it). It can be run again and again.
--
-- What it does, in this order:
--   1. creates every table that is missing (67 tables in all);
--   2. adds every column that is missing to the tables that already exist, and puts back a missing column default;
--      a column of a different data type is made wider when that cannot lose data (shorter text to longer,
--      smallint to integer to bigint); any other difference is only REPORTED (a NOTICE), never changed;
--   3. creates missing sequences, identity columns, primary / unique keys, indexes and foreign keys;
--   4. DROPS and CREATES AGAIN every view (9), function and procedure (4) and trigger (42) with the
--      definition that is in UAT, so one that was deleted or is out of date is replaced by the current one;
--   5. loads the fixed reference data the application depends on (document states, request types, distribution types, ...)
--      and the company ATCO (id 1), where they are missing;
--   6. checks that everything exists and stops with an error (undoing the whole run) if something is still missing.
--
-- It NEVER deletes a row, drops a table or drops a column. Only views, functions, procedures and triggers are dropped, and each is
-- created again in the same run. Existing rows are changed in just three ways, all needed by the new build, and only where a row
-- is still in the old form: the status text of OBSOLETE becomes "Obsoleted"; old ad-hoc approver steps are flagged ad-hoc; and
-- Division / Department / Sub Department of the Document Users and Distribution List rows are converted from cabinet codes
-- (DIV-0001 ...) to People Partners ids (the four tables are copied to *_bak_20261016 first).
-- The whole script is ONE transaction: if anything fails, nothing is changed.
-- Read the NOTICE lines it prints: they say what was added and what needs a look.
-- Run it as the owner of the DMS tables (normally the postgres user), ideally when nobody is using the application: for a few
-- seconds the views are not there.
-- ============================================================================

BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL check_function_bodies = false;
SET LOCAL client_min_messages = notice;
SET LOCAL statement_timeout = 0;
SET LOCAL lock_timeout = 0;


-- ============================================================================
-- 1. Sequences and tables that are missing
-- ============================================================================

CREATE SEQUENCE IF NOT EXISTS public.businessdomains_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.cabinetstructuretabsconfig_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.companies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.controltypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.departments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.designations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.distributiontypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.division_code_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.divisions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.document_request_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.document_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentattributevalues_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentevents_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentrequesthistory_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentrequesttypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentreviewpolicies_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documents_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentstatehistory_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentstates_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentstatetransitions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documenttrainingauthorizations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documenttypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.documentversions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.emailoutbox_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.orgunits_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.policyassignments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.rolecabinetmappings_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.roles_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.subdepartments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.uncontrolleddocumenthistory_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.uncontrolleddocuments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.useraccesslevels_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.workflowexecutions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.workflowpolicies_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.workflowpolicyversions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE SEQUENCE IF NOT EXISTS public.workflowsteptypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE TABLE IF NOT EXISTS public.alert_configs (
    id integer NOT NULL,
    company_id integer NOT NULL,
    name character varying(255) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    pending_state character varying(50),
    pending_days integer,
    review_within_days integer,
    inactive_days integer,
    status_changed_to character varying(50),
    lookback_days integer,
    frequency character varying(20) NOT NULL,
    send_at_time time without time zone NOT NULL,
    delivery_format character varying(30) NOT NULL,
    email_subject character varying(500) NOT NULL,
    email_body text NOT NULL,
    created_by character varying(20) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.alert_document_scopes (
    id integer NOT NULL,
    alert_config_id integer NOT NULL,
    company_id integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30)
);

CREATE TABLE IF NOT EXISTS public.alert_recipients (
    id integer NOT NULL,
    alert_config_id integer NOT NULL,
    email_address character varying(256) NOT NULL,
    recipient_type character varying(10) NOT NULL,
    is_self boolean DEFAULT false NOT NULL
);

CREATE TABLE IF NOT EXISTS public.attributemandatoryscopes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentattributeid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    departmentcode character varying(20) NOT NULL,
    subdepartmentcode character varying(30) NOT NULL,
    businessdomaincode character varying(30) NOT NULL,
    ismandatory boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.auditlogs (
    companyid integer NOT NULL,
    id integer NOT NULL,
    employeecode character varying(10) NOT NULL,
    action character varying(100) NOT NULL,
    entitytype character varying(50) NOT NULL,
    entityid integer NOT NULL,
    oldvalues jsonb,
    newvalues jsonb,
    "timestamp" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ipaddress character varying(45)
);

CREATE TABLE IF NOT EXISTS public.businessdomains (
    id integer NOT NULL,
    companyid integer NOT NULL,
    subdepartmentcode character varying(30) NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.cabinetstructuretabsconfig (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.companies (
    id bigint NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(200) NOT NULL,
    status character varying(50) DEFAULT 'Active'::character varying,
    subscriptionplan character varying(50),
    storagequotagb integer,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS public.controltypes (
    id integer NOT NULL,
    name character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.departments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(20) NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.designations (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.distributiontypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.divisions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentadhocapprovers (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentattributes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    controllabel character varying(100) NOT NULL,
    controltypeid integer NOT NULL,
    listvalues character varying(1000),
    ismandatory boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentattributevalues (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    documentattributeid integer NOT NULL,
    valuetext text,
    valuenumber numeric,
    valuedate date,
    valueboolean boolean,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentevents (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    documentid bigint NOT NULL,
    eventtype character varying(100) NOT NULL,
    performedby bigint NOT NULL,
    eventtimestamp timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    metadata jsonb
);

CREATE TABLE IF NOT EXISTS public.documentrequesthistory (
    id integer NOT NULL,
    companyid integer NOT NULL,
    requestid integer NOT NULL,
    fromstateid integer,
    tostateid integer NOT NULL,
    changedby character varying(100) NOT NULL,
    changedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    workflowexecutionid integer,
    comments text
);

CREATE TABLE IF NOT EXISTS public.documentrequestroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentrequestid integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    roleid integer,
    distributiontypeid integer NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentrequests (
    id integer NOT NULL,
    companyid integer NOT NULL,
    requestnumber character varying(50) NOT NULL,
    documentrequesttypecode character varying(10) NOT NULL,
    documentid integer,
    documenttypecode character varying(10) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documentname character varying(500) NOT NULL,
    justification character varying(2000) NOT NULL,
    proposedcontent text,
    iscontentfinalized boolean DEFAULT false,
    draftcontentlastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    draftcontentlastmodifiedby character varying(100),
    submittedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    submittedby character varying(100),
    status integer NOT NULL,
    rowversion character varying(10),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    draftfileurl character varying(500),
    parentdocumentid integer,
    parentrequestid integer
);

CREATE TABLE IF NOT EXISTS public.documentrequesttypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(500),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentrequestuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentrequestid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(120) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(120) NOT NULL,
    roleid integer,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30)
);

CREATE TABLE IF NOT EXISTS public.documentreviewpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(50) NOT NULL,
    reviewperiodyears integer DEFAULT 1 NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);

CREATE TABLE IF NOT EXISTS public.documentrevisiondistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentrevisionroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    divisioncode character varying,
    departmentcode character varying,
    subdepartmentcode character varying,
    businessdomaincode character varying,
    roleid integer NOT NULL,
    distributiontype integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL,
    isretrieved boolean DEFAULT false NOT NULL,
    retrievedat timestamp without time zone,
    retrievedby character varying
);

CREATE TABLE IF NOT EXISTS public.documentrevisionuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL,
    roleid integer,
    divisioncode character varying,
    departmentcode character varying,
    subdepartmentcode character varying,
    businessdomaincode character varying
);

CREATE TABLE IF NOT EXISTS public.documentroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    roleid integer NOT NULL,
    distributiontype integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    isretrieved boolean DEFAULT false NOT NULL,
    retrievedat timestamp without time zone,
    retrievedby character varying(100)
);

CREATE TABLE IF NOT EXISTS public.documents (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentnumber character varying(50),
    requestid integer,
    documenttypecode character varying(10) NOT NULL,
    parentdocumentid integer,
    title character varying(500) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    nextreviewdate date,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    documenturl character varying(500),
    justification character varying(1000),
    activitytypecode character varying(20)
);

CREATE TABLE IF NOT EXISTS public.documentstatehistory (
    companyid integer NOT NULL,
    id bigint NOT NULL,
    documentid bigint NOT NULL,
    fromstateid integer,
    tostateid integer NOT NULL,
    changedby character varying(100) NOT NULL,
    changedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    workflowexecutionid bigint,
    comments text
);

CREATE TABLE IF NOT EXISTS public.documentstates (
    id integer NOT NULL,
    code character varying(50) NOT NULL,
    name character varying(100) NOT NULL,
    isterminal boolean DEFAULT false
);

CREATE TABLE IF NOT EXISTS public.documentstatetransitions (
    id integer NOT NULL,
    fromstateid integer NOT NULL,
    tostateid integer NOT NULL,
    requiresworkflow boolean DEFAULT false,
    requirestraining boolean DEFAULT false,
    requiresauthorization boolean DEFAULT false
);

CREATE TABLE IF NOT EXISTS public.documenttraining (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    trainingmode integer NOT NULL,
    trainingstatus integer NOT NULL,
    trainingproofurl character varying(500),
    assessmentscore numeric(5,2),
    validationstatus integer DEFAULT 1 NOT NULL,
    readyforauthorization boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documenttrainingauthorizations (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(50) NOT NULL,
    authorizationrequired boolean DEFAULT false NOT NULL,
    authorizinguserid character varying(50),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);

CREATE TABLE IF NOT EXISTS public.documenttypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(150),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    roleid integer,
    divisioncode character varying(50),
    departmentcode character varying(50),
    subdepartmentcode character varying(50),
    businessdomaincode character varying(50)
);

CREATE TABLE IF NOT EXISTS public.documentusertraining (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    trainingmode integer NOT NULL,
    trainingstatus integer NOT NULL,
    trainingproofurl character varying(500),
    assessmentscore numeric(5,2),
    validationstatus integer DEFAULT 1 NOT NULL,
    readyforauthorization boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documentversions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    version character varying(10) NOT NULL,
    documenturl character varying(1500),
    versiontype integer NOT NULL,
    content text,
    changedescription character varying(1000),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    createdbyid integer,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    archivereason character varying(30)
);

CREATE TABLE IF NOT EXISTS public.emailoutbox (
    id bigint NOT NULL,
    companyid integer NOT NULL,
    recipientemployeecode character varying(50),
    recipientemail character varying(320) NOT NULL,
    subject character varying(500) NOT NULL,
    body text NOT NULL,
    notificationtype integer,
    relatedentityid integer,
    status character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    isdelivered boolean DEFAULT false NOT NULL,
    attemptcount integer DEFAULT 0 NOT NULL,
    lasterror text,
    createdat timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    nextattemptat timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    lastattemptat timestamp without time zone,
    deliveredat timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.employeedraftobservation (
    id integer NOT NULL,
    employeecode character varying(50) NOT NULL,
    companyid integer NOT NULL,
    observationtext text NOT NULL,
    documentid integer,
    requestid integer
);

CREATE TABLE IF NOT EXISTS public.esignatures (
    id integer NOT NULL,
    companyid integer NOT NULL,
    userid character varying(10) NOT NULL,
    signatureurl character varying(1000) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.notifications (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(100) NOT NULL,
    title character varying(200) NOT NULL,
    message character varying(1000) NOT NULL,
    notificationtype integer NOT NULL,
    relatedentitytype character varying(50),
    relatedentityid integer,
    redirectionurl character varying(200),
    isread boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.orgunits (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    parentid bigint,
    code character varying(20) NOT NULL,
    name character varying(200) NOT NULL,
    type character varying(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.policyassignments (
    id bigint NOT NULL,
    policytype character varying(50),
    policyversionid bigint NOT NULL,
    orgunitid bigint,
    documenttypecode character varying(10),
    isactive boolean DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.responsibilitytransfers (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeefrom character varying(10) NOT NULL,
    employeeto character varying(10) NOT NULL,
    reasonfortransfer character varying(10) NOT NULL,
    effectivedatefrom date NOT NULL,
    effectivedateto date,
    permanenttransfer boolean DEFAULT false,
    attachment character varying(1000),
    remarks character varying(1000) NOT NULL,
    status integer DEFAULT 1 NOT NULL,
    approverid character varying(100) NOT NULL,
    observation character varying(1000),
    actiondate timestamp without time zone,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.rolecabinetmappings (
    id integer NOT NULL,
    companyid integer NOT NULL,
    roleid integer NOT NULL,
    divisioncode character varying(100) NOT NULL,
    departmentcode character varying(100),
    subdepartmentcode character varying(100),
    businessdomaincode character varying(100),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);

CREATE TABLE IF NOT EXISTS public.roles (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(200),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.subdepartments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    departmentcode character varying(20) NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.tbldeptstrdetail (
    dptdid integer NOT NULL,
    dptmid integer,
    dptid integer,
    budgeted real,
    approved real,
    companyid integer,
    userid character varying,
    applicationid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    userempcode character varying,
    entterminal character varying,
    entterminalip character varying,
    entoperation character varying,
    entdate timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.tbldeptstrmaster (
    dptmid integer NOT NULL,
    mdptid integer,
    companyid integer,
    budgeted real,
    approved real,
    divid integer,
    userid character varying,
    applicationid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    userempcode character varying,
    entterminal character varying,
    entterminalip character varying,
    entoperation character varying,
    entdate timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.tblempjobprofile (
    jobprofileid integer NOT NULL,
    empid integer NOT NULL,
    jobcode character(7) NOT NULL,
    jobcdate timestamp without time zone NOT NULL,
    jobtitle character varying(100),
    mdptid integer NOT NULL,
    dptid integer NOT NULL,
    jobid integer NOT NULL,
    baseid integer NOT NULL,
    typid integer NOT NULL,
    reportingrelationship character varying(225),
    keyrelationshipint character varying(100),
    keyrelationshipext character varying(100),
    academicqualifications text,
    workexperience text,
    jobsummary text,
    keyperformanceindicators text,
    responsibilities text,
    accountabilities text,
    eoworkcondition text,
    empdate timestamp without time zone,
    mgrempid integer,
    mgrdate timestamp without time zone,
    hrempid integer,
    hrdate timestamp without time zone,
    companyid integer NOT NULL,
    appdoc bytea,
    divid integer,
    directrptto boolean,
    dcompanyid integer,
    dgradeid integer,
    ddsgid integer,
    ddivid integer,
    dmdptid integer,
    ddptid integer,
    dbaseid integer,
    indirectrptto boolean,
    indcompanyid integer,
    indgradeid integer,
    inddsgid integer,
    inddivid integer,
    indmdptid integer,
    inddptid integer,
    indbaseid integer,
    pagerange character varying(50),
    prefgender integer,
    eyears character varying(50),
    dsgid integer,
    rexpyears character varying(50),
    chkqual boolean,
    chkcert boolean,
    chktrain boolean,
    isdescripancyincompetency boolean,
    isdescripancyinkpi boolean,
    jdid integer,
    active boolean,
    pagerangeto integer,
    eyearsto integer,
    rexpyearsto integer,
    incpackage character varying(500),
    otherbenifit text,
    salaryremarks text,
    visastatus integer,
    salaryamountfrom numeric(9,2),
    salaryamountto numeric(9,2),
    salarycurrencyid integer,
    prefreligion integer,
    assigneddate timestamp without time zone,
    userid character varying(250),
    applicationid character varying(10),
    formid character varying(250),
    userempid integer,
    userempname character varying(250),
    userempcode character varying(10),
    entterminal character varying(250),
    entterminalip character varying(250),
    entoperation character varying(10),
    entdate timestamp without time zone,
    emointlprofiling integer,
    roleid integer,
    droleid integer,
    indroleid integer,
    received boolean,
    jdchangeversionno character varying(500),
    acknowledgeon timestamp without time zone,
    acknowledgebyuserid character varying(500),
    acknowledgeterminal character varying(500),
    acknowledgebyempid integer,
    changeresponsibilitydetail text
);

CREATE TABLE IF NOT EXISTS public.tblemployee (
    empid integer NOT NULL,
    empcode character varying NOT NULL,
    companyid integer,
    emprefno character varying,
    firstname character varying NOT NULL,
    midname character varying,
    lastname character varying,
    titleid integer,
    initial character varying,
    fname character varying,
    gndid integer,
    nic character varying,
    nicnew character varying,
    ntn character varying,
    dateofbirth timestamp without time zone,
    address character varying,
    ctyid integer,
    phone character varying,
    phonecntcode character varying,
    phonectycode character varying,
    phonenumber character varying,
    phone2 character varying,
    phone2cntcode character varying,
    phone2ctycode character varying,
    phone2number character varying,
    mobile character varying,
    mobilecode character varying,
    mobilenumber character varying,
    email character varying,
    rlgid integer,
    mrtid integer,
    bldid integer,
    nationalityid integer,
    qlfid integer,
    emgperson character varying,
    emgphone character varying,
    emgaddress character varying,
    mdptid integer,
    dptid integer NOT NULL,
    dsgid integer,
    cmpid integer,
    brnid integer,
    typid integer,
    datejoin timestamp without time zone,
    dateconfirm timestamp without time zone,
    cduedate timestamp without time zone,
    extdate timestamp without time zone,
    jobdes text,
    active integer,
    jobid character varying,
    notepad character varying,
    reportto integer,
    dotted integer,
    medical boolean,
    gshift boolean,
    gross numeric NOT NULL,
    pdate timestamp without time zone,
    lastincdate timestamp without time zone,
    height character varying,
    weight character varying,
    eyecolor character varying,
    glass character varying,
    medicalhistory character varying,
    disabilities smallint,
    disreason character varying,
    pgid integer,
    relname character varying,
    relworking character varying,
    relationshipid integer,
    relpositionid integer,
    sectid integer,
    areaid integer,
    mtid integer,
    divid integer,
    uid character varying,
    pwd character varying,
    vdesigid integer,
    vdesigid2 integer,
    baseid integer,
    regionid integer,
    mteamid integer,
    teamid integer,
    expyr real,
    editby integer,
    confirmby integer,
    hold smallint,
    isdirty smallint,
    dispid integer,
    identification character varying,
    sdwid smallint,
    domid integer,
    insurerid integer,
    grpinsrnum character varying,
    grpinsramt numeric,
    suminsured numeric,
    assetstatus integer,
    lreason character varying,
    shift character(1),
    autopresent boolean,
    pgids character varying,
    countryid integer,
    offerdate timestamp without time zone,
    appid integer,
    vaccinationhistory character varying,
    contractexpiredate timestamp without time zone,
    proratedleave smallint,
    passportno character varying,
    fathernic character varying,
    dgid integer,
    dutyid integer,
    payrollstatus integer,
    slicamount numeric,
    totalslicamount numeric,
    opdid integer,
    email2 character varying,
    emgmobile character varying,
    mdptidcurrent integer,
    mdptidold integer,
    dptidcurrent integer,
    dptidold integer,
    dsgidcurrent integer,
    dsgidold integer,
    jobidcurrent integer,
    pgidcurrent integer,
    reporttocurrent integer,
    cnicexpirydate timestamp without time zone,
    internexpirydate timestamp without time zone,
    hiringchecklistprocess boolean,
    oldempid integer,
    leavingdate timestamp without time zone,
    intercompanytransfer boolean,
    sourcecompanyid integer,
    targetcompanyid integer,
    originalcompanyid integer,
    sourceempid integer,
    targetempid integer,
    originalempid integer,
    intercompanyinduction boolean,
    lastworkingdate timestamp without time zone,
    reportingdate timestamp without time zone,
    basecntid integer,
    dispcntid integer,
    reporttocompany integer,
    relid integer,
    dottedreporttocompany integer,
    passportexpirydate timestamp without time zone,
    userid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    entterminal character varying,
    entterminalip character varying,
    userempcode character varying,
    visanumer character varying,
    visaexpirydate timestamp without time zone,
    visanumber character varying,
    nextincdate timestamp without time zone,
    matchedwithblacklist integer,
    processmanualmap integer,
    duplicatedrecordid integer,
    duplicatedmapempid integer,
    idcardremarks character varying,
    familycardno character varying,
    iqamano character varying,
    iqamaexpiryhijri timestamp without time zone,
    iqamaexpirygregorian timestamp without time zone,
    currspnsname character varying,
    spnstransferable integer,
    spnstype integer,
    spnscountry integer,
    spnscity integer,
    spnscontactdetails character varying,
    spnsnatureofbusiness character varying,
    iqamaprofession integer,
    spnsexpiryhijri timestamp without time zone,
    spnsexpirygregorian timestamp without time zone,
    firstnamearabic character varying,
    midnamearabic character varying,
    lastnamearabic character varying,
    titleidarabic integer,
    validdrivinglicenseksa integer,
    hstofpersecution character varying,
    hstofpenalties character varying,
    pendingcases character varying,
    spnscategory integer,
    noofspnschangedofvisa integer,
    empcategoryid integer,
    autopresentfromdate timestamp without time zone,
    autopresenttodate timestamp without time zone,
    transactionsource character varying,
    salarychangedstatus integer,
    reviewtransactionid integer,
    hrseries character varying,
    isleaveallocated boolean,
    isemployeesalary boolean,
    ishiringchecklistfinalized boolean,
    isemployeeprofileextended boolean,
    isemployeeexpenseentitlement boolean,
    isemployeejd boolean,
    isapprovalforpayroll boolean,
    isuserid boolean,
    preferredculture character varying,
    domcntid integer,
    entdate timestamp without time zone,
    applicationid character varying,
    retirementdate timestamp without time zone,
    salaryreviewchangedstatus integer,
    "timestamp" timestamp without time zone NOT NULL,
    jobidold integer,
    pgidold integer,
    reporttoold integer,
    flexishift boolean,
    flexitype integer,
    requiredhours numeric,
    editbyuser character varying,
    secondment boolean,
    photopath character varying,
    companyshortname character varying,
    acids character varying,
    drivinglicenseno character varying,
    drivinglicenseexpirydate timestamp without time zone,
    isspouseemployed boolean,
    isanyotherincomesource boolean,
    isanyphysicaldisability boolean,
    otherincomesourcedetails character varying,
    physicaldisabilitydetails character varying,
    residentialstatusid integer,
    fatherhusbandphone character varying,
    fatherhusbandoccupation character varying,
    isownconveyance boolean,
    conveyancetype integer,
    conveyancemake character varying,
    conveyancemodel character varying,
    conveyanceyear character varying,
    conveyanceregisterationno character varying,
    phoneextension character varying,
    sittinglocation character varying
);

CREATE TABLE IF NOT EXISTS public.tblsetupsdetail (
    sdlid integer NOT NULL,
    code character varying(100),
    smsid integer,
    name character varying(255),
    lastupdate timestamp without time zone,
    companyid integer,
    userempid integer,
    entoperation character varying(100),
    entdate timestamp without time zone,
    userempcode character varying(100),
    entterminalip character varying(100),
    entterminal character varying(100),
    userempname character varying(255),
    formid character varying(100),
    userid character varying(100),
    applicationid character varying(100),
    chngtimestamp bigint,
    inactive boolean
);

CREATE TABLE IF NOT EXISTS public.templates (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    templatename character varying(200) NOT NULL,
    templatefileurl character varying(500),
    templatetype integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    isdefault boolean DEFAULT true NOT NULL,
    templatecontent text,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.trainingpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    trainingrequired boolean DEFAULT false NOT NULL,
    minimumscore integer,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.transferscopepolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    reportingtolevel integer NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.transferworkflowpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    approvalroleid character varying(10) NOT NULL,
    approvaluserid character varying(10) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    approverempcode character varying(10)
);

CREATE TABLE IF NOT EXISTS public.uncontrolleddocumenthistory (
    id integer NOT NULL,
    companyid integer NOT NULL,
    uncontrolleddocumentid integer NOT NULL,
    previousdocumenturl character varying(500),
    newdocumenturl character varying(500) NOT NULL,
    previousreviewdate date,
    newreviewdate date NOT NULL,
    reviewedby character varying(100) NOT NULL,
    reviewedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.uncontrolleddocuments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentname character varying(500) NOT NULL,
    documenturl character varying(500) NOT NULL,
    reviewdate date NOT NULL,
    reviewauthorityempcode character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.useraccesslevels (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documenttypecode character varying(10) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.userroles (
    companyid bigint NOT NULL,
    userid character varying(100) NOT NULL,
    roleid bigint NOT NULL,
    assignedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    assignedby character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.users (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    employeename character varying(100) NOT NULL,
    email character varying(100) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    designationcode character varying(30),
    reportingto character varying(255) NOT NULL,
    grade character varying(20) NOT NULL,
    dateofjoining timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.workflowpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(200) NOT NULL,
    entitytype character varying(50) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documenttypecode character varying(10),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.workflowpolicyversions (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    workflowpolicyid integer NOT NULL,
    versionnumber integer NOT NULL,
    isactive boolean DEFAULT true,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.workflowstepdefinitions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    workflowpolicyversionid integer NOT NULL,
    steporder integer NOT NULL,
    stepgroup integer DEFAULT 1,
    steptype character varying(50) NOT NULL,
    roleid integer,
    designationid integer,
    userid character varying(100),
    requiresallapprovals boolean DEFAULT true,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    canaddadhocapprover boolean DEFAULT false NOT NULL
);

CREATE TABLE IF NOT EXISTS public.workflowexecutions (
    companyid integer NOT NULL,
    id integer NOT NULL,
    workflowpolicyversionid integer NOT NULL,
    entitytype character varying(50) NOT NULL,
    entityid integer NOT NULL,
    status character varying(50) NOT NULL,
    startedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    completedat timestamp without time zone,
    startedby character varying(100) NOT NULL,
    activitytypecode character varying(20)
);

CREATE TABLE IF NOT EXISTS public.workflowexecutionsteps (
    id integer NOT NULL,
    companyid integer NOT NULL,
    workflowexecutionid integer NOT NULL,
    stepdefinitionid integer NOT NULL,
    assigneduserid character varying(100),
    assignedroleid integer,
    assigneddesignationid integer,
    decision character varying(50),
    observation text NOT NULL,
    actionat timestamp without time zone,
    isactive boolean DEFAULT false,
    steporder integer,
    isadhoc boolean DEFAULT false NOT NULL,
    addedby character varying(50),
    canaddadhocapprover boolean DEFAULT false NOT NULL
);

CREATE TABLE IF NOT EXISTS public.workflowsteptypes (
    id integer NOT NULL,
    code character varying(50),
    name character varying(100)
);


-- ============================================================================
-- 2. Triggers, views, functions and procedures are dropped here and created again below (so that a column can be changed)
-- ============================================================================

DO $sync$
DECLARE r record;
BEGIN
  -- triggers first (they use the audit function), then views from the last to the first, then the functions
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_alert_configs ON public.alert_configs';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_attributemandatoryscopes ON public.attributemandatoryscopes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_businessdomains ON public.businessdomains';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_cabinetstructuretabsconfig ON public.cabinetstructuretabsconfig';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_companies ON public.companies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_controltypes ON public.controltypes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_departments ON public.departments';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_designations ON public.designations';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_distributiontypes ON public.distributiontypes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_divisions ON public.divisions';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentadhocapprovers ON public.documentadhocapprovers';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentattributes ON public.documentattributes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentattributevalues ON public.documentattributevalues';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentrequesttypes ON public.documentrequesttypes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentreviewpolicies ON public.documentreviewpolicies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentstates ON public.documentstates';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentstatetransitions ON public.documentstatetransitions';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documenttraining ON public.documenttraining';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documenttrainingauthorizations ON public.documenttrainingauthorizations';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documenttypes ON public.documenttypes';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentusertraining ON public.documentusertraining';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_documentversions ON public.documentversions';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_employeedraftobservation ON public.employeedraftobservation';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_esignatures ON public.esignatures';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_orgunits ON public.orgunits';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_policyassignments ON public.policyassignments';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_responsibilitytransfers ON public.responsibilitytransfers';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_roles ON public.roles';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_subdepartments ON public.subdepartments';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_templates ON public.templates';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_trainingpolicies ON public.trainingpolicies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_transferscopepolicies ON public.transferscopepolicies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_transferworkflowpolicies ON public.transferworkflowpolicies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_uncontrolleddocumenthistory ON public.uncontrolleddocumenthistory';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_uncontrolleddocuments ON public.uncontrolleddocuments';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_useraccesslevels ON public.useraccesslevels';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_userroles ON public.userroles';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_users ON public.users';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_workflowpolicies ON public.workflowpolicies';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_workflowpolicyversions ON public.workflowpolicyversions';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_workflowstepdefinitions ON public.workflowstepdefinitions';
  EXECUTE 'DROP TRIGGER IF EXISTS trg_audit_workflowsteptypes ON public.workflowsteptypes';
  EXECUTE 'DROP VIEW IF EXISTS vw_workflowstepdefinitions';
  EXECUTE 'DROP VIEW IF EXISTS vw_workflowpolicy';
  EXECUTE 'DROP VIEW IF EXISTS vw_users';
  EXECUTE 'DROP VIEW IF EXISTS vw_useraccesslevelcabinets';
  EXECUTE 'DROP VIEW IF EXISTS vw_documents_all';
  EXECUTE 'DROP VIEW IF EXISTS vw_documents';
  EXECUTE 'DROP VIEW IF EXISTS vw_documentrequests_latest_count';
  EXECUTE 'DROP VIEW IF EXISTS vw_documentrequests';
  EXECUTE 'DROP VIEW IF EXISTS vw_employeenames';
  FOR r IN SELECT p.oid::regprocedure::text AS sig, p.prokind
             FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = ANY (ARRAY['fn_dms_audit_log', 'fn_get_my_inbox_documents', 'fn_get_my_inbox_requests', 'sp_get_my_workflow_requests']) LOOP
    EXECUTE format('DROP %s %s', CASE r.prokind WHEN 'p' THEN 'PROCEDURE' ELSE 'FUNCTION' END, r.sig);
  END LOOP;
END $sync$;


-- ============================================================================
-- 3. Columns missing from tables that already exist, and missing column defaults
-- ============================================================================

DO $sync$
DECLARE
  r record;
  v_type text; v_default text; v_notnull boolean; v_def text; v_has_rows boolean;
  v_cur_type text; v_cur_default text; v_rel regclass; v_ok boolean; v_cur_notnull boolean; v_is_pk boolean;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS _sync_nullable (tbl text, col text) ON COMMIT DROP;
  FOR r IN SELECT * FROM (VALUES
  ('alert_configs', 'id', 'integer NOT NULL'),
  ('alert_configs', 'company_id', 'integer NOT NULL'),
  ('alert_configs', 'name', 'character varying(255) NOT NULL'),
  ('alert_configs', 'is_active', 'boolean DEFAULT true NOT NULL'),
  ('alert_configs', 'pending_state', 'character varying(50)'),
  ('alert_configs', 'pending_days', 'integer'),
  ('alert_configs', 'review_within_days', 'integer'),
  ('alert_configs', 'inactive_days', 'integer'),
  ('alert_configs', 'status_changed_to', 'character varying(50)'),
  ('alert_configs', 'lookback_days', 'integer'),
  ('alert_configs', 'frequency', 'character varying(20) NOT NULL'),
  ('alert_configs', 'send_at_time', 'time without time zone NOT NULL'),
  ('alert_configs', 'delivery_format', 'character varying(30) NOT NULL'),
  ('alert_configs', 'email_subject', 'character varying(500) NOT NULL'),
  ('alert_configs', 'email_body', 'text NOT NULL'),
  ('alert_configs', 'created_by', 'character varying(20) NOT NULL'),
  ('alert_configs', 'created_at', 'timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('alert_configs', 'updated_at', 'timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('alert_document_scopes', 'id', 'integer NOT NULL'),
  ('alert_document_scopes', 'alert_config_id', 'integer NOT NULL'),
  ('alert_document_scopes', 'company_id', 'integer NOT NULL'),
  ('alert_document_scopes', 'divisioncode', 'character varying(10)'),
  ('alert_document_scopes', 'departmentcode', 'character varying(20)'),
  ('alert_document_scopes', 'subdepartmentcode', 'character varying(30)'),
  ('alert_document_scopes', 'businessdomaincode', 'character varying(30)'),
  ('alert_recipients', 'id', 'integer NOT NULL'),
  ('alert_recipients', 'alert_config_id', 'integer NOT NULL'),
  ('alert_recipients', 'email_address', 'character varying(256) NOT NULL'),
  ('alert_recipients', 'recipient_type', 'character varying(10) NOT NULL'),
  ('alert_recipients', 'is_self', 'boolean DEFAULT false NOT NULL'),
  ('attributemandatoryscopes', 'id', 'integer NOT NULL'),
  ('attributemandatoryscopes', 'companyid', 'integer NOT NULL'),
  ('attributemandatoryscopes', 'documentattributeid', 'integer NOT NULL'),
  ('attributemandatoryscopes', 'divisioncode', 'character varying(10) NOT NULL'),
  ('attributemandatoryscopes', 'departmentcode', 'character varying(20) NOT NULL'),
  ('attributemandatoryscopes', 'subdepartmentcode', 'character varying(30) NOT NULL'),
  ('attributemandatoryscopes', 'businessdomaincode', 'character varying(30) NOT NULL'),
  ('attributemandatoryscopes', 'ismandatory', 'boolean DEFAULT false NOT NULL'),
  ('attributemandatoryscopes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('attributemandatoryscopes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('attributemandatoryscopes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('attributemandatoryscopes', 'createdby', 'character varying(100) NOT NULL'),
  ('attributemandatoryscopes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('attributemandatoryscopes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('auditlogs', 'companyid', 'integer NOT NULL'),
  ('auditlogs', 'id', 'integer NOT NULL'),
  ('auditlogs', 'employeecode', 'character varying(10) NOT NULL'),
  ('auditlogs', 'action', 'character varying(100) NOT NULL'),
  ('auditlogs', 'entitytype', 'character varying(50) NOT NULL'),
  ('auditlogs', 'entityid', 'integer NOT NULL'),
  ('auditlogs', 'oldvalues', 'jsonb'),
  ('auditlogs', 'newvalues', 'jsonb'),
  ('auditlogs', 'timestamp', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('auditlogs', 'ipaddress', 'character varying(45)'),
  ('businessdomains', 'id', 'integer NOT NULL'),
  ('businessdomains', 'companyid', 'integer NOT NULL'),
  ('businessdomains', 'subdepartmentcode', 'character varying(30) NOT NULL'),
  ('businessdomains', 'code', 'character varying(10) NOT NULL'),
  ('businessdomains', 'name', 'character varying(100) NOT NULL'),
  ('businessdomains', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('businessdomains', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('businessdomains', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('businessdomains', 'createdby', 'character varying(100) NOT NULL'),
  ('businessdomains', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('businessdomains', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('cabinetstructuretabsconfig', 'id', 'integer NOT NULL'),
  ('cabinetstructuretabsconfig', 'companyid', 'integer NOT NULL'),
  ('cabinetstructuretabsconfig', 'name', 'character varying(100) NOT NULL'),
  ('cabinetstructuretabsconfig', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('cabinetstructuretabsconfig', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('cabinetstructuretabsconfig', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('cabinetstructuretabsconfig', 'createdby', 'character varying(100) NOT NULL'),
  ('cabinetstructuretabsconfig', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('cabinetstructuretabsconfig', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('companies', 'id', 'bigint NOT NULL'),
  ('companies', 'code', 'character varying(20) NOT NULL'),
  ('companies', 'name', 'character varying(200) NOT NULL'),
  ('companies', 'status', 'character varying(50) DEFAULT ''Active''::character varying'),
  ('companies', 'subscriptionplan', 'character varying(50)'),
  ('companies', 'storagequotagb', 'integer'),
  ('companies', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('controltypes', 'id', 'integer NOT NULL'),
  ('controltypes', 'name', 'character varying(50) NOT NULL'),
  ('controltypes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('controltypes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('controltypes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('controltypes', 'createdby', 'character varying(100) NOT NULL'),
  ('controltypes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('controltypes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('departments', 'id', 'integer NOT NULL'),
  ('departments', 'companyid', 'integer NOT NULL'),
  ('departments', 'divisioncode', 'character varying(20) NOT NULL'),
  ('departments', 'code', 'character varying(20) NOT NULL'),
  ('departments', 'name', 'character varying(100) NOT NULL'),
  ('departments', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('departments', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('departments', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('departments', 'createdby', 'character varying(100) NOT NULL'),
  ('departments', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('departments', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('designations', 'id', 'integer NOT NULL'),
  ('designations', 'companyid', 'integer NOT NULL'),
  ('designations', 'code', 'character varying(10) NOT NULL'),
  ('designations', 'name', 'character varying(100) NOT NULL'),
  ('designations', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('designations', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('designations', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('designations', 'createdby', 'character varying(100) NOT NULL'),
  ('designations', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('designations', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('distributiontypes', 'id', 'integer NOT NULL'),
  ('distributiontypes', 'companyid', 'integer NOT NULL'),
  ('distributiontypes', 'name', 'character varying(50) NOT NULL'),
  ('distributiontypes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('distributiontypes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('distributiontypes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('distributiontypes', 'createdby', 'character varying(100) NOT NULL'),
  ('distributiontypes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('distributiontypes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('divisions', 'id', 'integer NOT NULL'),
  ('divisions', 'companyid', 'integer NOT NULL'),
  ('divisions', 'code', 'character varying(10) NOT NULL'),
  ('divisions', 'name', 'character varying(100) NOT NULL'),
  ('divisions', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('divisions', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('divisions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('divisions', 'createdby', 'character varying(100) NOT NULL'),
  ('divisions', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('divisions', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentadhocapprovers', 'id', 'integer NOT NULL'),
  ('documentadhocapprovers', 'companyid', 'integer NOT NULL'),
  ('documentadhocapprovers', 'documentid', 'integer NOT NULL'),
  ('documentadhocapprovers', 'employeecode', 'character varying(20) NOT NULL'),
  ('documentadhocapprovers', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentadhocapprovers', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentadhocapprovers', 'createdby', 'character varying(100) NOT NULL'),
  ('documentadhocapprovers', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentadhocapprovers', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentattributes', 'id', 'integer NOT NULL'),
  ('documentattributes', 'companyid', 'integer NOT NULL'),
  ('documentattributes', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('documentattributes', 'controllabel', 'character varying(100) NOT NULL'),
  ('documentattributes', 'controltypeid', 'integer NOT NULL'),
  ('documentattributes', 'listvalues', 'character varying(1000)'),
  ('documentattributes', 'ismandatory', 'boolean DEFAULT false NOT NULL'),
  ('documentattributes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentattributes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentattributes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentattributes', 'createdby', 'character varying(100) NOT NULL'),
  ('documentattributes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentattributes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentattributevalues', 'id', 'integer NOT NULL'),
  ('documentattributevalues', 'companyid', 'integer NOT NULL'),
  ('documentattributevalues', 'documentid', 'integer NOT NULL'),
  ('documentattributevalues', 'documentattributeid', 'integer NOT NULL'),
  ('documentattributevalues', 'valuetext', 'text'),
  ('documentattributevalues', 'valuenumber', 'numeric'),
  ('documentattributevalues', 'valuedate', 'date'),
  ('documentattributevalues', 'valueboolean', 'boolean'),
  ('documentattributevalues', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentattributevalues', 'createdby', 'character varying(100) NOT NULL'),
  ('documentevents', 'companyid', 'bigint NOT NULL'),
  ('documentevents', 'id', 'bigint NOT NULL'),
  ('documentevents', 'documentid', 'bigint NOT NULL'),
  ('documentevents', 'eventtype', 'character varying(100) NOT NULL'),
  ('documentevents', 'performedby', 'bigint NOT NULL'),
  ('documentevents', 'eventtimestamp', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentevents', 'metadata', 'jsonb'),
  ('documentrequesthistory', 'id', 'integer NOT NULL'),
  ('documentrequesthistory', 'companyid', 'integer NOT NULL'),
  ('documentrequesthistory', 'requestid', 'integer NOT NULL'),
  ('documentrequesthistory', 'fromstateid', 'integer'),
  ('documentrequesthistory', 'tostateid', 'integer NOT NULL'),
  ('documentrequesthistory', 'changedby', 'character varying(100) NOT NULL'),
  ('documentrequesthistory', 'changedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrequesthistory', 'workflowexecutionid', 'integer'),
  ('documentrequesthistory', 'comments', 'text'),
  ('documentrequestroledistributions', 'id', 'integer NOT NULL'),
  ('documentrequestroledistributions', 'companyid', 'integer NOT NULL'),
  ('documentrequestroledistributions', 'documentrequestid', 'integer NOT NULL'),
  ('documentrequestroledistributions', 'divisioncode', 'character varying(10)'),
  ('documentrequestroledistributions', 'departmentcode', 'character varying(20)'),
  ('documentrequestroledistributions', 'subdepartmentcode', 'character varying(30)'),
  ('documentrequestroledistributions', 'businessdomaincode', 'character varying(30)'),
  ('documentrequestroledistributions', 'roleid', 'integer'),
  ('documentrequestroledistributions', 'distributiontypeid', 'integer NOT NULL'),
  ('documentrequestroledistributions', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentrequestroledistributions', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentrequestroledistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequestroledistributions', 'createdby', 'character varying(100) NOT NULL'),
  ('documentrequestroledistributions', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequestroledistributions', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentrequests', 'id', 'integer NOT NULL'),
  ('documentrequests', 'companyid', 'integer NOT NULL'),
  ('documentrequests', 'requestnumber', 'character varying(50) NOT NULL'),
  ('documentrequests', 'documentrequesttypecode', 'character varying(10) NOT NULL'),
  ('documentrequests', 'documentid', 'integer'),
  ('documentrequests', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('documentrequests', 'divisioncode', 'character varying(10)'),
  ('documentrequests', 'departmentcode', 'character varying(20)'),
  ('documentrequests', 'subdepartmentcode', 'character varying(30)'),
  ('documentrequests', 'businessdomaincode', 'character varying(30)'),
  ('documentrequests', 'documentname', 'character varying(500) NOT NULL'),
  ('documentrequests', 'justification', 'character varying(2000) NOT NULL'),
  ('documentrequests', 'proposedcontent', 'text'),
  ('documentrequests', 'iscontentfinalized', 'boolean DEFAULT false'),
  ('documentrequests', 'draftcontentlastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrequests', 'draftcontentlastmodifiedby', 'character varying(100)'),
  ('documentrequests', 'submittedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrequests', 'submittedby', 'character varying(100)'),
  ('documentrequests', 'status', 'integer NOT NULL'),
  ('documentrequests', 'rowversion', 'character varying(10)'),
  ('documentrequests', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentrequests', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentrequests', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequests', 'createdby', 'character varying(100) NOT NULL'),
  ('documentrequests', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequests', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentrequests', 'draftfileurl', 'character varying(500)'),
  ('documentrequests', 'parentdocumentid', 'integer'),
  ('documentrequests', 'parentrequestid', 'integer'),
  ('documentrequesttypes', 'id', 'integer NOT NULL'),
  ('documentrequesttypes', 'companyid', 'integer NOT NULL'),
  ('documentrequesttypes', 'code', 'character varying(10) NOT NULL'),
  ('documentrequesttypes', 'name', 'character varying(50) NOT NULL'),
  ('documentrequesttypes', 'description', 'character varying(500)'),
  ('documentrequesttypes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentrequesttypes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentrequesttypes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequesttypes', 'createdby', 'character varying(100) NOT NULL'),
  ('documentrequesttypes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentrequesttypes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentrequestuserdistributions', 'id', 'integer NOT NULL'),
  ('documentrequestuserdistributions', 'companyid', 'integer NOT NULL'),
  ('documentrequestuserdistributions', 'documentrequestid', 'integer NOT NULL'),
  ('documentrequestuserdistributions', 'employeecode', 'character varying(20) NOT NULL'),
  ('documentrequestuserdistributions', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentrequestuserdistributions', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentrequestuserdistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrequestuserdistributions', 'createdby', 'character varying(120) NOT NULL'),
  ('documentrequestuserdistributions', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrequestuserdistributions', 'lastmodifiedby', 'character varying(120) NOT NULL'),
  ('documentrequestuserdistributions', 'roleid', 'integer'),
  ('documentrequestuserdistributions', 'divisioncode', 'character varying(10)'),
  ('documentrequestuserdistributions', 'departmentcode', 'character varying(20)'),
  ('documentrequestuserdistributions', 'subdepartmentcode', 'character varying(30)'),
  ('documentrequestuserdistributions', 'businessdomaincode', 'character varying(30)'),
  ('documentreviewpolicies', 'id', 'integer NOT NULL'),
  ('documentreviewpolicies', 'companyid', 'integer NOT NULL'),
  ('documentreviewpolicies', 'documenttypecode', 'character varying(50) NOT NULL'),
  ('documentreviewpolicies', 'reviewperiodyears', 'integer DEFAULT 1 NOT NULL'),
  ('documentreviewpolicies', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentreviewpolicies', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentreviewpolicies', 'createdat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('documentreviewpolicies', 'createdby', 'character varying(50)'),
  ('documentreviewpolicies', 'lastmodifiedat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('documentreviewpolicies', 'lastmodifiedby', 'character varying(50)'),
  ('documentrevisiondistributions', 'id', 'integer NOT NULL'),
  ('documentrevisiondistributions', 'companyid', 'integer NOT NULL'),
  ('documentrevisiondistributions', 'documentid', 'integer NOT NULL'),
  ('documentrevisiondistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrevisiondistributions', 'createdby', 'character varying NOT NULL'),
  ('documentrevisionroledistributions', 'id', 'integer NOT NULL'),
  ('documentrevisionroledistributions', 'companyid', 'integer NOT NULL'),
  ('documentrevisionroledistributions', 'documentid', 'integer NOT NULL'),
  ('documentrevisionroledistributions', 'divisioncode', 'character varying'),
  ('documentrevisionroledistributions', 'departmentcode', 'character varying'),
  ('documentrevisionroledistributions', 'subdepartmentcode', 'character varying'),
  ('documentrevisionroledistributions', 'businessdomaincode', 'character varying'),
  ('documentrevisionroledistributions', 'roleid', 'integer NOT NULL'),
  ('documentrevisionroledistributions', 'distributiontype', 'integer NOT NULL'),
  ('documentrevisionroledistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrevisionroledistributions', 'createdby', 'character varying NOT NULL'),
  ('documentrevisionroledistributions', 'isretrieved', 'boolean DEFAULT false NOT NULL'),
  ('documentrevisionroledistributions', 'retrievedat', 'timestamp without time zone'),
  ('documentrevisionroledistributions', 'retrievedby', 'character varying'),
  ('documentrevisionuserdistributions', 'id', 'integer NOT NULL'),
  ('documentrevisionuserdistributions', 'companyid', 'integer NOT NULL'),
  ('documentrevisionuserdistributions', 'documentid', 'integer NOT NULL'),
  ('documentrevisionuserdistributions', 'employeecode', 'character varying NOT NULL'),
  ('documentrevisionuserdistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentrevisionuserdistributions', 'createdby', 'character varying NOT NULL'),
  ('documentrevisionuserdistributions', 'roleid', 'integer'),
  ('documentrevisionuserdistributions', 'divisioncode', 'character varying'),
  ('documentrevisionuserdistributions', 'departmentcode', 'character varying'),
  ('documentrevisionuserdistributions', 'subdepartmentcode', 'character varying'),
  ('documentrevisionuserdistributions', 'businessdomaincode', 'character varying'),
  ('documentroledistributions', 'id', 'integer NOT NULL'),
  ('documentroledistributions', 'companyid', 'integer NOT NULL'),
  ('documentroledistributions', 'documentid', 'integer NOT NULL'),
  ('documentroledistributions', 'divisioncode', 'character varying(10)'),
  ('documentroledistributions', 'departmentcode', 'character varying(20)'),
  ('documentroledistributions', 'subdepartmentcode', 'character varying(30)'),
  ('documentroledistributions', 'businessdomaincode', 'character varying(30)'),
  ('documentroledistributions', 'roleid', 'integer NOT NULL'),
  ('documentroledistributions', 'distributiontype', 'integer NOT NULL'),
  ('documentroledistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentroledistributions', 'createdby', 'character varying(100) NOT NULL'),
  ('documentroledistributions', 'isretrieved', 'boolean DEFAULT false NOT NULL'),
  ('documentroledistributions', 'retrievedat', 'timestamp without time zone'),
  ('documentroledistributions', 'retrievedby', 'character varying(100)'),
  ('documents', 'id', 'integer NOT NULL'),
  ('documents', 'companyid', 'integer NOT NULL'),
  ('documents', 'documentnumber', 'character varying(50)'),
  ('documents', 'requestid', 'integer'),
  ('documents', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('documents', 'parentdocumentid', 'integer'),
  ('documents', 'title', 'character varying(500) NOT NULL'),
  ('documents', 'divisioncode', 'character varying(10)'),
  ('documents', 'departmentcode', 'character varying(20)'),
  ('documents', 'subdepartmentcode', 'character varying(30)'),
  ('documents', 'businessdomaincode', 'character varying(30)'),
  ('documents', 'nextreviewdate', 'date'),
  ('documents', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documents', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documents', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documents', 'createdby', 'character varying(100) NOT NULL'),
  ('documents', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documents', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documents', 'documenturl', 'character varying(500)'),
  ('documents', 'justification', 'character varying(1000)'),
  ('documents', 'activitytypecode', 'character varying(20)'),
  ('documentstatehistory', 'companyid', 'integer NOT NULL'),
  ('documentstatehistory', 'id', 'bigint NOT NULL'),
  ('documentstatehistory', 'documentid', 'bigint NOT NULL'),
  ('documentstatehistory', 'fromstateid', 'integer'),
  ('documentstatehistory', 'tostateid', 'integer NOT NULL'),
  ('documentstatehistory', 'changedby', 'character varying(100) NOT NULL'),
  ('documentstatehistory', 'changedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentstatehistory', 'workflowexecutionid', 'bigint'),
  ('documentstatehistory', 'comments', 'text'),
  ('documentstates', 'id', 'integer NOT NULL'),
  ('documentstates', 'code', 'character varying(50) NOT NULL'),
  ('documentstates', 'name', 'character varying(100) NOT NULL'),
  ('documentstates', 'isterminal', 'boolean DEFAULT false'),
  ('documentstatetransitions', 'id', 'integer NOT NULL'),
  ('documentstatetransitions', 'fromstateid', 'integer NOT NULL'),
  ('documentstatetransitions', 'tostateid', 'integer NOT NULL'),
  ('documentstatetransitions', 'requiresworkflow', 'boolean DEFAULT false'),
  ('documentstatetransitions', 'requirestraining', 'boolean DEFAULT false'),
  ('documentstatetransitions', 'requiresauthorization', 'boolean DEFAULT false'),
  ('documenttraining', 'id', 'integer NOT NULL'),
  ('documenttraining', 'companyid', 'integer NOT NULL'),
  ('documenttraining', 'documentid', 'integer NOT NULL'),
  ('documenttraining', 'trainingmode', 'integer NOT NULL'),
  ('documenttraining', 'trainingstatus', 'integer NOT NULL'),
  ('documenttraining', 'trainingproofurl', 'character varying(500)'),
  ('documenttraining', 'assessmentscore', 'numeric(5,2)'),
  ('documenttraining', 'validationstatus', 'integer DEFAULT 1 NOT NULL'),
  ('documenttraining', 'readyforauthorization', 'boolean DEFAULT false NOT NULL'),
  ('documenttraining', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documenttraining', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documenttraining', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documenttraining', 'createdby', 'character varying(100) NOT NULL'),
  ('documenttraining', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documenttraining', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documenttrainingauthorizations', 'id', 'integer NOT NULL'),
  ('documenttrainingauthorizations', 'companyid', 'integer NOT NULL'),
  ('documenttrainingauthorizations', 'documenttypecode', 'character varying(50) NOT NULL'),
  ('documenttrainingauthorizations', 'authorizationrequired', 'boolean DEFAULT false NOT NULL'),
  ('documenttrainingauthorizations', 'authorizinguserid', 'character varying(50)'),
  ('documenttrainingauthorizations', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documenttrainingauthorizations', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documenttrainingauthorizations', 'createdat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('documenttrainingauthorizations', 'createdby', 'character varying(50)'),
  ('documenttrainingauthorizations', 'lastmodifiedat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('documenttrainingauthorizations', 'lastmodifiedby', 'character varying(50)'),
  ('documenttypes', 'id', 'integer NOT NULL'),
  ('documenttypes', 'companyid', 'integer NOT NULL'),
  ('documenttypes', 'code', 'character varying(10) NOT NULL'),
  ('documenttypes', 'name', 'character varying(50) NOT NULL'),
  ('documenttypes', 'description', 'character varying(150)'),
  ('documenttypes', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documenttypes', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documenttypes', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documenttypes', 'createdby', 'character varying(100) NOT NULL'),
  ('documenttypes', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documenttypes', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentuserdistributions', 'id', 'integer NOT NULL'),
  ('documentuserdistributions', 'companyid', 'integer NOT NULL'),
  ('documentuserdistributions', 'documentid', 'integer NOT NULL'),
  ('documentuserdistributions', 'employeecode', 'character varying(20) NOT NULL'),
  ('documentuserdistributions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentuserdistributions', 'createdby', 'character varying(100) NOT NULL'),
  ('documentuserdistributions', 'roleid', 'integer'),
  ('documentuserdistributions', 'divisioncode', 'character varying(50)'),
  ('documentuserdistributions', 'departmentcode', 'character varying(50)'),
  ('documentuserdistributions', 'subdepartmentcode', 'character varying(50)'),
  ('documentuserdistributions', 'businessdomaincode', 'character varying(50)'),
  ('documentusertraining', 'id', 'integer NOT NULL'),
  ('documentusertraining', 'companyid', 'integer NOT NULL'),
  ('documentusertraining', 'documentid', 'integer NOT NULL'),
  ('documentusertraining', 'employeecode', 'character varying(20) NOT NULL'),
  ('documentusertraining', 'trainingmode', 'integer NOT NULL'),
  ('documentusertraining', 'trainingstatus', 'integer NOT NULL'),
  ('documentusertraining', 'trainingproofurl', 'character varying(500)'),
  ('documentusertraining', 'assessmentscore', 'numeric(5,2)'),
  ('documentusertraining', 'validationstatus', 'integer DEFAULT 1 NOT NULL'),
  ('documentusertraining', 'readyforauthorization', 'boolean DEFAULT false NOT NULL'),
  ('documentusertraining', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentusertraining', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentusertraining', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentusertraining', 'createdby', 'character varying(100) NOT NULL'),
  ('documentusertraining', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('documentusertraining', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentversions', 'id', 'integer NOT NULL'),
  ('documentversions', 'companyid', 'integer NOT NULL'),
  ('documentversions', 'documentid', 'integer NOT NULL'),
  ('documentversions', 'version', 'character varying(10) NOT NULL'),
  ('documentversions', 'documenturl', 'character varying(1500)'),
  ('documentversions', 'versiontype', 'integer NOT NULL'),
  ('documentversions', 'content', 'text'),
  ('documentversions', 'changedescription', 'character varying(1000)'),
  ('documentversions', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('documentversions', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('documentversions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentversions', 'createdby', 'character varying(100) NOT NULL'),
  ('documentversions', 'createdbyid', 'integer'),
  ('documentversions', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('documentversions', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('documentversions', 'archivereason', 'character varying(30)'),
  ('emailoutbox', 'id', 'bigint NOT NULL'),
  ('emailoutbox', 'companyid', 'integer NOT NULL'),
  ('emailoutbox', 'recipientemployeecode', 'character varying(50)'),
  ('emailoutbox', 'recipientemail', 'character varying(320) NOT NULL'),
  ('emailoutbox', 'subject', 'character varying(500) NOT NULL'),
  ('emailoutbox', 'body', 'text NOT NULL'),
  ('emailoutbox', 'notificationtype', 'integer'),
  ('emailoutbox', 'relatedentityid', 'integer'),
  ('emailoutbox', 'status', 'character varying(20) DEFAULT ''Pending''::character varying NOT NULL'),
  ('emailoutbox', 'isdelivered', 'boolean DEFAULT false NOT NULL'),
  ('emailoutbox', 'attemptcount', 'integer DEFAULT 0 NOT NULL'),
  ('emailoutbox', 'lasterror', 'text'),
  ('emailoutbox', 'createdat', 'timestamp without time zone DEFAULT (now() AT TIME ZONE ''UTC''::text) NOT NULL'),
  ('emailoutbox', 'nextattemptat', 'timestamp without time zone DEFAULT (now() AT TIME ZONE ''UTC''::text) NOT NULL'),
  ('emailoutbox', 'lastattemptat', 'timestamp without time zone'),
  ('emailoutbox', 'deliveredat', 'timestamp without time zone'),
  ('employeedraftobservation', 'id', 'integer NOT NULL'),
  ('employeedraftobservation', 'employeecode', 'character varying(50) NOT NULL'),
  ('employeedraftobservation', 'companyid', 'integer NOT NULL'),
  ('employeedraftobservation', 'observationtext', 'text NOT NULL'),
  ('employeedraftobservation', 'documentid', 'integer'),
  ('employeedraftobservation', 'requestid', 'integer'),
  ('esignatures', 'id', 'integer NOT NULL'),
  ('esignatures', 'companyid', 'integer NOT NULL'),
  ('esignatures', 'userid', 'character varying(10) NOT NULL'),
  ('esignatures', 'signatureurl', 'character varying(1000) NOT NULL'),
  ('esignatures', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('esignatures', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('esignatures', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('esignatures', 'createdby', 'character varying(100) NOT NULL'),
  ('esignatures', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('esignatures', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('notifications', 'id', 'integer NOT NULL'),
  ('notifications', 'companyid', 'integer NOT NULL'),
  ('notifications', 'employeecode', 'character varying(100) NOT NULL'),
  ('notifications', 'title', 'character varying(200) NOT NULL'),
  ('notifications', 'message', 'character varying(1000) NOT NULL'),
  ('notifications', 'notificationtype', 'integer NOT NULL'),
  ('notifications', 'relatedentitytype', 'character varying(50)'),
  ('notifications', 'relatedentityid', 'integer'),
  ('notifications', 'redirectionurl', 'character varying(200)'),
  ('notifications', 'isread', 'boolean DEFAULT false NOT NULL'),
  ('notifications', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('orgunits', 'companyid', 'bigint NOT NULL'),
  ('orgunits', 'id', 'bigint NOT NULL'),
  ('orgunits', 'parentid', 'bigint'),
  ('orgunits', 'code', 'character varying(20) NOT NULL'),
  ('orgunits', 'name', 'character varying(200) NOT NULL'),
  ('orgunits', 'type', 'character varying(50) NOT NULL'),
  ('policyassignments', 'id', 'bigint NOT NULL'),
  ('policyassignments', 'policytype', 'character varying(50)'),
  ('policyassignments', 'policyversionid', 'bigint NOT NULL'),
  ('policyassignments', 'orgunitid', 'bigint'),
  ('policyassignments', 'documenttypecode', 'character varying(10)'),
  ('policyassignments', 'isactive', 'boolean DEFAULT true'),
  ('responsibilitytransfers', 'id', 'integer NOT NULL'),
  ('responsibilitytransfers', 'companyid', 'integer NOT NULL'),
  ('responsibilitytransfers', 'employeefrom', 'character varying(10) NOT NULL'),
  ('responsibilitytransfers', 'employeeto', 'character varying(10) NOT NULL'),
  ('responsibilitytransfers', 'reasonfortransfer', 'character varying(10) NOT NULL'),
  ('responsibilitytransfers', 'effectivedatefrom', 'date NOT NULL'),
  ('responsibilitytransfers', 'effectivedateto', 'date'),
  ('responsibilitytransfers', 'permanenttransfer', 'boolean DEFAULT false'),
  ('responsibilitytransfers', 'attachment', 'character varying(1000)'),
  ('responsibilitytransfers', 'remarks', 'character varying(1000) NOT NULL'),
  ('responsibilitytransfers', 'status', 'integer DEFAULT 1 NOT NULL'),
  ('responsibilitytransfers', 'approverid', 'character varying(100) NOT NULL'),
  ('responsibilitytransfers', 'observation', 'character varying(1000)'),
  ('responsibilitytransfers', 'actiondate', 'timestamp without time zone'),
  ('responsibilitytransfers', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('responsibilitytransfers', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('responsibilitytransfers', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('responsibilitytransfers', 'createdby', 'character varying(100) NOT NULL'),
  ('responsibilitytransfers', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('responsibilitytransfers', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('rolecabinetmappings', 'id', 'integer NOT NULL'),
  ('rolecabinetmappings', 'companyid', 'integer NOT NULL'),
  ('rolecabinetmappings', 'roleid', 'integer NOT NULL'),
  ('rolecabinetmappings', 'divisioncode', 'character varying(100) NOT NULL'),
  ('rolecabinetmappings', 'departmentcode', 'character varying(100)'),
  ('rolecabinetmappings', 'subdepartmentcode', 'character varying(100)'),
  ('rolecabinetmappings', 'businessdomaincode', 'character varying(100)'),
  ('rolecabinetmappings', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('rolecabinetmappings', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('rolecabinetmappings', 'createdat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('rolecabinetmappings', 'createdby', 'character varying(50)'),
  ('rolecabinetmappings', 'lastmodifiedat', 'timestamp without time zone DEFAULT now() NOT NULL'),
  ('rolecabinetmappings', 'lastmodifiedby', 'character varying(50)'),
  ('roles', 'id', 'integer NOT NULL'),
  ('roles', 'companyid', 'integer NOT NULL'),
  ('roles', 'name', 'character varying(50) NOT NULL'),
  ('roles', 'description', 'character varying(200)'),
  ('roles', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('roles', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('roles', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('roles', 'createdby', 'character varying(100) NOT NULL'),
  ('roles', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('roles', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('subdepartments', 'id', 'integer NOT NULL'),
  ('subdepartments', 'companyid', 'integer NOT NULL'),
  ('subdepartments', 'departmentcode', 'character varying(20) NOT NULL'),
  ('subdepartments', 'code', 'character varying(30) NOT NULL'),
  ('subdepartments', 'name', 'character varying(100) NOT NULL'),
  ('subdepartments', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('subdepartments', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('subdepartments', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('subdepartments', 'createdby', 'character varying(100) NOT NULL'),
  ('subdepartments', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('subdepartments', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('tbldeptstrdetail', 'dptdid', 'integer NOT NULL'),
  ('tbldeptstrdetail', 'dptmid', 'integer'),
  ('tbldeptstrdetail', 'dptid', 'integer'),
  ('tbldeptstrdetail', 'budgeted', 'real'),
  ('tbldeptstrdetail', 'approved', 'real'),
  ('tbldeptstrdetail', 'companyid', 'integer'),
  ('tbldeptstrdetail', 'userid', 'character varying'),
  ('tbldeptstrdetail', 'applicationid', 'character varying'),
  ('tbldeptstrdetail', 'formid', 'character varying'),
  ('tbldeptstrdetail', 'userempid', 'integer'),
  ('tbldeptstrdetail', 'userempname', 'character varying'),
  ('tbldeptstrdetail', 'userempcode', 'character varying'),
  ('tbldeptstrdetail', 'entterminal', 'character varying'),
  ('tbldeptstrdetail', 'entterminalip', 'character varying'),
  ('tbldeptstrdetail', 'entoperation', 'character varying'),
  ('tbldeptstrdetail', 'entdate', 'timestamp without time zone'),
  ('tbldeptstrmaster', 'dptmid', 'integer NOT NULL'),
  ('tbldeptstrmaster', 'mdptid', 'integer'),
  ('tbldeptstrmaster', 'companyid', 'integer'),
  ('tbldeptstrmaster', 'budgeted', 'real'),
  ('tbldeptstrmaster', 'approved', 'real'),
  ('tbldeptstrmaster', 'divid', 'integer'),
  ('tbldeptstrmaster', 'userid', 'character varying'),
  ('tbldeptstrmaster', 'applicationid', 'character varying'),
  ('tbldeptstrmaster', 'formid', 'character varying'),
  ('tbldeptstrmaster', 'userempid', 'integer'),
  ('tbldeptstrmaster', 'userempname', 'character varying'),
  ('tbldeptstrmaster', 'userempcode', 'character varying'),
  ('tbldeptstrmaster', 'entterminal', 'character varying'),
  ('tbldeptstrmaster', 'entterminalip', 'character varying'),
  ('tbldeptstrmaster', 'entoperation', 'character varying'),
  ('tbldeptstrmaster', 'entdate', 'timestamp without time zone'),
  ('tblempjobprofile', 'jobprofileid', 'integer NOT NULL'),
  ('tblempjobprofile', 'empid', 'integer NOT NULL'),
  ('tblempjobprofile', 'jobcode', 'character(7) NOT NULL'),
  ('tblempjobprofile', 'jobcdate', 'timestamp without time zone NOT NULL'),
  ('tblempjobprofile', 'jobtitle', 'character varying(100)'),
  ('tblempjobprofile', 'mdptid', 'integer NOT NULL'),
  ('tblempjobprofile', 'dptid', 'integer NOT NULL'),
  ('tblempjobprofile', 'jobid', 'integer NOT NULL'),
  ('tblempjobprofile', 'baseid', 'integer NOT NULL'),
  ('tblempjobprofile', 'typid', 'integer NOT NULL'),
  ('tblempjobprofile', 'reportingrelationship', 'character varying(225)'),
  ('tblempjobprofile', 'keyrelationshipint', 'character varying(100)'),
  ('tblempjobprofile', 'keyrelationshipext', 'character varying(100)'),
  ('tblempjobprofile', 'academicqualifications', 'text'),
  ('tblempjobprofile', 'workexperience', 'text'),
  ('tblempjobprofile', 'jobsummary', 'text'),
  ('tblempjobprofile', 'keyperformanceindicators', 'text'),
  ('tblempjobprofile', 'responsibilities', 'text'),
  ('tblempjobprofile', 'accountabilities', 'text'),
  ('tblempjobprofile', 'eoworkcondition', 'text'),
  ('tblempjobprofile', 'empdate', 'timestamp without time zone'),
  ('tblempjobprofile', 'mgrempid', 'integer'),
  ('tblempjobprofile', 'mgrdate', 'timestamp without time zone'),
  ('tblempjobprofile', 'hrempid', 'integer'),
  ('tblempjobprofile', 'hrdate', 'timestamp without time zone'),
  ('tblempjobprofile', 'companyid', 'integer NOT NULL'),
  ('tblempjobprofile', 'appdoc', 'bytea'),
  ('tblempjobprofile', 'divid', 'integer'),
  ('tblempjobprofile', 'directrptto', 'boolean'),
  ('tblempjobprofile', 'dcompanyid', 'integer'),
  ('tblempjobprofile', 'dgradeid', 'integer'),
  ('tblempjobprofile', 'ddsgid', 'integer'),
  ('tblempjobprofile', 'ddivid', 'integer'),
  ('tblempjobprofile', 'dmdptid', 'integer'),
  ('tblempjobprofile', 'ddptid', 'integer'),
  ('tblempjobprofile', 'dbaseid', 'integer'),
  ('tblempjobprofile', 'indirectrptto', 'boolean'),
  ('tblempjobprofile', 'indcompanyid', 'integer'),
  ('tblempjobprofile', 'indgradeid', 'integer'),
  ('tblempjobprofile', 'inddsgid', 'integer'),
  ('tblempjobprofile', 'inddivid', 'integer'),
  ('tblempjobprofile', 'indmdptid', 'integer'),
  ('tblempjobprofile', 'inddptid', 'integer'),
  ('tblempjobprofile', 'indbaseid', 'integer'),
  ('tblempjobprofile', 'pagerange', 'character varying(50)'),
  ('tblempjobprofile', 'prefgender', 'integer'),
  ('tblempjobprofile', 'eyears', 'character varying(50)'),
  ('tblempjobprofile', 'dsgid', 'integer'),
  ('tblempjobprofile', 'rexpyears', 'character varying(50)'),
  ('tblempjobprofile', 'chkqual', 'boolean'),
  ('tblempjobprofile', 'chkcert', 'boolean'),
  ('tblempjobprofile', 'chktrain', 'boolean'),
  ('tblempjobprofile', 'isdescripancyincompetency', 'boolean'),
  ('tblempjobprofile', 'isdescripancyinkpi', 'boolean'),
  ('tblempjobprofile', 'jdid', 'integer'),
  ('tblempjobprofile', 'active', 'boolean'),
  ('tblempjobprofile', 'pagerangeto', 'integer'),
  ('tblempjobprofile', 'eyearsto', 'integer'),
  ('tblempjobprofile', 'rexpyearsto', 'integer'),
  ('tblempjobprofile', 'incpackage', 'character varying(500)'),
  ('tblempjobprofile', 'otherbenifit', 'text'),
  ('tblempjobprofile', 'salaryremarks', 'text'),
  ('tblempjobprofile', 'visastatus', 'integer'),
  ('tblempjobprofile', 'salaryamountfrom', 'numeric(9,2)'),
  ('tblempjobprofile', 'salaryamountto', 'numeric(9,2)'),
  ('tblempjobprofile', 'salarycurrencyid', 'integer'),
  ('tblempjobprofile', 'prefreligion', 'integer'),
  ('tblempjobprofile', 'assigneddate', 'timestamp without time zone'),
  ('tblempjobprofile', 'userid', 'character varying(250)'),
  ('tblempjobprofile', 'applicationid', 'character varying(10)'),
  ('tblempjobprofile', 'formid', 'character varying(250)'),
  ('tblempjobprofile', 'userempid', 'integer'),
  ('tblempjobprofile', 'userempname', 'character varying(250)'),
  ('tblempjobprofile', 'userempcode', 'character varying(10)'),
  ('tblempjobprofile', 'entterminal', 'character varying(250)'),
  ('tblempjobprofile', 'entterminalip', 'character varying(250)'),
  ('tblempjobprofile', 'entoperation', 'character varying(10)'),
  ('tblempjobprofile', 'entdate', 'timestamp without time zone'),
  ('tblempjobprofile', 'emointlprofiling', 'integer'),
  ('tblempjobprofile', 'roleid', 'integer'),
  ('tblempjobprofile', 'droleid', 'integer'),
  ('tblempjobprofile', 'indroleid', 'integer'),
  ('tblempjobprofile', 'received', 'boolean'),
  ('tblempjobprofile', 'jdchangeversionno', 'character varying(500)'),
  ('tblempjobprofile', 'acknowledgeon', 'timestamp without time zone'),
  ('tblempjobprofile', 'acknowledgebyuserid', 'character varying(500)'),
  ('tblempjobprofile', 'acknowledgeterminal', 'character varying(500)'),
  ('tblempjobprofile', 'acknowledgebyempid', 'integer'),
  ('tblempjobprofile', 'changeresponsibilitydetail', 'text'),
  ('tblemployee', 'empid', 'integer NOT NULL'),
  ('tblemployee', 'empcode', 'character varying NOT NULL'),
  ('tblemployee', 'companyid', 'integer'),
  ('tblemployee', 'emprefno', 'character varying'),
  ('tblemployee', 'firstname', 'character varying NOT NULL'),
  ('tblemployee', 'midname', 'character varying'),
  ('tblemployee', 'lastname', 'character varying'),
  ('tblemployee', 'titleid', 'integer'),
  ('tblemployee', 'initial', 'character varying'),
  ('tblemployee', 'fname', 'character varying'),
  ('tblemployee', 'gndid', 'integer'),
  ('tblemployee', 'nic', 'character varying'),
  ('tblemployee', 'nicnew', 'character varying'),
  ('tblemployee', 'ntn', 'character varying'),
  ('tblemployee', 'dateofbirth', 'timestamp without time zone'),
  ('tblemployee', 'address', 'character varying'),
  ('tblemployee', 'ctyid', 'integer'),
  ('tblemployee', 'phone', 'character varying'),
  ('tblemployee', 'phonecntcode', 'character varying'),
  ('tblemployee', 'phonectycode', 'character varying'),
  ('tblemployee', 'phonenumber', 'character varying'),
  ('tblemployee', 'phone2', 'character varying'),
  ('tblemployee', 'phone2cntcode', 'character varying'),
  ('tblemployee', 'phone2ctycode', 'character varying'),
  ('tblemployee', 'phone2number', 'character varying'),
  ('tblemployee', 'mobile', 'character varying'),
  ('tblemployee', 'mobilecode', 'character varying'),
  ('tblemployee', 'mobilenumber', 'character varying'),
  ('tblemployee', 'email', 'character varying'),
  ('tblemployee', 'rlgid', 'integer'),
  ('tblemployee', 'mrtid', 'integer'),
  ('tblemployee', 'bldid', 'integer'),
  ('tblemployee', 'nationalityid', 'integer'),
  ('tblemployee', 'qlfid', 'integer'),
  ('tblemployee', 'emgperson', 'character varying'),
  ('tblemployee', 'emgphone', 'character varying'),
  ('tblemployee', 'emgaddress', 'character varying'),
  ('tblemployee', 'mdptid', 'integer'),
  ('tblemployee', 'dptid', 'integer NOT NULL'),
  ('tblemployee', 'dsgid', 'integer'),
  ('tblemployee', 'cmpid', 'integer'),
  ('tblemployee', 'brnid', 'integer'),
  ('tblemployee', 'typid', 'integer'),
  ('tblemployee', 'datejoin', 'timestamp without time zone'),
  ('tblemployee', 'dateconfirm', 'timestamp without time zone'),
  ('tblemployee', 'cduedate', 'timestamp without time zone'),
  ('tblemployee', 'extdate', 'timestamp without time zone'),
  ('tblemployee', 'jobdes', 'text'),
  ('tblemployee', 'active', 'integer'),
  ('tblemployee', 'jobid', 'character varying'),
  ('tblemployee', 'notepad', 'character varying'),
  ('tblemployee', 'reportto', 'integer'),
  ('tblemployee', 'dotted', 'integer'),
  ('tblemployee', 'medical', 'boolean'),
  ('tblemployee', 'gshift', 'boolean'),
  ('tblemployee', 'gross', 'numeric NOT NULL'),
  ('tblemployee', 'pdate', 'timestamp without time zone'),
  ('tblemployee', 'lastincdate', 'timestamp without time zone'),
  ('tblemployee', 'height', 'character varying'),
  ('tblemployee', 'weight', 'character varying'),
  ('tblemployee', 'eyecolor', 'character varying'),
  ('tblemployee', 'glass', 'character varying'),
  ('tblemployee', 'medicalhistory', 'character varying'),
  ('tblemployee', 'disabilities', 'smallint'),
  ('tblemployee', 'disreason', 'character varying'),
  ('tblemployee', 'pgid', 'integer'),
  ('tblemployee', 'relname', 'character varying'),
  ('tblemployee', 'relworking', 'character varying'),
  ('tblemployee', 'relationshipid', 'integer'),
  ('tblemployee', 'relpositionid', 'integer'),
  ('tblemployee', 'sectid', 'integer'),
  ('tblemployee', 'areaid', 'integer'),
  ('tblemployee', 'mtid', 'integer'),
  ('tblemployee', 'divid', 'integer'),
  ('tblemployee', 'uid', 'character varying'),
  ('tblemployee', 'pwd', 'character varying'),
  ('tblemployee', 'vdesigid', 'integer'),
  ('tblemployee', 'vdesigid2', 'integer'),
  ('tblemployee', 'baseid', 'integer'),
  ('tblemployee', 'regionid', 'integer'),
  ('tblemployee', 'mteamid', 'integer'),
  ('tblemployee', 'teamid', 'integer'),
  ('tblemployee', 'expyr', 'real'),
  ('tblemployee', 'editby', 'integer'),
  ('tblemployee', 'confirmby', 'integer'),
  ('tblemployee', 'hold', 'smallint'),
  ('tblemployee', 'isdirty', 'smallint'),
  ('tblemployee', 'dispid', 'integer'),
  ('tblemployee', 'identification', 'character varying'),
  ('tblemployee', 'sdwid', 'smallint'),
  ('tblemployee', 'domid', 'integer'),
  ('tblemployee', 'insurerid', 'integer'),
  ('tblemployee', 'grpinsrnum', 'character varying'),
  ('tblemployee', 'grpinsramt', 'numeric'),
  ('tblemployee', 'suminsured', 'numeric'),
  ('tblemployee', 'assetstatus', 'integer'),
  ('tblemployee', 'lreason', 'character varying'),
  ('tblemployee', 'shift', 'character(1)'),
  ('tblemployee', 'autopresent', 'boolean'),
  ('tblemployee', 'pgids', 'character varying'),
  ('tblemployee', 'countryid', 'integer'),
  ('tblemployee', 'offerdate', 'timestamp without time zone'),
  ('tblemployee', 'appid', 'integer'),
  ('tblemployee', 'vaccinationhistory', 'character varying'),
  ('tblemployee', 'contractexpiredate', 'timestamp without time zone'),
  ('tblemployee', 'proratedleave', 'smallint'),
  ('tblemployee', 'passportno', 'character varying'),
  ('tblemployee', 'fathernic', 'character varying'),
  ('tblemployee', 'dgid', 'integer'),
  ('tblemployee', 'dutyid', 'integer'),
  ('tblemployee', 'payrollstatus', 'integer'),
  ('tblemployee', 'slicamount', 'numeric'),
  ('tblemployee', 'totalslicamount', 'numeric'),
  ('tblemployee', 'opdid', 'integer'),
  ('tblemployee', 'email2', 'character varying'),
  ('tblemployee', 'emgmobile', 'character varying'),
  ('tblemployee', 'mdptidcurrent', 'integer'),
  ('tblemployee', 'mdptidold', 'integer'),
  ('tblemployee', 'dptidcurrent', 'integer'),
  ('tblemployee', 'dptidold', 'integer'),
  ('tblemployee', 'dsgidcurrent', 'integer'),
  ('tblemployee', 'dsgidold', 'integer'),
  ('tblemployee', 'jobidcurrent', 'integer'),
  ('tblemployee', 'pgidcurrent', 'integer'),
  ('tblemployee', 'reporttocurrent', 'integer'),
  ('tblemployee', 'cnicexpirydate', 'timestamp without time zone'),
  ('tblemployee', 'internexpirydate', 'timestamp without time zone'),
  ('tblemployee', 'hiringchecklistprocess', 'boolean'),
  ('tblemployee', 'oldempid', 'integer'),
  ('tblemployee', 'leavingdate', 'timestamp without time zone'),
  ('tblemployee', 'intercompanytransfer', 'boolean'),
  ('tblemployee', 'sourcecompanyid', 'integer'),
  ('tblemployee', 'targetcompanyid', 'integer'),
  ('tblemployee', 'originalcompanyid', 'integer'),
  ('tblemployee', 'sourceempid', 'integer'),
  ('tblemployee', 'targetempid', 'integer'),
  ('tblemployee', 'originalempid', 'integer'),
  ('tblemployee', 'intercompanyinduction', 'boolean'),
  ('tblemployee', 'lastworkingdate', 'timestamp without time zone'),
  ('tblemployee', 'reportingdate', 'timestamp without time zone'),
  ('tblemployee', 'basecntid', 'integer'),
  ('tblemployee', 'dispcntid', 'integer'),
  ('tblemployee', 'reporttocompany', 'integer'),
  ('tblemployee', 'relid', 'integer'),
  ('tblemployee', 'dottedreporttocompany', 'integer'),
  ('tblemployee', 'passportexpirydate', 'timestamp without time zone'),
  ('tblemployee', 'userid', 'character varying'),
  ('tblemployee', 'formid', 'character varying'),
  ('tblemployee', 'userempid', 'integer'),
  ('tblemployee', 'userempname', 'character varying'),
  ('tblemployee', 'entterminal', 'character varying'),
  ('tblemployee', 'entterminalip', 'character varying'),
  ('tblemployee', 'userempcode', 'character varying'),
  ('tblemployee', 'visanumer', 'character varying'),
  ('tblemployee', 'visaexpirydate', 'timestamp without time zone'),
  ('tblemployee', 'visanumber', 'character varying'),
  ('tblemployee', 'nextincdate', 'timestamp without time zone'),
  ('tblemployee', 'matchedwithblacklist', 'integer'),
  ('tblemployee', 'processmanualmap', 'integer'),
  ('tblemployee', 'duplicatedrecordid', 'integer'),
  ('tblemployee', 'duplicatedmapempid', 'integer'),
  ('tblemployee', 'idcardremarks', 'character varying'),
  ('tblemployee', 'familycardno', 'character varying'),
  ('tblemployee', 'iqamano', 'character varying'),
  ('tblemployee', 'iqamaexpiryhijri', 'timestamp without time zone'),
  ('tblemployee', 'iqamaexpirygregorian', 'timestamp without time zone'),
  ('tblemployee', 'currspnsname', 'character varying'),
  ('tblemployee', 'spnstransferable', 'integer'),
  ('tblemployee', 'spnstype', 'integer'),
  ('tblemployee', 'spnscountry', 'integer'),
  ('tblemployee', 'spnscity', 'integer'),
  ('tblemployee', 'spnscontactdetails', 'character varying'),
  ('tblemployee', 'spnsnatureofbusiness', 'character varying'),
  ('tblemployee', 'iqamaprofession', 'integer'),
  ('tblemployee', 'spnsexpiryhijri', 'timestamp without time zone'),
  ('tblemployee', 'spnsexpirygregorian', 'timestamp without time zone'),
  ('tblemployee', 'firstnamearabic', 'character varying'),
  ('tblemployee', 'midnamearabic', 'character varying'),
  ('tblemployee', 'lastnamearabic', 'character varying'),
  ('tblemployee', 'titleidarabic', 'integer'),
  ('tblemployee', 'validdrivinglicenseksa', 'integer'),
  ('tblemployee', 'hstofpersecution', 'character varying'),
  ('tblemployee', 'hstofpenalties', 'character varying'),
  ('tblemployee', 'pendingcases', 'character varying'),
  ('tblemployee', 'spnscategory', 'integer'),
  ('tblemployee', 'noofspnschangedofvisa', 'integer'),
  ('tblemployee', 'empcategoryid', 'integer'),
  ('tblemployee', 'autopresentfromdate', 'timestamp without time zone'),
  ('tblemployee', 'autopresenttodate', 'timestamp without time zone'),
  ('tblemployee', 'transactionsource', 'character varying'),
  ('tblemployee', 'salarychangedstatus', 'integer'),
  ('tblemployee', 'reviewtransactionid', 'integer'),
  ('tblemployee', 'hrseries', 'character varying'),
  ('tblemployee', 'isleaveallocated', 'boolean'),
  ('tblemployee', 'isemployeesalary', 'boolean'),
  ('tblemployee', 'ishiringchecklistfinalized', 'boolean'),
  ('tblemployee', 'isemployeeprofileextended', 'boolean'),
  ('tblemployee', 'isemployeeexpenseentitlement', 'boolean'),
  ('tblemployee', 'isemployeejd', 'boolean'),
  ('tblemployee', 'isapprovalforpayroll', 'boolean'),
  ('tblemployee', 'isuserid', 'boolean'),
  ('tblemployee', 'preferredculture', 'character varying'),
  ('tblemployee', 'domcntid', 'integer'),
  ('tblemployee', 'entdate', 'timestamp without time zone'),
  ('tblemployee', 'applicationid', 'character varying'),
  ('tblemployee', 'retirementdate', 'timestamp without time zone'),
  ('tblemployee', 'salaryreviewchangedstatus', 'integer'),
  ('tblemployee', 'timestamp', 'timestamp without time zone NOT NULL'),
  ('tblemployee', 'jobidold', 'integer'),
  ('tblemployee', 'pgidold', 'integer'),
  ('tblemployee', 'reporttoold', 'integer'),
  ('tblemployee', 'flexishift', 'boolean'),
  ('tblemployee', 'flexitype', 'integer'),
  ('tblemployee', 'requiredhours', 'numeric'),
  ('tblemployee', 'editbyuser', 'character varying'),
  ('tblemployee', 'secondment', 'boolean'),
  ('tblemployee', 'photopath', 'character varying'),
  ('tblemployee', 'companyshortname', 'character varying'),
  ('tblemployee', 'acids', 'character varying'),
  ('tblemployee', 'drivinglicenseno', 'character varying'),
  ('tblemployee', 'drivinglicenseexpirydate', 'timestamp without time zone'),
  ('tblemployee', 'isspouseemployed', 'boolean'),
  ('tblemployee', 'isanyotherincomesource', 'boolean'),
  ('tblemployee', 'isanyphysicaldisability', 'boolean'),
  ('tblemployee', 'otherincomesourcedetails', 'character varying'),
  ('tblemployee', 'physicaldisabilitydetails', 'character varying'),
  ('tblemployee', 'residentialstatusid', 'integer'),
  ('tblemployee', 'fatherhusbandphone', 'character varying'),
  ('tblemployee', 'fatherhusbandoccupation', 'character varying'),
  ('tblemployee', 'isownconveyance', 'boolean'),
  ('tblemployee', 'conveyancetype', 'integer'),
  ('tblemployee', 'conveyancemake', 'character varying'),
  ('tblemployee', 'conveyancemodel', 'character varying'),
  ('tblemployee', 'conveyanceyear', 'character varying'),
  ('tblemployee', 'conveyanceregisterationno', 'character varying'),
  ('tblemployee', 'phoneextension', 'character varying'),
  ('tblemployee', 'sittinglocation', 'character varying'),
  ('tblsetupsdetail', 'sdlid', 'integer NOT NULL'),
  ('tblsetupsdetail', 'code', 'character varying(100)'),
  ('tblsetupsdetail', 'smsid', 'integer'),
  ('tblsetupsdetail', 'name', 'character varying(255)'),
  ('tblsetupsdetail', 'lastupdate', 'timestamp without time zone'),
  ('tblsetupsdetail', 'companyid', 'integer'),
  ('tblsetupsdetail', 'userempid', 'integer'),
  ('tblsetupsdetail', 'entoperation', 'character varying(100)'),
  ('tblsetupsdetail', 'entdate', 'timestamp without time zone'),
  ('tblsetupsdetail', 'userempcode', 'character varying(100)'),
  ('tblsetupsdetail', 'entterminalip', 'character varying(100)'),
  ('tblsetupsdetail', 'entterminal', 'character varying(100)'),
  ('tblsetupsdetail', 'userempname', 'character varying(255)'),
  ('tblsetupsdetail', 'formid', 'character varying(100)'),
  ('tblsetupsdetail', 'userid', 'character varying(100)'),
  ('tblsetupsdetail', 'applicationid', 'character varying(100)'),
  ('tblsetupsdetail', 'chngtimestamp', 'bigint'),
  ('tblsetupsdetail', 'inactive', 'boolean'),
  ('templates', 'id', 'integer NOT NULL'),
  ('templates', 'companyid', 'integer NOT NULL'),
  ('templates', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('templates', 'templatename', 'character varying(200) NOT NULL'),
  ('templates', 'templatefileurl', 'character varying(500)'),
  ('templates', 'templatetype', 'integer NOT NULL'),
  ('templates', 'divisioncode', 'character varying(10)'),
  ('templates', 'departmentcode', 'character varying(20)'),
  ('templates', 'subdepartmentcode', 'character varying(30)'),
  ('templates', 'businessdomaincode', 'character varying(30)'),
  ('templates', 'isdefault', 'boolean DEFAULT true NOT NULL'),
  ('templates', 'templatecontent', 'text'),
  ('templates', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('templates', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('templates', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('templates', 'createdby', 'character varying(100) NOT NULL'),
  ('templates', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('templates', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('trainingpolicies', 'id', 'integer NOT NULL'),
  ('trainingpolicies', 'companyid', 'integer NOT NULL'),
  ('trainingpolicies', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('trainingpolicies', 'trainingrequired', 'boolean DEFAULT false NOT NULL'),
  ('trainingpolicies', 'minimumscore', 'integer'),
  ('trainingpolicies', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('trainingpolicies', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('trainingpolicies', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('trainingpolicies', 'createdby', 'character varying(100) NOT NULL'),
  ('trainingpolicies', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('trainingpolicies', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('transferscopepolicies', 'id', 'integer NOT NULL'),
  ('transferscopepolicies', 'companyid', 'integer NOT NULL'),
  ('transferscopepolicies', 'divisioncode', 'character varying(10) NOT NULL'),
  ('transferscopepolicies', 'reportingtolevel', 'integer NOT NULL'),
  ('transferscopepolicies', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('transferscopepolicies', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('transferscopepolicies', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('transferscopepolicies', 'createdby', 'character varying(100) NOT NULL'),
  ('transferscopepolicies', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('transferscopepolicies', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('transferworkflowpolicies', 'id', 'integer NOT NULL'),
  ('transferworkflowpolicies', 'companyid', 'integer NOT NULL'),
  ('transferworkflowpolicies', 'divisioncode', 'character varying(10) NOT NULL'),
  ('transferworkflowpolicies', 'approvalroleid', 'character varying(10) NOT NULL'),
  ('transferworkflowpolicies', 'approvaluserid', 'character varying(10) NOT NULL'),
  ('transferworkflowpolicies', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('transferworkflowpolicies', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('transferworkflowpolicies', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('transferworkflowpolicies', 'createdby', 'character varying(100) NOT NULL'),
  ('transferworkflowpolicies', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('transferworkflowpolicies', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('transferworkflowpolicies', 'approverempcode', 'character varying(10)'),
  ('uncontrolleddocumenthistory', 'id', 'integer NOT NULL'),
  ('uncontrolleddocumenthistory', 'companyid', 'integer NOT NULL'),
  ('uncontrolleddocumenthistory', 'uncontrolleddocumentid', 'integer NOT NULL'),
  ('uncontrolleddocumenthistory', 'previousdocumenturl', 'character varying(500)'),
  ('uncontrolleddocumenthistory', 'newdocumenturl', 'character varying(500) NOT NULL'),
  ('uncontrolleddocumenthistory', 'previousreviewdate', 'date'),
  ('uncontrolleddocumenthistory', 'newreviewdate', 'date NOT NULL'),
  ('uncontrolleddocumenthistory', 'reviewedby', 'character varying(100) NOT NULL'),
  ('uncontrolleddocumenthistory', 'reviewedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('uncontrolleddocuments', 'id', 'integer NOT NULL'),
  ('uncontrolleddocuments', 'companyid', 'integer NOT NULL'),
  ('uncontrolleddocuments', 'documentname', 'character varying(500) NOT NULL'),
  ('uncontrolleddocuments', 'documenturl', 'character varying(500) NOT NULL'),
  ('uncontrolleddocuments', 'reviewdate', 'date NOT NULL'),
  ('uncontrolleddocuments', 'reviewauthorityempcode', 'character varying(50) NOT NULL'),
  ('uncontrolleddocuments', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('uncontrolleddocuments', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('uncontrolleddocuments', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('uncontrolleddocuments', 'createdby', 'character varying(100) NOT NULL'),
  ('uncontrolleddocuments', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('uncontrolleddocuments', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('useraccesslevels', 'id', 'integer NOT NULL'),
  ('useraccesslevels', 'companyid', 'integer NOT NULL'),
  ('useraccesslevels', 'employeecode', 'character varying(20) NOT NULL'),
  ('useraccesslevels', 'divisioncode', 'character varying(10)'),
  ('useraccesslevels', 'departmentcode', 'character varying(20)'),
  ('useraccesslevels', 'subdepartmentcode', 'character varying(30)'),
  ('useraccesslevels', 'businessdomaincode', 'character varying(30)'),
  ('useraccesslevels', 'documenttypecode', 'character varying(10) NOT NULL'),
  ('useraccesslevels', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('useraccesslevels', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('useraccesslevels', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('useraccesslevels', 'createdby', 'character varying(100) NOT NULL'),
  ('useraccesslevels', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('useraccesslevels', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('userroles', 'companyid', 'bigint NOT NULL'),
  ('userroles', 'userid', 'character varying(100) NOT NULL'),
  ('userroles', 'roleid', 'bigint NOT NULL'),
  ('userroles', 'assignedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('userroles', 'assignedby', 'character varying(100) NOT NULL'),
  ('userroles', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('userroles', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('userroles', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('userroles', 'createdby', 'character varying(100) NOT NULL'),
  ('userroles', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('userroles', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('users', 'id', 'integer NOT NULL'),
  ('users', 'companyid', 'integer NOT NULL'),
  ('users', 'employeecode', 'character varying(20) NOT NULL'),
  ('users', 'employeename', 'character varying(100) NOT NULL'),
  ('users', 'email', 'character varying(100) NOT NULL'),
  ('users', 'divisioncode', 'character varying(10)'),
  ('users', 'departmentcode', 'character varying(20)'),
  ('users', 'subdepartmentcode', 'character varying(30)'),
  ('users', 'businessdomaincode', 'character varying(30)'),
  ('users', 'designationcode', 'character varying(30)'),
  ('users', 'reportingto', 'character varying(255) NOT NULL'),
  ('users', 'grade', 'character varying(20) NOT NULL'),
  ('users', 'dateofjoining', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('users', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('users', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('users', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('users', 'createdby', 'character varying(100) NOT NULL'),
  ('users', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('users', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('workflowpolicies', 'id', 'integer NOT NULL'),
  ('workflowpolicies', 'companyid', 'integer NOT NULL'),
  ('workflowpolicies', 'name', 'character varying(200) NOT NULL'),
  ('workflowpolicies', 'entitytype', 'character varying(50) NOT NULL'),
  ('workflowpolicies', 'divisioncode', 'character varying(10)'),
  ('workflowpolicies', 'departmentcode', 'character varying(20)'),
  ('workflowpolicies', 'subdepartmentcode', 'character varying(30)'),
  ('workflowpolicies', 'businessdomaincode', 'character varying(30)'),
  ('workflowpolicies', 'documenttypecode', 'character varying(10)'),
  ('workflowpolicies', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('workflowpolicies', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('workflowpolicies', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('workflowpolicies', 'createdby', 'character varying(100) NOT NULL'),
  ('workflowpolicies', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('workflowpolicies', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('workflowpolicyversions', 'companyid', 'bigint NOT NULL'),
  ('workflowpolicyversions', 'id', 'bigint NOT NULL'),
  ('workflowpolicyversions', 'workflowpolicyid', 'integer NOT NULL'),
  ('workflowpolicyversions', 'versionnumber', 'integer NOT NULL'),
  ('workflowpolicyversions', 'isactive', 'boolean DEFAULT true'),
  ('workflowpolicyversions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('workflowpolicyversions', 'createdby', 'character varying(100) NOT NULL'),
  ('workflowstepdefinitions', 'id', 'integer NOT NULL'),
  ('workflowstepdefinitions', 'companyid', 'integer NOT NULL'),
  ('workflowstepdefinitions', 'workflowpolicyversionid', 'integer NOT NULL'),
  ('workflowstepdefinitions', 'steporder', 'integer NOT NULL'),
  ('workflowstepdefinitions', 'stepgroup', 'integer DEFAULT 1'),
  ('workflowstepdefinitions', 'steptype', 'character varying(50) NOT NULL'),
  ('workflowstepdefinitions', 'roleid', 'integer'),
  ('workflowstepdefinitions', 'designationid', 'integer'),
  ('workflowstepdefinitions', 'userid', 'character varying(100)'),
  ('workflowstepdefinitions', 'requiresallapprovals', 'boolean DEFAULT true'),
  ('workflowstepdefinitions', 'isactive', 'boolean DEFAULT true NOT NULL'),
  ('workflowstepdefinitions', 'isdeleted', 'boolean DEFAULT false NOT NULL'),
  ('workflowstepdefinitions', 'createdat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('workflowstepdefinitions', 'createdby', 'character varying(100) NOT NULL'),
  ('workflowstepdefinitions', 'lastmodifiedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL'),
  ('workflowstepdefinitions', 'lastmodifiedby', 'character varying(100) NOT NULL'),
  ('workflowstepdefinitions', 'canaddadhocapprover', 'boolean DEFAULT false NOT NULL'),
  ('workflowexecutions', 'companyid', 'integer NOT NULL'),
  ('workflowexecutions', 'id', 'integer NOT NULL'),
  ('workflowexecutions', 'workflowpolicyversionid', 'integer NOT NULL'),
  ('workflowexecutions', 'entitytype', 'character varying(50) NOT NULL'),
  ('workflowexecutions', 'entityid', 'integer NOT NULL'),
  ('workflowexecutions', 'status', 'character varying(50) NOT NULL'),
  ('workflowexecutions', 'startedat', 'timestamp without time zone DEFAULT CURRENT_TIMESTAMP'),
  ('workflowexecutions', 'completedat', 'timestamp without time zone'),
  ('workflowexecutions', 'startedby', 'character varying(100) NOT NULL'),
  ('workflowexecutions', 'activitytypecode', 'character varying(20)'),
  ('workflowexecutionsteps', 'id', 'integer NOT NULL'),
  ('workflowexecutionsteps', 'companyid', 'integer NOT NULL'),
  ('workflowexecutionsteps', 'workflowexecutionid', 'integer NOT NULL'),
  ('workflowexecutionsteps', 'stepdefinitionid', 'integer NOT NULL'),
  ('workflowexecutionsteps', 'assigneduserid', 'character varying(100)'),
  ('workflowexecutionsteps', 'assignedroleid', 'integer'),
  ('workflowexecutionsteps', 'assigneddesignationid', 'integer'),
  ('workflowexecutionsteps', 'decision', 'character varying(50)'),
  ('workflowexecutionsteps', 'observation', 'text NOT NULL'),
  ('workflowexecutionsteps', 'actionat', 'timestamp without time zone'),
  ('workflowexecutionsteps', 'isactive', 'boolean DEFAULT false'),
  ('workflowexecutionsteps', 'steporder', 'integer'),
  ('workflowexecutionsteps', 'isadhoc', 'boolean DEFAULT false NOT NULL'),
  ('workflowexecutionsteps', 'addedby', 'character varying(50)'),
  ('workflowexecutionsteps', 'canaddadhocapprover', 'boolean DEFAULT false NOT NULL'),
  ('workflowsteptypes', 'id', 'integer NOT NULL'),
  ('workflowsteptypes', 'code', 'character varying(50)'),
  ('workflowsteptypes', 'name', 'character varying(100)')
  ) AS spec(tbl, col, def) LOOP
    v_notnull := r.def LIKE '% NOT NULL';
    v_def := regexp_replace(r.def, ' NOT NULL$', '');
    v_default := CASE WHEN v_def LIKE '% DEFAULT %' THEN substring(v_def FROM ' DEFAULT (.*)$') END;
    v_type := CASE WHEN v_def LIKE '% DEFAULT %' THEN substring(v_def FROM '^(.*?) DEFAULT ') ELSE v_def END;
    v_rel := format('public.%I', r.tbl)::regclass;

    IF NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped) THEN
      -- A NOT NULL column without a default cannot be added to a table that already has rows; it is added as nullable then.
      EXECUTE format('SELECT EXISTS (SELECT 1 FROM public.%I)', r.tbl) INTO v_has_rows;
      IF v_notnull AND v_default IS NULL AND v_has_rows THEN
        EXECUTE format('ALTER TABLE public.%I ADD COLUMN %I %s', r.tbl, r.col, v_def);
        INSERT INTO _sync_nullable VALUES (r.tbl, r.col);
        RAISE NOTICE 'Added column %.% as NULLABLE (it is NOT NULL in UAT, but this table already has rows)', r.tbl, r.col;
      ELSE
        EXECUTE format('ALTER TABLE public.%I ADD COLUMN %I %s', r.tbl, r.col, r.def);
        RAISE NOTICE 'Added column %.%', r.tbl, r.col;
      END IF;
    ELSE
      SELECT pg_catalog.format_type(a.atttypid, a.atttypmod), pg_get_expr(d.adbin, d.adrelid)
        INTO v_cur_type, v_cur_default
        FROM pg_attribute a
        LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
       WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped;
      -- whether the column may be empty: made the same as in UAT (a key column is never touched)
      SELECT a.attnotnull, EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = a.attrelid AND i.indisprimary AND a.attnum = ANY (i.indkey))
        INTO v_cur_notnull, v_is_pk
        FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped;
      IF NOT v_notnull AND v_cur_notnull AND NOT v_is_pk THEN
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I DROP NOT NULL', r.tbl, r.col);
        RAISE NOTICE 'Column %.% may be empty now (as in UAT)', r.tbl, r.col;
      ELSIF v_notnull AND NOT v_cur_notnull THEN
        BEGIN
          EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET NOT NULL', r.tbl, r.col);
          RAISE NOTICE 'Column %.% is NOT NULL now (as in UAT)', r.tbl, r.col;
        EXCEPTION WHEN OTHERS THEN
          RAISE NOTICE 'CHECK: column %.% is NOT NULL in UAT but has empty values here (left as it is)', r.tbl, r.col;
        END;
      END IF;
      IF v_default IS NOT NULL AND v_cur_default IS NULL AND NOT EXISTS (
           SELECT 1 FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND a.attidentity <> '') THEN
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET DEFAULT %s', r.tbl, r.col, v_default);
        RAISE NOTICE 'Restored the default of %.%', r.tbl, r.col;
      END IF;
      IF v_cur_type IS DISTINCT FROM v_type THEN
        -- Only a change that cannot lose data is made (a shorter text column made longer, smallint -> integer -> bigint).
        -- Anything else (a narrower type, another kind of type) is reported and left for you to decide.
        v_ok := (v_type = 'text' AND v_cur_type LIKE 'character%')
             OR (v_type ~ '^character varying\(\d+\)$' AND v_cur_type ~ '^character(?: varying)?\(\d+\)$'
                 AND substring(v_type FROM '\((\d+)\)')::int > substring(v_cur_type FROM '\((\d+)\)')::int)
             OR (v_cur_type = 'smallint' AND v_type IN ('integer', 'bigint'))
             OR (v_cur_type = 'integer' AND v_type = 'bigint');
        IF v_ok THEN
          BEGIN
            EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I TYPE %s', r.tbl, r.col, v_type);
            RAISE NOTICE 'Widened column %.% from "%" to "%"', r.tbl, r.col, v_cur_type, v_type;
          EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'CHECK: column %.% is "%" here but "%" in UAT and could not be changed: %', r.tbl, r.col, v_cur_type, v_type, SQLERRM;
          END;
        ELSE
          RAISE NOTICE 'CHECK: column %.% is "%" here but "%" in UAT (left as it is)', r.tbl, r.col, v_cur_type, v_type;
        END IF;
      END IF;
    END IF;
  END LOOP;
END $sync$;


-- ============================================================================
-- 3b. Sequences, identity columns, keys, indexes and foreign keys that are missing
-- ============================================================================

ALTER SEQUENCE public.businessdomains_id_seq OWNED BY public.businessdomains.id;

ALTER SEQUENCE public.cabinetstructuretabsconfig_id_seq OWNED BY public.cabinetstructuretabsconfig.id;

ALTER SEQUENCE public.companies_id_seq OWNED BY public.companies.id;

ALTER SEQUENCE public.controltypes_id_seq OWNED BY public.controltypes.id;

ALTER SEQUENCE public.departments_id_seq OWNED BY public.departments.id;

ALTER SEQUENCE public.designations_id_seq OWNED BY public.designations.id;

ALTER SEQUENCE public.distributiontypes_id_seq OWNED BY public.distributiontypes.id;

ALTER SEQUENCE public.divisions_id_seq OWNED BY public.divisions.id;

ALTER SEQUENCE public.documentattributevalues_id_seq OWNED BY public.documentattributevalues.id;

ALTER SEQUENCE public.documentevents_id_seq OWNED BY public.documentevents.id;

ALTER SEQUENCE public.documentrequesthistory_id_seq OWNED BY public.documentrequesthistory.id;

ALTER SEQUENCE public.documentrequesttypes_id_seq OWNED BY public.documentrequesttypes.id;

ALTER SEQUENCE public.documentreviewpolicies_id_seq OWNED BY public.documentreviewpolicies.id;

ALTER SEQUENCE public.documents_id_seq OWNED BY public.documents.id;

ALTER SEQUENCE public.documentstatehistory_id_seq OWNED BY public.documentstatehistory.id;

ALTER SEQUENCE public.documentstates_id_seq OWNED BY public.documentstates.id;

ALTER SEQUENCE public.documentstatetransitions_id_seq OWNED BY public.documentstatetransitions.id;

ALTER SEQUENCE public.documenttrainingauthorizations_id_seq OWNED BY public.documenttrainingauthorizations.id;

ALTER SEQUENCE public.documenttypes_id_seq OWNED BY public.documenttypes.id;

ALTER SEQUENCE public.documentversions_id_seq OWNED BY public.documentversions.id;

ALTER SEQUENCE public.emailoutbox_id_seq OWNED BY public.emailoutbox.id;

ALTER SEQUENCE public.orgunits_id_seq OWNED BY public.orgunits.id;

ALTER SEQUENCE public.policyassignments_id_seq OWNED BY public.policyassignments.id;

ALTER SEQUENCE public.rolecabinetmappings_id_seq OWNED BY public.rolecabinetmappings.id;

ALTER SEQUENCE public.roles_id_seq OWNED BY public.roles.id;

ALTER SEQUENCE public.subdepartments_id_seq OWNED BY public.subdepartments.id;

ALTER SEQUENCE public.uncontrolleddocumenthistory_id_seq OWNED BY public.uncontrolleddocumenthistory.id;

ALTER SEQUENCE public.uncontrolleddocuments_id_seq OWNED BY public.uncontrolleddocuments.id;

ALTER SEQUENCE public.useraccesslevels_id_seq OWNED BY public.useraccesslevels.id;

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;

ALTER SEQUENCE public.workflowexecutions_id_seq OWNED BY public.workflowexecutions.id;

ALTER SEQUENCE public.workflowpolicies_id_seq OWNED BY public.workflowpolicies.id;

ALTER SEQUENCE public.workflowpolicyversions_id_seq OWNED BY public.workflowpolicyversions.id;

ALTER SEQUENCE public.workflowsteptypes_id_seq OWNED BY public.workflowsteptypes.id;

ALTER TABLE ONLY public.businessdomains ALTER COLUMN id SET DEFAULT nextval('public.businessdomains_id_seq'::regclass);

ALTER TABLE ONLY public.cabinetstructuretabsconfig ALTER COLUMN id SET DEFAULT nextval('public.cabinetstructuretabsconfig_id_seq'::regclass);

ALTER TABLE ONLY public.companies ALTER COLUMN id SET DEFAULT nextval('public.companies_id_seq'::regclass);

ALTER TABLE ONLY public.controltypes ALTER COLUMN id SET DEFAULT nextval('public.controltypes_id_seq'::regclass);

ALTER TABLE ONLY public.departments ALTER COLUMN id SET DEFAULT nextval('public.departments_id_seq'::regclass);

ALTER TABLE ONLY public.designations ALTER COLUMN id SET DEFAULT nextval('public.designations_id_seq'::regclass);

ALTER TABLE ONLY public.distributiontypes ALTER COLUMN id SET DEFAULT nextval('public.distributiontypes_id_seq'::regclass);

ALTER TABLE ONLY public.divisions ALTER COLUMN id SET DEFAULT nextval('public.divisions_id_seq'::regclass);

ALTER TABLE ONLY public.documentattributevalues ALTER COLUMN id SET DEFAULT nextval('public.documentattributevalues_id_seq'::regclass);

ALTER TABLE ONLY public.documentevents ALTER COLUMN id SET DEFAULT nextval('public.documentevents_id_seq'::regclass);

ALTER TABLE ONLY public.documentrequesthistory ALTER COLUMN id SET DEFAULT nextval('public.documentrequesthistory_id_seq'::regclass);

ALTER TABLE ONLY public.documentrequesttypes ALTER COLUMN id SET DEFAULT nextval('public.documentrequesttypes_id_seq'::regclass);

ALTER TABLE ONLY public.documentreviewpolicies ALTER COLUMN id SET DEFAULT nextval('public.documentreviewpolicies_id_seq'::regclass);

ALTER TABLE ONLY public.documents ALTER COLUMN id SET DEFAULT nextval('public.documents_id_seq'::regclass);

ALTER TABLE ONLY public.documentstatehistory ALTER COLUMN id SET DEFAULT nextval('public.documentstatehistory_id_seq'::regclass);

ALTER TABLE ONLY public.documentstates ALTER COLUMN id SET DEFAULT nextval('public.documentstates_id_seq'::regclass);

ALTER TABLE ONLY public.documentstatetransitions ALTER COLUMN id SET DEFAULT nextval('public.documentstatetransitions_id_seq'::regclass);

ALTER TABLE ONLY public.documenttrainingauthorizations ALTER COLUMN id SET DEFAULT nextval('public.documenttrainingauthorizations_id_seq'::regclass);

ALTER TABLE ONLY public.documenttypes ALTER COLUMN id SET DEFAULT nextval('public.documenttypes_id_seq'::regclass);

ALTER TABLE ONLY public.documentversions ALTER COLUMN id SET DEFAULT nextval('public.documentversions_id_seq'::regclass);

ALTER TABLE ONLY public.emailoutbox ALTER COLUMN id SET DEFAULT nextval('public.emailoutbox_id_seq'::regclass);

ALTER TABLE ONLY public.orgunits ALTER COLUMN id SET DEFAULT nextval('public.orgunits_id_seq'::regclass);

ALTER TABLE ONLY public.policyassignments ALTER COLUMN id SET DEFAULT nextval('public.policyassignments_id_seq'::regclass);

ALTER TABLE ONLY public.rolecabinetmappings ALTER COLUMN id SET DEFAULT nextval('public.rolecabinetmappings_id_seq'::regclass);

ALTER TABLE ONLY public.roles ALTER COLUMN id SET DEFAULT nextval('public.roles_id_seq'::regclass);

ALTER TABLE ONLY public.subdepartments ALTER COLUMN id SET DEFAULT nextval('public.subdepartments_id_seq'::regclass);

ALTER TABLE ONLY public.uncontrolleddocumenthistory ALTER COLUMN id SET DEFAULT nextval('public.uncontrolleddocumenthistory_id_seq'::regclass);

ALTER TABLE ONLY public.uncontrolleddocuments ALTER COLUMN id SET DEFAULT nextval('public.uncontrolleddocuments_id_seq'::regclass);

ALTER TABLE ONLY public.useraccesslevels ALTER COLUMN id SET DEFAULT nextval('public.useraccesslevels_id_seq'::regclass);

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);

ALTER TABLE ONLY public.workflowexecutions ALTER COLUMN id SET DEFAULT nextval('public.workflowexecutions_id_seq'::regclass);

ALTER TABLE ONLY public.workflowpolicies ALTER COLUMN id SET DEFAULT nextval('public.workflowpolicies_id_seq'::regclass);

ALTER TABLE ONLY public.workflowpolicyversions ALTER COLUMN id SET DEFAULT nextval('public.workflowpolicyversions_id_seq'::regclass);

ALTER TABLE ONLY public.workflowsteptypes ALTER COLUMN id SET DEFAULT nextval('public.workflowsteptypes_id_seq'::regclass);

DO $sync$
DECLARE r record; v_seq text; v_max bigint; v_last bigint;
BEGIN
  FOR r IN SELECT * FROM (VALUES
    ('businessdomains', 'id'),
    ('cabinetstructuretabsconfig', 'id'),
    ('companies', 'id'),
    ('controltypes', 'id'),
    ('departments', 'id'),
    ('designations', 'id'),
    ('distributiontypes', 'id'),
    ('divisions', 'id'),
    ('documentattributevalues', 'id'),
    ('documentevents', 'id'),
    ('documentrequesthistory', 'id'),
    ('documentrequesttypes', 'id'),
    ('documentreviewpolicies', 'id'),
    ('documents', 'id'),
    ('documentstatehistory', 'id'),
    ('documentstates', 'id'),
    ('documentstatetransitions', 'id'),
    ('documenttrainingauthorizations', 'id'),
    ('documenttypes', 'id'),
    ('documentversions', 'id'),
    ('emailoutbox', 'id'),
    ('orgunits', 'id'),
    ('policyassignments', 'id'),
    ('rolecabinetmappings', 'id'),
    ('roles', 'id'),
    ('subdepartments', 'id'),
    ('uncontrolleddocumenthistory', 'id'),
    ('uncontrolleddocuments', 'id'),
    ('useraccesslevels', 'id'),
    ('users', 'id'),
    ('workflowexecutions', 'id'),
    ('workflowpolicies', 'id'),
    ('workflowpolicyversions', 'id'),
    ('workflowsteptypes', 'id')
  ) AS s(tbl, col) LOOP
    v_seq := pg_get_serial_sequence('public.' || r.tbl, r.col);
    CONTINUE WHEN v_seq IS NULL;
    EXECUTE format('SELECT MAX(%I) FROM public.%I', r.col, r.tbl) INTO v_max;
    EXECUTE format('SELECT last_value FROM %s', v_seq) INTO v_last;
    IF v_max IS NOT NULL AND v_max > v_last THEN
      PERFORM setval(v_seq, v_max, true);
      RAISE NOTICE 'Moved sequence % up to % (the table already had higher ids)', v_seq, v_max;
    END IF;
  END LOOP;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.alert_configs'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.alert_configs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.alert_configs_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'alert_configs') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.alert_configs', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of alert_configs.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.alert_document_scopes'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.alert_document_scopes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.alert_document_scopes_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'alert_document_scopes') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.alert_document_scopes', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of alert_document_scopes.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.alert_recipients'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.alert_recipients ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.alert_recipients_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'alert_recipients') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.alert_recipients', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of alert_recipients.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.attributemandatoryscopes'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.attributemandatoryscopes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.attributemandatoryscopes_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'attributemandatoryscopes') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.attributemandatoryscopes', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of attributemandatoryscopes.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.auditlogs'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.auditlogs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.auditlogs_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'auditlogs') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.auditlogs', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of auditlogs.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentadhocapprovers'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentadhocapprovers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentadhocapprovers_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentadhocapprovers') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentadhocapprovers', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentadhocapprovers.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentattributes'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentattributes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentattributes_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentattributes') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentattributes', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentattributes.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrequestroledistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrequestroledistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentrequestroledistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrequestroledistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrequestroledistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrequestroledistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrequests'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrequests ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentrequests_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrequests') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrequests', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrequests.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrequestuserdistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrequestuserdistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentrequestuserdistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrequestuserdistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrequestuserdistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrequestuserdistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrevisiondistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrevisiondistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.documentrevisiondistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrevisiondistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrevisiondistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrevisiondistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrevisionroledistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrevisionroledistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.documentrevisionroledistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrevisionroledistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrevisionroledistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrevisionroledistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentrevisionuserdistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentrevisionuserdistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.documentrevisionuserdistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentrevisionuserdistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentrevisionuserdistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentrevisionuserdistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentroledistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentroledistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentroledistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentroledistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentroledistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentroledistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documenttraining'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documenttraining ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documenttraining_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documenttraining') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documenttraining', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documenttraining.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentuserdistributions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentuserdistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentuserdistributions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentuserdistributions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentuserdistributions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentuserdistributions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.documentusertraining'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.documentusertraining ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.documentusertraining_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'documentusertraining') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.documentusertraining', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of documentusertraining.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.employeedraftobservation'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.employeedraftobservation ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.employeedraftobservation_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'employeedraftobservation') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.employeedraftobservation', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of employeedraftobservation.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.esignatures'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.esignatures ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.esignatures_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'esignatures') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.esignatures', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of esignatures.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.notifications'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.notifications ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.notifications_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'notifications') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.notifications', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of notifications.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.responsibilitytransfers'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.responsibilitytransfers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.responsibilitytransfers_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'responsibilitytransfers') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.responsibilitytransfers', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of responsibilitytransfers.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.tblempjobprofile'::regclass AND a.attname = 'jobprofileid' AND a.attidentity = '') THEN
    ALTER TABLE public.tblempjobprofile ALTER COLUMN jobprofileid ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.tblempjobprofile_jobprofileid_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'jobprofileid', 'tblempjobprofile') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.tblempjobprofile', 'jobprofileid'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of tblempjobprofile.jobprofileid was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.templates'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.templates ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.templates_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'templates') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.templates', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of templates.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.trainingpolicies'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.trainingpolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.trainingpolicies_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'trainingpolicies') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.trainingpolicies', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of trainingpolicies.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.transferscopepolicies'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.transferscopepolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.transferscopepolicies_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'transferscopepolicies') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.transferscopepolicies', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of transferscopepolicies.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.transferworkflowpolicies'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.transferworkflowpolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
        SEQUENCE NAME public.transferworkflowpolicies_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'transferworkflowpolicies') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.transferworkflowpolicies', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of transferworkflowpolicies.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.workflowexecutionsteps'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.workflowexecutionsteps ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.workflowexecutionsteps_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'workflowexecutionsteps') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.workflowexecutionsteps', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of workflowexecutionsteps.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.workflowstepdefinitions'::regclass AND a.attname = 'id' AND a.attidentity = '') THEN
    ALTER TABLE public.workflowstepdefinitions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
        SEQUENCE NAME public.workflowstepdefinitions_id_seq
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        CACHE 1
    );
    EXECUTE format('SELECT MAX(%I) FROM public.%I', 'id', 'workflowstepdefinitions') INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.workflowstepdefinitions', 'id'), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of workflowstepdefinitions.id was not set: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_configs_pkey' AND conrelid = 'public.alert_configs'::regclass) THEN
    ALTER TABLE ONLY public.alert_configs
        ADD CONSTRAINT alert_configs_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint alert_configs_pkey on alert_configs';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint alert_configs_pkey on alert_configs was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_document_scopes_pkey' AND conrelid = 'public.alert_document_scopes'::regclass) THEN
    ALTER TABLE ONLY public.alert_document_scopes
        ADD CONSTRAINT alert_document_scopes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint alert_document_scopes_pkey on alert_document_scopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint alert_document_scopes_pkey on alert_document_scopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_recipients_pkey' AND conrelid = 'public.alert_recipients'::regclass) THEN
    ALTER TABLE ONLY public.alert_recipients
        ADD CONSTRAINT alert_recipients_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint alert_recipients_pkey on alert_recipients';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint alert_recipients_pkey on alert_recipients was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'attributemandatoryscopes_pkey' AND conrelid = 'public.attributemandatoryscopes'::regclass) THEN
    ALTER TABLE ONLY public.attributemandatoryscopes
        ADD CONSTRAINT attributemandatoryscopes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint attributemandatoryscopes_pkey on attributemandatoryscopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint attributemandatoryscopes_pkey on attributemandatoryscopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'auditlogs_pkey' AND conrelid = 'public.auditlogs'::regclass) THEN
    ALTER TABLE ONLY public.auditlogs
        ADD CONSTRAINT auditlogs_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint auditlogs_pkey on auditlogs';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint auditlogs_pkey on auditlogs was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'businessdomains_pkey' AND conrelid = 'public.businessdomains'::regclass) THEN
    ALTER TABLE ONLY public.businessdomains
        ADD CONSTRAINT businessdomains_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint businessdomains_pkey on businessdomains';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint businessdomains_pkey on businessdomains was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'cabinetstructuretabsconfig_pkey' AND conrelid = 'public.cabinetstructuretabsconfig'::regclass) THEN
    ALTER TABLE ONLY public.cabinetstructuretabsconfig
        ADD CONSTRAINT cabinetstructuretabsconfig_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint cabinetstructuretabsconfig_pkey on cabinetstructuretabsconfig';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint cabinetstructuretabsconfig_pkey on cabinetstructuretabsconfig was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'companies_code_key' AND conrelid = 'public.companies'::regclass) THEN
    ALTER TABLE ONLY public.companies
        ADD CONSTRAINT companies_code_key UNIQUE (code);
    RAISE NOTICE 'Added key/constraint companies_code_key on companies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint companies_code_key on companies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'companies_pkey' AND conrelid = 'public.companies'::regclass) THEN
    ALTER TABLE ONLY public.companies
        ADD CONSTRAINT companies_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint companies_pkey on companies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint companies_pkey on companies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'controltypes_pkey' AND conrelid = 'public.controltypes'::regclass) THEN
    ALTER TABLE ONLY public.controltypes
        ADD CONSTRAINT controltypes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint controltypes_pkey on controltypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint controltypes_pkey on controltypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'departments_pkey' AND conrelid = 'public.departments'::regclass) THEN
    ALTER TABLE ONLY public.departments
        ADD CONSTRAINT departments_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint departments_pkey on departments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint departments_pkey on departments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'designations_pkey' AND conrelid = 'public.designations'::regclass) THEN
    ALTER TABLE ONLY public.designations
        ADD CONSTRAINT designations_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint designations_pkey on designations';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint designations_pkey on designations was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'distributiontypes_pkey' AND conrelid = 'public.distributiontypes'::regclass) THEN
    ALTER TABLE ONLY public.distributiontypes
        ADD CONSTRAINT distributiontypes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint distributiontypes_pkey on distributiontypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint distributiontypes_pkey on distributiontypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'divisions_pkey' AND conrelid = 'public.divisions'::regclass) THEN
    ALTER TABLE ONLY public.divisions
        ADD CONSTRAINT divisions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint divisions_pkey on divisions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint divisions_pkey on divisions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentadhocapprovers_pkey' AND conrelid = 'public.documentadhocapprovers'::regclass) THEN
    ALTER TABLE ONLY public.documentadhocapprovers
        ADD CONSTRAINT documentadhocapprovers_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentadhocapprovers_pkey on documentadhocapprovers';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentadhocapprovers_pkey on documentadhocapprovers was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentattributes_pkey' AND conrelid = 'public.documentattributes'::regclass) THEN
    ALTER TABLE ONLY public.documentattributes
        ADD CONSTRAINT documentattributes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentattributes_pkey on documentattributes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentattributes_pkey on documentattributes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentattributevalues_pkey' AND conrelid = 'public.documentattributevalues'::regclass) THEN
    ALTER TABLE ONLY public.documentattributevalues
        ADD CONSTRAINT documentattributevalues_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentattributevalues_pkey on documentattributevalues';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentattributevalues_pkey on documentattributevalues was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentevents_pkey' AND conrelid = 'public.documentevents'::regclass) THEN
    ALTER TABLE ONLY public.documentevents
        ADD CONSTRAINT documentevents_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documentevents_pkey on documentevents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentevents_pkey on documentevents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrequesthistory_pkey' AND conrelid = 'public.documentrequesthistory'::regclass) THEN
    ALTER TABLE ONLY public.documentrequesthistory
        ADD CONSTRAINT documentrequesthistory_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documentrequesthistory_pkey on documentrequesthistory';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrequesthistory_pkey on documentrequesthistory was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrequestroledistributions_pkey' AND conrelid = 'public.documentrequestroledistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestroledistributions
        ADD CONSTRAINT documentrequestroledistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrequestroledistributions_pkey on documentrequestroledistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrequestroledistributions_pkey on documentrequestroledistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrequests_pkey' AND conrelid = 'public.documentrequests'::regclass) THEN
    ALTER TABLE ONLY public.documentrequests
        ADD CONSTRAINT documentrequests_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrequests_pkey on documentrequests';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrequests_pkey on documentrequests was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrequesttypes_pkey' AND conrelid = 'public.documentrequesttypes'::regclass) THEN
    ALTER TABLE ONLY public.documentrequesttypes
        ADD CONSTRAINT documentrequesttypes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrequesttypes_pkey on documentrequesttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrequesttypes_pkey on documentrequesttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrequestuserdistributions_pkey' AND conrelid = 'public.documentrequestuserdistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestuserdistributions
        ADD CONSTRAINT documentrequestuserdistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrequestuserdistributions_pkey on documentrequestuserdistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrequestuserdistributions_pkey on documentrequestuserdistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentreviewpolicies_pkey' AND conrelid = 'public.documentreviewpolicies'::regclass) THEN
    ALTER TABLE ONLY public.documentreviewpolicies
        ADD CONSTRAINT documentreviewpolicies_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentreviewpolicies_pkey on documentreviewpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentreviewpolicies_pkey on documentreviewpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrevisiondistributions_pkey' AND conrelid = 'public.documentrevisiondistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrevisiondistributions
        ADD CONSTRAINT documentrevisiondistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrevisiondistributions_pkey on documentrevisiondistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrevisiondistributions_pkey on documentrevisiondistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrevisionroledistributions_pkey' AND conrelid = 'public.documentrevisionroledistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrevisionroledistributions
        ADD CONSTRAINT documentrevisionroledistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrevisionroledistributions_pkey on documentrevisionroledistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrevisionroledistributions_pkey on documentrevisionroledistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentrevisionuserdistributions_pkey' AND conrelid = 'public.documentrevisionuserdistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrevisionuserdistributions
        ADD CONSTRAINT documentrevisionuserdistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentrevisionuserdistributions_pkey on documentrevisionuserdistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentrevisionuserdistributions_pkey on documentrevisionuserdistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentroledistributions_pkey' AND conrelid = 'public.documentroledistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentroledistributions
        ADD CONSTRAINT documentroledistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentroledistributions_pkey on documentroledistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentroledistributions_pkey on documentroledistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documents_pkey' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT documents_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documents_pkey on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documents_pkey on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentstatehistory_pkey' AND conrelid = 'public.documentstatehistory'::regclass) THEN
    ALTER TABLE ONLY public.documentstatehistory
        ADD CONSTRAINT documentstatehistory_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documentstatehistory_pkey on documentstatehistory';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentstatehistory_pkey on documentstatehistory was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentstates_code_key' AND conrelid = 'public.documentstates'::regclass) THEN
    ALTER TABLE ONLY public.documentstates
        ADD CONSTRAINT documentstates_code_key UNIQUE (code);
    RAISE NOTICE 'Added key/constraint documentstates_code_key on documentstates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentstates_code_key on documentstates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentstates_pkey' AND conrelid = 'public.documentstates'::regclass) THEN
    ALTER TABLE ONLY public.documentstates
        ADD CONSTRAINT documentstates_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentstates_pkey on documentstates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentstates_pkey on documentstates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentstatetransitions_pkey' AND conrelid = 'public.documentstatetransitions'::regclass) THEN
    ALTER TABLE ONLY public.documentstatetransitions
        ADD CONSTRAINT documentstatetransitions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentstatetransitions_pkey on documentstatetransitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentstatetransitions_pkey on documentstatetransitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documenttraining_pkey' AND conrelid = 'public.documenttraining'::regclass) THEN
    ALTER TABLE ONLY public.documenttraining
        ADD CONSTRAINT documenttraining_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documenttraining_pkey on documenttraining';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documenttraining_pkey on documenttraining was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documenttrainingauthorizations_pkey' AND conrelid = 'public.documenttrainingauthorizations'::regclass) THEN
    ALTER TABLE ONLY public.documenttrainingauthorizations
        ADD CONSTRAINT documenttrainingauthorizations_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documenttrainingauthorizations_pkey on documenttrainingauthorizations';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documenttrainingauthorizations_pkey on documenttrainingauthorizations was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documenttypes_pkey' AND conrelid = 'public.documenttypes'::regclass) THEN
    ALTER TABLE ONLY public.documenttypes
        ADD CONSTRAINT documenttypes_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documenttypes_pkey on documenttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documenttypes_pkey on documenttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentuserdistributions_pkey' AND conrelid = 'public.documentuserdistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentuserdistributions
        ADD CONSTRAINT documentuserdistributions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentuserdistributions_pkey on documentuserdistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentuserdistributions_pkey on documentuserdistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentusertraining_pkey' AND conrelid = 'public.documentusertraining'::regclass) THEN
    ALTER TABLE ONLY public.documentusertraining
        ADD CONSTRAINT documentusertraining_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint documentusertraining_pkey on documentusertraining';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentusertraining_pkey on documentusertraining was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'documentversions_pkey' AND conrelid = 'public.documentversions'::regclass) THEN
    ALTER TABLE ONLY public.documentversions
        ADD CONSTRAINT documentversions_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint documentversions_pkey on documentversions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint documentversions_pkey on documentversions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'emailoutbox_pkey' AND conrelid = 'public.emailoutbox'::regclass) THEN
    ALTER TABLE ONLY public.emailoutbox
        ADD CONSTRAINT emailoutbox_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint emailoutbox_pkey on emailoutbox';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint emailoutbox_pkey on emailoutbox was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'employeedraftobservation_pkey' AND conrelid = 'public.employeedraftobservation'::regclass) THEN
    ALTER TABLE ONLY public.employeedraftobservation
        ADD CONSTRAINT employeedraftobservation_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint employeedraftobservation_pkey on employeedraftobservation';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint employeedraftobservation_pkey on employeedraftobservation was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'esignatures_pkey' AND conrelid = 'public.esignatures'::regclass) THEN
    ALTER TABLE ONLY public.esignatures
        ADD CONSTRAINT esignatures_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint esignatures_pkey on esignatures';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint esignatures_pkey on esignatures was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'notifications_pkey' AND conrelid = 'public.notifications'::regclass) THEN
    ALTER TABLE ONLY public.notifications
        ADD CONSTRAINT notifications_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint notifications_pkey on notifications';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint notifications_pkey on notifications was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'orgunits_companyid_code_key' AND conrelid = 'public.orgunits'::regclass) THEN
    ALTER TABLE ONLY public.orgunits
        ADD CONSTRAINT orgunits_companyid_code_key UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint orgunits_companyid_code_key on orgunits';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint orgunits_companyid_code_key on orgunits was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'orgunits_pkey' AND conrelid = 'public.orgunits'::regclass) THEN
    ALTER TABLE ONLY public.orgunits
        ADD CONSTRAINT orgunits_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint orgunits_pkey on orgunits';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint orgunits_pkey on orgunits was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'policyassignments_pkey' AND conrelid = 'public.policyassignments'::regclass) THEN
    ALTER TABLE ONLY public.policyassignments
        ADD CONSTRAINT policyassignments_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint policyassignments_pkey on policyassignments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint policyassignments_pkey on policyassignments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'responsibilitytransfers_pkey' AND conrelid = 'public.responsibilitytransfers'::regclass) THEN
    ALTER TABLE ONLY public.responsibilitytransfers
        ADD CONSTRAINT responsibilitytransfers_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint responsibilitytransfers_pkey on responsibilitytransfers';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint responsibilitytransfers_pkey on responsibilitytransfers was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'rolecabinetmappings_pkey' AND conrelid = 'public.rolecabinetmappings'::regclass) THEN
    ALTER TABLE ONLY public.rolecabinetmappings
        ADD CONSTRAINT rolecabinetmappings_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint rolecabinetmappings_pkey on rolecabinetmappings';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint rolecabinetmappings_pkey on rolecabinetmappings was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'roles_pkey' AND conrelid = 'public.roles'::regclass) THEN
    ALTER TABLE ONLY public.roles
        ADD CONSTRAINT roles_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint roles_pkey on roles';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint roles_pkey on roles was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'subdepartments_pkey' AND conrelid = 'public.subdepartments'::regclass) THEN
    ALTER TABLE ONLY public.subdepartments
        ADD CONSTRAINT subdepartments_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint subdepartments_pkey on subdepartments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint subdepartments_pkey on subdepartments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tbldeptstrdetail_dptdid' AND conrelid = 'public.tbldeptstrdetail'::regclass) THEN
    ALTER TABLE ONLY public.tbldeptstrdetail
        ADD CONSTRAINT tbldeptstrdetail_dptdid PRIMARY KEY (dptdid);
    RAISE NOTICE 'Added key/constraint tbldeptstrdetail_dptdid on tbldeptstrdetail';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint tbldeptstrdetail_dptdid on tbldeptstrdetail was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tbldeptstrmaster_dptmid' AND conrelid = 'public.tbldeptstrmaster'::regclass) THEN
    ALTER TABLE ONLY public.tbldeptstrmaster
        ADD CONSTRAINT tbldeptstrmaster_dptmid PRIMARY KEY (dptmid);
    RAISE NOTICE 'Added key/constraint tbldeptstrmaster_dptmid on tbldeptstrmaster';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint tbldeptstrmaster_dptmid on tbldeptstrmaster was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tblempjobprofile_pkey' AND conrelid = 'public.tblempjobprofile'::regclass) THEN
    ALTER TABLE ONLY public.tblempjobprofile
        ADD CONSTRAINT tblempjobprofile_pkey PRIMARY KEY (jobprofileid);
    RAISE NOTICE 'Added key/constraint tblempjobprofile_pkey on tblempjobprofile';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint tblempjobprofile_pkey on tblempjobprofile was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tblemployee_empid' AND conrelid = 'public.tblemployee'::regclass) THEN
    ALTER TABLE ONLY public.tblemployee
        ADD CONSTRAINT tblemployee_empid PRIMARY KEY (empid);
    RAISE NOTICE 'Added key/constraint tblemployee_empid on tblemployee';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint tblemployee_empid on tblemployee was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'tblsetupsdetail_sdlid' AND conrelid = 'public.tblsetupsdetail'::regclass) THEN
    ALTER TABLE ONLY public.tblsetupsdetail
        ADD CONSTRAINT tblsetupsdetail_sdlid PRIMARY KEY (sdlid);
    RAISE NOTICE 'Added key/constraint tblsetupsdetail_sdlid on tblsetupsdetail';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint tblsetupsdetail_sdlid on tblsetupsdetail was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'templates_pkey' AND conrelid = 'public.templates'::regclass) THEN
    ALTER TABLE ONLY public.templates
        ADD CONSTRAINT templates_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint templates_pkey on templates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint templates_pkey on templates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'trainingpolicies_pkey' AND conrelid = 'public.trainingpolicies'::regclass) THEN
    ALTER TABLE ONLY public.trainingpolicies
        ADD CONSTRAINT trainingpolicies_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint trainingpolicies_pkey on trainingpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint trainingpolicies_pkey on trainingpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'transferscopepolicies_pkey' AND conrelid = 'public.transferscopepolicies'::regclass) THEN
    ALTER TABLE ONLY public.transferscopepolicies
        ADD CONSTRAINT transferscopepolicies_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint transferscopepolicies_pkey on transferscopepolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint transferscopepolicies_pkey on transferscopepolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'transferworkflowpolicies_pkey' AND conrelid = 'public.transferworkflowpolicies'::regclass) THEN
    ALTER TABLE ONLY public.transferworkflowpolicies
        ADD CONSTRAINT transferworkflowpolicies_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint transferworkflowpolicies_pkey on transferworkflowpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint transferworkflowpolicies_pkey on transferworkflowpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uncontrolleddocumenthistory_pkey' AND conrelid = 'public.uncontrolleddocumenthistory'::regclass) THEN
    ALTER TABLE ONLY public.uncontrolleddocumenthistory
        ADD CONSTRAINT uncontrolleddocumenthistory_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint uncontrolleddocumenthistory_pkey on uncontrolleddocumenthistory';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uncontrolleddocumenthistory_pkey on uncontrolleddocumenthistory was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uncontrolleddocuments_pkey' AND conrelid = 'public.uncontrolleddocuments'::regclass) THEN
    ALTER TABLE ONLY public.uncontrolleddocuments
        ADD CONSTRAINT uncontrolleddocuments_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint uncontrolleddocuments_pkey on uncontrolleddocuments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uncontrolleddocuments_pkey on uncontrolleddocuments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_businessdomain_company_code' AND conrelid = 'public.businessdomains'::regclass) THEN
    ALTER TABLE ONLY public.businessdomains
        ADD CONSTRAINT uq_businessdomain_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_businessdomain_company_code on businessdomains';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_businessdomain_company_code on businessdomains was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_cabinetstructuretabsconfig_company_name' AND conrelid = 'public.cabinetstructuretabsconfig'::regclass) THEN
    ALTER TABLE ONLY public.cabinetstructuretabsconfig
        ADD CONSTRAINT uq_cabinetstructuretabsconfig_company_name UNIQUE (companyid, name);
    RAISE NOTICE 'Added key/constraint uq_cabinetstructuretabsconfig_company_name on cabinetstructuretabsconfig';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_cabinetstructuretabsconfig_company_name on cabinetstructuretabsconfig was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_departments_company_code' AND conrelid = 'public.departments'::regclass) THEN
    ALTER TABLE ONLY public.departments
        ADD CONSTRAINT uq_departments_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_departments_company_code on departments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_departments_company_code on departments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_designations_company_code' AND conrelid = 'public.designations'::regclass) THEN
    ALTER TABLE ONLY public.designations
        ADD CONSTRAINT uq_designations_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_designations_company_code on designations';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_designations_company_code on designations was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_distributiontypes_company_name' AND conrelid = 'public.distributiontypes'::regclass) THEN
    ALTER TABLE ONLY public.distributiontypes
        ADD CONSTRAINT uq_distributiontypes_company_name UNIQUE (companyid, name);
    RAISE NOTICE 'Added key/constraint uq_distributiontypes_company_name on distributiontypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_distributiontypes_company_name on distributiontypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_divisions_company_code' AND conrelid = 'public.divisions'::regclass) THEN
    ALTER TABLE ONLY public.divisions
        ADD CONSTRAINT uq_divisions_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_divisions_company_code on divisions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_divisions_company_code on divisions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_document_attribute_value' AND conrelid = 'public.documentattributevalues'::regclass) THEN
    ALTER TABLE ONLY public.documentattributevalues
        ADD CONSTRAINT uq_document_attribute_value UNIQUE (companyid, documentid, documentattributeid);
    RAISE NOTICE 'Added key/constraint uq_document_attribute_value on documentattributevalues';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_document_attribute_value on documentattributevalues was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documentrequests_company_requestnumber' AND conrelid = 'public.documentrequests'::regclass) THEN
    ALTER TABLE ONLY public.documentrequests
        ADD CONSTRAINT uq_documentrequests_company_requestnumber UNIQUE (companyid, requestnumber);
    RAISE NOTICE 'Added key/constraint uq_documentrequests_company_requestnumber on documentrequests';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documentrequests_company_requestnumber on documentrequests was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documentrequesttypes_company_code' AND conrelid = 'public.documentrequesttypes'::regclass) THEN
    ALTER TABLE ONLY public.documentrequesttypes
        ADD CONSTRAINT uq_documentrequesttypes_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_documentrequesttypes_company_code on documentrequesttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documentrequesttypes_company_code on documentrequesttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documentrequesttypes_company_name' AND conrelid = 'public.documentrequesttypes'::regclass) THEN
    ALTER TABLE ONLY public.documentrequesttypes
        ADD CONSTRAINT uq_documentrequesttypes_company_name UNIQUE (companyid, name);
    RAISE NOTICE 'Added key/constraint uq_documentrequesttypes_company_name on documentrequesttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documentrequesttypes_company_name on documentrequesttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documentrevisiondistributions' AND conrelid = 'public.documentrevisiondistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrevisiondistributions
        ADD CONSTRAINT uq_documentrevisiondistributions UNIQUE (companyid, documentid);
    RAISE NOTICE 'Added key/constraint uq_documentrevisiondistributions on documentrevisiondistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documentrevisiondistributions on documentrevisiondistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documents_company_documentnumber' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT uq_documents_company_documentnumber UNIQUE (companyid, documentnumber);
    RAISE NOTICE 'Added key/constraint uq_documents_company_documentnumber on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documents_company_documentnumber on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_documenttypes_company_code' AND conrelid = 'public.documenttypes'::regclass) THEN
    ALTER TABLE ONLY public.documenttypes
        ADD CONSTRAINT uq_documenttypes_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_documenttypes_company_code on documenttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_documenttypes_company_code on documenttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_roles_company_name' AND conrelid = 'public.roles'::regclass) THEN
    ALTER TABLE ONLY public.roles
        ADD CONSTRAINT uq_roles_company_name UNIQUE (companyid, name);
    RAISE NOTICE 'Added key/constraint uq_roles_company_name on roles';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_roles_company_name on roles was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_subdepartment_company_code' AND conrelid = 'public.subdepartments'::regclass) THEN
    ALTER TABLE ONLY public.subdepartments
        ADD CONSTRAINT uq_subdepartment_company_code UNIQUE (companyid, code);
    RAISE NOTICE 'Added key/constraint uq_subdepartment_company_code on subdepartments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_subdepartment_company_code on subdepartments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_subdepartments_company_department_code' AND conrelid = 'public.subdepartments'::regclass) THEN
    ALTER TABLE ONLY public.subdepartments
        ADD CONSTRAINT uq_subdepartments_company_department_code UNIQUE (companyid, departmentcode, code);
    RAISE NOTICE 'Added key/constraint uq_subdepartments_company_department_code on subdepartments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_subdepartments_company_department_code on subdepartments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_trainingpolicies_company_documenttype' AND conrelid = 'public.trainingpolicies'::regclass) THEN
    ALTER TABLE ONLY public.trainingpolicies
        ADD CONSTRAINT uq_trainingpolicies_company_documenttype UNIQUE (companyid, documenttypecode);
    RAISE NOTICE 'Added key/constraint uq_trainingpolicies_company_documenttype on trainingpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_trainingpolicies_company_documenttype on trainingpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_transition' AND conrelid = 'public.documentstatetransitions'::regclass) THEN
    ALTER TABLE ONLY public.documentstatetransitions
        ADD CONSTRAINT uq_transition UNIQUE (fromstateid, tostateid);
    RAISE NOTICE 'Added key/constraint uq_transition on documentstatetransitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_transition on documentstatetransitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_users_company_employeecode' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT uq_users_company_employeecode UNIQUE (companyid, employeecode);
    RAISE NOTICE 'Added key/constraint uq_users_company_employeecode on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint uq_users_company_employeecode on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'useraccesslevels_pkey' AND conrelid = 'public.useraccesslevels'::regclass) THEN
    ALTER TABLE ONLY public.useraccesslevels
        ADD CONSTRAINT useraccesslevels_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint useraccesslevels_pkey on useraccesslevels';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint useraccesslevels_pkey on useraccesslevels was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'userroles_pkey' AND conrelid = 'public.userroles'::regclass) THEN
    ALTER TABLE ONLY public.userroles
        ADD CONSTRAINT userroles_pkey PRIMARY KEY (companyid, userid, roleid);
    RAISE NOTICE 'Added key/constraint userroles_pkey on userroles';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint userroles_pkey on userroles was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_companyid_email_key' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT users_companyid_email_key UNIQUE (companyid, email);
    RAISE NOTICE 'Added key/constraint users_companyid_email_key on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint users_companyid_email_key on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_pkey' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT users_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint users_pkey on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint users_pkey on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowexecutions_pkey' AND conrelid = 'public.workflowexecutions'::regclass) THEN
    ALTER TABLE ONLY public.workflowexecutions
        ADD CONSTRAINT workflowexecutions_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint workflowexecutions_pkey on workflowexecutions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowexecutions_pkey on workflowexecutions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowexecutionsteps_pkey' AND conrelid = 'public.workflowexecutionsteps'::regclass) THEN
    ALTER TABLE ONLY public.workflowexecutionsteps
        ADD CONSTRAINT workflowexecutionsteps_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint workflowexecutionsteps_pkey on workflowexecutionsteps';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowexecutionsteps_pkey on workflowexecutionsteps was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowpolicies_pkey' AND conrelid = 'public.workflowpolicies'::regclass) THEN
    ALTER TABLE ONLY public.workflowpolicies
        ADD CONSTRAINT workflowpolicies_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint workflowpolicies_pkey on workflowpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowpolicies_pkey on workflowpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowpolicyversions_companyid_workflowpolicyid_versionnu_key' AND conrelid = 'public.workflowpolicyversions'::regclass) THEN
    ALTER TABLE ONLY public.workflowpolicyversions
        ADD CONSTRAINT workflowpolicyversions_companyid_workflowpolicyid_versionnu_key UNIQUE (companyid, workflowpolicyid, versionnumber);
    RAISE NOTICE 'Added key/constraint workflowpolicyversions_companyid_workflowpolicyid_versionnu_key on workflowpolicyversions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowpolicyversions_companyid_workflowpolicyid_versionnu_key on workflowpolicyversions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowpolicyversions_pkey' AND conrelid = 'public.workflowpolicyversions'::regclass) THEN
    ALTER TABLE ONLY public.workflowpolicyversions
        ADD CONSTRAINT workflowpolicyversions_pkey PRIMARY KEY (companyid, id);
    RAISE NOTICE 'Added key/constraint workflowpolicyversions_pkey on workflowpolicyversions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowpolicyversions_pkey on workflowpolicyversions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowstepdefinitions_pkey' AND conrelid = 'public.workflowstepdefinitions'::regclass) THEN
    ALTER TABLE ONLY public.workflowstepdefinitions
        ADD CONSTRAINT workflowstepdefinitions_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint workflowstepdefinitions_pkey on workflowstepdefinitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowstepdefinitions_pkey on workflowstepdefinitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowsteptypes_code_key' AND conrelid = 'public.workflowsteptypes'::regclass) THEN
    ALTER TABLE ONLY public.workflowsteptypes
        ADD CONSTRAINT workflowsteptypes_code_key UNIQUE (code);
    RAISE NOTICE 'Added key/constraint workflowsteptypes_code_key on workflowsteptypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowsteptypes_code_key on workflowsteptypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowsteptypes_pkey' AND conrelid = 'public.workflowsteptypes'::regclass) THEN
    ALTER TABLE ONLY public.workflowsteptypes
        ADD CONSTRAINT workflowsteptypes_pkey PRIMARY KEY (id);
    RAISE NOTICE 'Added key/constraint workflowsteptypes_pkey on workflowsteptypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: key/constraint workflowsteptypes_pkey on workflowsteptypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_alert_recipients_config_id ON public.alert_recipients USING btree (alert_config_id);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_alert_recipients_config_id was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_alert_scopes_company_config ON public.alert_document_scopes USING btree (company_id, alert_config_id);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_alert_scopes_company_config was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_attrmandatory_scope ON public.attributemandatoryscopes USING btree (companyid, divisioncode, departmentcode, subdepartmentcode, businessdomaincode, documentattributeid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_attrmandatory_scope was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_docreq_company_businessdomain ON public.documentrequests USING btree (companyid, businessdomaincode);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_docreq_company_businessdomain was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_documentadhocapprovers_document ON public.documentadhocapprovers USING btree (documentid, companyid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_documentadhocapprovers_document was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_documents_company ON public.documents USING btree (companyid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_documents_company was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_documents_company_businessdomain ON public.documents USING btree (companyid, businessdomaincode);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_documents_company_businessdomain was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_orgunits_company ON public.orgunits USING btree (companyid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_orgunits_company was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_templates_scope ON public.templates USING btree (companyid, documenttypecode, divisioncode, departmentcode, subdepartmentcode, businessdomaincode, isdefault);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_templates_scope was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_uncontrolleddochistory_doc ON public.uncontrolleddocumenthistory USING btree (uncontrolleddocumentid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_uncontrolleddochistory_doc was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_uncontrolleddocuments_company ON public.uncontrolleddocuments USING btree (companyid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_uncontrolleddocuments_company was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_users_company ON public.users USING btree (companyid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_users_company was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_users_company_businessdomain ON public.users USING btree (companyid, businessdomaincode);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_users_company_businessdomain was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS idx_workflow_execution_entity ON public.workflowexecutions USING btree (companyid, entitytype, entityid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index idx_workflow_execution_entity was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS ix_docrevroledist_doc ON public.documentrevisionroledistributions USING btree (companyid, documentid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ix_docrevroledist_doc was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS ix_docrevuserdist_doc ON public.documentrevisionuserdistributions USING btree (companyid, documentid);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ix_docrevuserdist_doc was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS ix_emailoutbox_due ON public.emailoutbox USING btree (status, nextattemptat);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ix_emailoutbox_due was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS ix_emailoutbox_lastattempt ON public.emailoutbox USING btree (lastattemptat);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ix_emailoutbox_lastattempt was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE INDEX IF NOT EXISTS ix_rolecabinetmappings_role ON public.rolecabinetmappings USING btree (companyid, roleid) WHERE (isdeleted = false);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ix_rolecabinetmappings_role was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_defaulttemplate_perdocumenttype ON public.templates USING btree (documenttypecode) WHERE ((isdefault = true) AND (isactive = true) AND (isdeleted = false));
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_defaulttemplate_perdocumenttype was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_documentrequesttype_code ON public.documentrequesttypes USING btree (code);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_documentrequesttype_code was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_documentrequesttype_name ON public.documentrequesttypes USING btree (upper((name)::text)) WHERE (isdeleted = false);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_documentrequesttype_name was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_documenttype_code ON public.documenttypes USING btree (code);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_documenttype_code was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_documenttype_name ON public.documenttypes USING btree (upper((name)::text)) WHERE (isdeleted = false);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_documenttype_name was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  CREATE UNIQUE INDEX IF NOT EXISTS ux_rolecabinetmappings_scope ON public.rolecabinetmappings USING btree (companyid, roleid, divisioncode, COALESCE(departmentcode, ''::character varying), COALESCE(subdepartmentcode, ''::character varying), COALESCE(businessdomaincode, ''::character varying)) WHERE (isdeleted = false);
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ux_rolecabinetmappings_scope was not created: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attributemandatoryscopes_businessdomains' AND conrelid = 'public.attributemandatoryscopes'::regclass) THEN
    ALTER TABLE ONLY public.attributemandatoryscopes
        ADD CONSTRAINT fk_attributemandatoryscopes_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);
    RAISE NOTICE 'Added foreign key fk_attributemandatoryscopes_businessdomains on attributemandatoryscopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_attributemandatoryscopes_businessdomains on attributemandatoryscopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attributemandatoryscopes_department' AND conrelid = 'public.attributemandatoryscopes'::regclass) THEN
    ALTER TABLE ONLY public.attributemandatoryscopes
        ADD CONSTRAINT fk_attributemandatoryscopes_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_attributemandatoryscopes_department on attributemandatoryscopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_attributemandatoryscopes_department on attributemandatoryscopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attributemandatoryscopes_division' AND conrelid = 'public.attributemandatoryscopes'::regclass) THEN
    ALTER TABLE ONLY public.attributemandatoryscopes
        ADD CONSTRAINT fk_attributemandatoryscopes_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);
    RAISE NOTICE 'Added foreign key fk_attributemandatoryscopes_division on attributemandatoryscopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_attributemandatoryscopes_division on attributemandatoryscopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attributemandatoryscopes_subdepartment' AND conrelid = 'public.attributemandatoryscopes'::regclass) THEN
    ALTER TABLE ONLY public.attributemandatoryscopes
        ADD CONSTRAINT fk_attributemandatoryscopes_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);
    RAISE NOTICE 'Added foreign key fk_attributemandatoryscopes_subdepartment on attributemandatoryscopes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_attributemandatoryscopes_subdepartment on attributemandatoryscopes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_auditlogs_company' AND conrelid = 'public.auditlogs'::regclass) THEN
    ALTER TABLE ONLY public.auditlogs
        ADD CONSTRAINT fk_auditlogs_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_auditlogs_company on auditlogs';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_auditlogs_company on auditlogs was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_businessdomain_subdepartment' AND conrelid = 'public.businessdomains'::regclass) THEN
    ALTER TABLE ONLY public.businessdomains
        ADD CONSTRAINT fk_businessdomain_subdepartment FOREIGN KEY (companyid, subdepartmentcode) REFERENCES public.subdepartments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_businessdomain_subdepartment on businessdomains';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_businessdomain_subdepartment on businessdomains was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_daa_company' AND conrelid = 'public.documentadhocapprovers'::regclass) THEN
    ALTER TABLE ONLY public.documentadhocapprovers
        ADD CONSTRAINT fk_daa_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_daa_company on documentadhocapprovers';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_daa_company on documentadhocapprovers was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_daa_document' AND conrelid = 'public.documentadhocapprovers'::regclass) THEN
    ALTER TABLE ONLY public.documentadhocapprovers
        ADD CONSTRAINT fk_daa_document FOREIGN KEY (documentid, companyid) REFERENCES public.documents(id, companyid);
    RAISE NOTICE 'Added foreign key fk_daa_document on documentadhocapprovers';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_daa_document on documentadhocapprovers was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_distributiontypes_company' AND conrelid = 'public.distributiontypes'::regclass) THEN
    ALTER TABLE ONLY public.distributiontypes
        ADD CONSTRAINT fk_distributiontypes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_distributiontypes_company on distributiontypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_distributiontypes_company on distributiontypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentattributes_company' AND conrelid = 'public.documentattributes'::regclass) THEN
    ALTER TABLE ONLY public.documentattributes
        ADD CONSTRAINT fk_documentattributes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documentattributes_company on documentattributes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentattributes_company on documentattributes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentattributes_documenttype' AND conrelid = 'public.documentattributes'::regclass) THEN
    ALTER TABLE ONLY public.documentattributes
        ADD CONSTRAINT fk_documentattributes_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documentattributes_documenttype on documentattributes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentattributes_documenttype on documentattributes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentattributevalues_attribute' AND conrelid = 'public.documentattributevalues'::regclass) THEN
    ALTER TABLE ONLY public.documentattributevalues
        ADD CONSTRAINT fk_documentattributevalues_attribute FOREIGN KEY (documentattributeid) REFERENCES public.documentattributes(id);
    RAISE NOTICE 'Added foreign key fk_documentattributevalues_attribute on documentattributevalues';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentattributevalues_attribute on documentattributevalues was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentattributevalues_company' AND conrelid = 'public.documentattributevalues'::regclass) THEN
    ALTER TABLE ONLY public.documentattributevalues
        ADD CONSTRAINT fk_documentattributevalues_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documentattributevalues_company on documentattributevalues';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentattributevalues_company on documentattributevalues was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentattributevalues_document' AND conrelid = 'public.documentattributevalues'::regclass) THEN
    ALTER TABLE ONLY public.documentattributevalues
        ADD CONSTRAINT fk_documentattributevalues_document FOREIGN KEY (companyid, documentid) REFERENCES public.documents(companyid, id);
    RAISE NOTICE 'Added foreign key fk_documentattributevalues_document on documentattributevalues';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentattributevalues_document on documentattributevalues was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequestroledistributions_company' AND conrelid = 'public.documentrequestroledistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestroledistributions
        ADD CONSTRAINT fk_documentrequestroledistributions_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documentrequestroledistributions_company on documentrequestroledistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequestroledistributions_company on documentrequestroledistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequestroledistributions_documentrequest' AND conrelid = 'public.documentrequestroledistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestroledistributions
        ADD CONSTRAINT fk_documentrequestroledistributions_documentrequest FOREIGN KEY (documentrequestid) REFERENCES public.documentrequests(id);
    RAISE NOTICE 'Added foreign key fk_documentrequestroledistributions_documentrequest on documentrequestroledistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequestroledistributions_documentrequest on documentrequestroledistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequests_company' AND conrelid = 'public.documentrequests'::regclass) THEN
    ALTER TABLE ONLY public.documentrequests
        ADD CONSTRAINT fk_documentrequests_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documentrequests_company on documentrequests';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequests_company on documentrequests was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequests_documentrequesttypes' AND conrelid = 'public.documentrequests'::regclass) THEN
    ALTER TABLE ONLY public.documentrequests
        ADD CONSTRAINT fk_documentrequests_documentrequesttypes FOREIGN KEY (companyid, documentrequesttypecode) REFERENCES public.documentrequesttypes(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documentrequests_documentrequesttypes on documentrequests';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequests_documentrequesttypes on documentrequests was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequests_documenttype' AND conrelid = 'public.documentrequests'::regclass) THEN
    ALTER TABLE ONLY public.documentrequests
        ADD CONSTRAINT fk_documentrequests_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documentrequests_documenttype on documentrequests';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequests_documenttype on documentrequests was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentrequests_documenttype' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documentrequests_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documentrequests_documenttype on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentrequests_documenttype on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_businessdomains' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documents_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documents_businessdomains on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documents_businessdomains on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_department' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documents_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documents_department on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documents_department on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_division' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documents_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);
    RAISE NOTICE 'Added foreign key fk_documents_division on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documents_division on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_parent' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documents_parent FOREIGN KEY (companyid, parentdocumentid) REFERENCES public.documents(companyid, id);
    RAISE NOTICE 'Added foreign key fk_documents_parent on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documents_parent on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_subdepartment' AND conrelid = 'public.documents'::regclass) THEN
    ALTER TABLE ONLY public.documents
        ADD CONSTRAINT fk_documents_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);
    RAISE NOTICE 'Added foreign key fk_documents_subdepartment on documents';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documents_subdepartment on documents was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documenttypes_company' AND conrelid = 'public.documenttypes'::regclass) THEN
    ALTER TABLE ONLY public.documenttypes
        ADD CONSTRAINT fk_documenttypes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documenttypes_company on documenttypes';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documenttypes_company on documenttypes was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentversions_company' AND conrelid = 'public.documentversions'::regclass) THEN
    ALTER TABLE ONLY public.documentversions
        ADD CONSTRAINT fk_documentversions_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_documentversions_company on documentversions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentversions_company on documentversions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_documentversions_document' AND conrelid = 'public.documentversions'::regclass) THEN
    ALTER TABLE ONLY public.documentversions
        ADD CONSTRAINT fk_documentversions_document FOREIGN KEY (companyid, documentid) REFERENCES public.documents(companyid, id);
    RAISE NOTICE 'Added foreign key fk_documentversions_document on documentversions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_documentversions_document on documentversions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_drud_company' AND conrelid = 'public.documentrequestuserdistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestuserdistributions
        ADD CONSTRAINT fk_drud_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_drud_company on documentrequestuserdistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_drud_company on documentrequestuserdistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_drud_request' AND conrelid = 'public.documentrequestuserdistributions'::regclass) THEN
    ALTER TABLE ONLY public.documentrequestuserdistributions
        ADD CONSTRAINT fk_drud_request FOREIGN KEY (documentrequestid) REFERENCES public.documentrequests(id);
    RAISE NOTICE 'Added foreign key fk_drud_request on documentrequestuserdistributions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_drud_request on documentrequestuserdistributions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_dut_company' AND conrelid = 'public.documentusertraining'::regclass) THEN
    ALTER TABLE ONLY public.documentusertraining
        ADD CONSTRAINT fk_dut_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_dut_company on documentusertraining';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_dut_company on documentusertraining was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_dut_document' AND conrelid = 'public.documentusertraining'::regclass) THEN
    ALTER TABLE ONLY public.documentusertraining
        ADD CONSTRAINT fk_dut_document FOREIGN KEY (documentid, companyid) REFERENCES public.documents(id, companyid);
    RAISE NOTICE 'Added foreign key fk_dut_document on documentusertraining';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_dut_document on documentusertraining was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_executionsteps_company' AND conrelid = 'public.workflowexecutionsteps'::regclass) THEN
    ALTER TABLE ONLY public.workflowexecutionsteps
        ADD CONSTRAINT fk_executionsteps_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_executionsteps_company on workflowexecutionsteps';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_executionsteps_company on workflowexecutionsteps was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_executionsteps_execution' AND conrelid = 'public.workflowexecutionsteps'::regclass) THEN
    ALTER TABLE ONLY public.workflowexecutionsteps
        ADD CONSTRAINT fk_executionsteps_execution FOREIGN KEY (companyid, workflowexecutionid) REFERENCES public.workflowexecutions(companyid, id);
    RAISE NOTICE 'Added foreign key fk_executionsteps_execution on workflowexecutionsteps';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_executionsteps_execution on workflowexecutionsteps was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_from_state' AND conrelid = 'public.documentstatetransitions'::regclass) THEN
    ALTER TABLE ONLY public.documentstatetransitions
        ADD CONSTRAINT fk_from_state FOREIGN KEY (fromstateid) REFERENCES public.documentstates(id);
    RAISE NOTICE 'Added foreign key fk_from_state on documentstatetransitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_from_state on documentstatetransitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_notifications_company' AND conrelid = 'public.notifications'::regclass) THEN
    ALTER TABLE ONLY public.notifications
        ADD CONSTRAINT fk_notifications_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_notifications_company on notifications';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_notifications_company on notifications was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_responsibilitytransfers_company' AND conrelid = 'public.responsibilitytransfers'::regclass) THEN
    ALTER TABLE ONLY public.responsibilitytransfers
        ADD CONSTRAINT fk_responsibilitytransfers_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_responsibilitytransfers_company on responsibilitytransfers';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_responsibilitytransfers_company on responsibilitytransfers was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_roles_company' AND conrelid = 'public.roles'::regclass) THEN
    ALTER TABLE ONLY public.roles
        ADD CONSTRAINT fk_roles_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_roles_company on roles';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_roles_company on roles was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_step_policy_version' AND conrelid = 'public.workflowstepdefinitions'::regclass) THEN
    ALTER TABLE ONLY public.workflowstepdefinitions
        ADD CONSTRAINT fk_step_policy_version FOREIGN KEY (companyid, workflowpolicyversionid) REFERENCES public.workflowpolicyversions(companyid, id);
    RAISE NOTICE 'Added foreign key fk_step_policy_version on workflowstepdefinitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_step_policy_version on workflowstepdefinitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_subdepartments_department' AND conrelid = 'public.subdepartments'::regclass) THEN
    ALTER TABLE ONLY public.subdepartments
        ADD CONSTRAINT fk_subdepartments_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_subdepartments_department on subdepartments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_subdepartments_department on subdepartments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_templates_businessdomains' AND conrelid = 'public.templates'::regclass) THEN
    ALTER TABLE ONLY public.templates
        ADD CONSTRAINT fk_templates_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);
    RAISE NOTICE 'Added foreign key fk_templates_businessdomains on templates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_templates_businessdomains on templates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_templates_department' AND conrelid = 'public.templates'::regclass) THEN
    ALTER TABLE ONLY public.templates
        ADD CONSTRAINT fk_templates_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_templates_department on templates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_templates_department on templates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_templates_division' AND conrelid = 'public.templates'::regclass) THEN
    ALTER TABLE ONLY public.templates
        ADD CONSTRAINT fk_templates_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);
    RAISE NOTICE 'Added foreign key fk_templates_division on templates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_templates_division on templates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_templates_subdepartment' AND conrelid = 'public.templates'::regclass) THEN
    ALTER TABLE ONLY public.templates
        ADD CONSTRAINT fk_templates_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);
    RAISE NOTICE 'Added foreign key fk_templates_subdepartment on templates';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_templates_subdepartment on templates was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_to_state' AND conrelid = 'public.documentstatetransitions'::regclass) THEN
    ALTER TABLE ONLY public.documentstatetransitions
        ADD CONSTRAINT fk_to_state FOREIGN KEY (tostateid) REFERENCES public.documentstates(id);
    RAISE NOTICE 'Added foreign key fk_to_state on documentstatetransitions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_to_state on documentstatetransitions was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_transferscopepolicies_division' AND conrelid = 'public.transferscopepolicies'::regclass) THEN
    ALTER TABLE ONLY public.transferscopepolicies
        ADD CONSTRAINT fk_transferscopepolicies_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);
    RAISE NOTICE 'Added foreign key fk_transferscopepolicies_division on transferscopepolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_transferscopepolicies_division on transferscopepolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_useraccesslevel_company' AND conrelid = 'public.useraccesslevels'::regclass) THEN
    ALTER TABLE ONLY public.useraccesslevels
        ADD CONSTRAINT fk_useraccesslevel_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_useraccesslevel_company on useraccesslevels';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_useraccesslevel_company on useraccesslevels was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_useraccesslevel_documenttype' AND conrelid = 'public.useraccesslevels'::regclass) THEN
    ALTER TABLE ONLY public.useraccesslevels
        ADD CONSTRAINT fk_useraccesslevel_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);
    RAISE NOTICE 'Added foreign key fk_useraccesslevel_documenttype on useraccesslevels';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_useraccesslevel_documenttype on useraccesslevels was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_businessdomains' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);
    RAISE NOTICE 'Added foreign key fk_users_businessdomains on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_businessdomains on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_company' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_users_company on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_company on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_departments' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_departments FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);
    RAISE NOTICE 'Added foreign key fk_users_departments on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_departments on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_designations' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_designations FOREIGN KEY (companyid, designationcode) REFERENCES public.designations(companyid, code);
    RAISE NOTICE 'Added foreign key fk_users_designations on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_designations on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_divisions' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_divisions FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);
    RAISE NOTICE 'Added foreign key fk_users_divisions on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_divisions on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_subdepartments' AND conrelid = 'public.users'::regclass) THEN
    ALTER TABLE ONLY public.users
        ADD CONSTRAINT fk_users_subdepartments FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);
    RAISE NOTICE 'Added foreign key fk_users_subdepartments on users';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_users_subdepartments on users was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_workflowpolicies_company' AND conrelid = 'public.workflowpolicies'::regclass) THEN
    ALTER TABLE ONLY public.workflowpolicies
        ADD CONSTRAINT fk_workflowpolicies_company FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key fk_workflowpolicies_company on workflowpolicies';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key fk_workflowpolicies_company on workflowpolicies was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'orgunits_companyid_fkey' AND conrelid = 'public.orgunits'::regclass) THEN
    ALTER TABLE ONLY public.orgunits
        ADD CONSTRAINT orgunits_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key orgunits_companyid_fkey on orgunits';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key orgunits_companyid_fkey on orgunits was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'orgunits_companyid_parentid_fkey' AND conrelid = 'public.orgunits'::regclass) THEN
    ALTER TABLE ONLY public.orgunits
        ADD CONSTRAINT orgunits_companyid_parentid_fkey FOREIGN KEY (companyid, parentid) REFERENCES public.orgunits(companyid, id);
    RAISE NOTICE 'Added foreign key orgunits_companyid_parentid_fkey on orgunits';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key orgunits_companyid_parentid_fkey on orgunits was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uncontrolleddocumenthistory_companyid_fkey' AND conrelid = 'public.uncontrolleddocumenthistory'::regclass) THEN
    ALTER TABLE ONLY public.uncontrolleddocumenthistory
        ADD CONSTRAINT uncontrolleddocumenthistory_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key uncontrolleddocumenthistory_companyid_fkey on uncontrolleddocumenthistory';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key uncontrolleddocumenthistory_companyid_fkey on uncontrolleddocumenthistory was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uncontrolleddocumenthistory_uncontrolleddocumentid_fkey' AND conrelid = 'public.uncontrolleddocumenthistory'::regclass) THEN
    ALTER TABLE ONLY public.uncontrolleddocumenthistory
        ADD CONSTRAINT uncontrolleddocumenthistory_uncontrolleddocumentid_fkey FOREIGN KEY (uncontrolleddocumentid) REFERENCES public.uncontrolleddocuments(id);
    RAISE NOTICE 'Added foreign key uncontrolleddocumenthistory_uncontrolleddocumentid_fkey on uncontrolleddocumenthistory';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key uncontrolleddocumenthistory_uncontrolleddocumentid_fkey on uncontrolleddocumenthistory was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uncontrolleddocuments_companyid_fkey' AND conrelid = 'public.uncontrolleddocuments'::regclass) THEN
    ALTER TABLE ONLY public.uncontrolleddocuments
        ADD CONSTRAINT uncontrolleddocuments_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);
    RAISE NOTICE 'Added foreign key uncontrolleddocuments_companyid_fkey on uncontrolleddocuments';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key uncontrolleddocuments_companyid_fkey on uncontrolleddocuments was not added: %', SQLERRM;
END $sync$;

DO $sync$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'workflowexecutions_companyid_workflowpolicyversionid_fkey' AND conrelid = 'public.workflowexecutions'::regclass) THEN
    ALTER TABLE ONLY public.workflowexecutions
        ADD CONSTRAINT workflowexecutions_companyid_workflowpolicyversionid_fkey FOREIGN KEY (companyid, workflowpolicyversionid) REFERENCES public.workflowpolicyversions(companyid, id);
    
    
    --
    -- PostgreSQL database dump complete
    --
    RAISE NOTICE 'Added foreign key workflowexecutions_companyid_workflowpolicyversionid_fkey on workflowexecutions';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: foreign key workflowexecutions_companyid_workflowpolicyversionid_fkey on workflowexecutions was not added: %', SQLERRM;
END $sync$;


-- ============================================================================
-- 4. Functions, procedures and views, created again with the current definitions
-- ============================================================================

CREATE FUNCTION public.fn_dms_audit_log() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_old jsonb;
    v_new jsonb;
    v_row jsonb;
    v_entityid int;
    v_companyid int;
    v_action text;
    v_empcode text;
    v_ip text;
BEGIN
    BEGIN
        v_empcode := NULLIF(current_setting('app.employee_code', true), '');
        v_ip := NULLIF(current_setting('app.ip_address', true), '');

        IF v_empcode IS NULL THEN
            RETURN COALESCE(NEW, OLD);
        END IF;

        IF TG_OP = 'INSERT' THEN
            v_new := to_jsonb(NEW);
            v_row := v_new;
            v_action := 'Created';
        ELSIF TG_OP = 'UPDATE' THEN
            v_old := to_jsonb(OLD);
            v_new := to_jsonb(NEW);
            v_row := v_new;
            v_action := 'Updated';
        ELSIF TG_OP = 'DELETE' THEN
            v_old := to_jsonb(OLD);
            v_row := v_old;
            v_action := 'Deleted';
        END IF;

        BEGIN
            v_entityid := NULLIF(v_row->>'id', '')::int;
        EXCEPTION WHEN OTHERS THEN
            v_entityid := NULL;
        END;

        BEGIN
            v_companyid := NULLIF(v_row->>'companyid', '')::int;
        EXCEPTION WHEN OTHERS THEN
            v_companyid := NULL;
        END;

        -- employeecode is varchar(10); truncate defensively rather than let a longer session
        -- value (shouldn't happen for real emp codes, but never let this be what breaks a write)
        -- fail the whole trigger.
        INSERT INTO AuditLogs
            (CompanyId, EmployeeCode, Action, EntityType, EntityId, OldValues, NewValues, Timestamp, IPAddress)
        VALUES
            (COALESCE(v_companyid, 0), LEFT(v_empcode, 10), v_action, LEFT(TG_TABLE_NAME, 50), COALESCE(v_entityid, 0), v_old, v_new, NOW(), LEFT(COALESCE(v_ip, ''), 45));
    EXCEPTION WHEN OTHERS THEN
        -- Audit logging must never block or fail the real data change it's observing.
        NULL;
    END;

    RETURN COALESCE(NEW, OLD);
END;
$$;

CREATE FUNCTION public.fn_get_my_inbox_documents(p_companyid integer, p_userid text, p_status text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text) RETURNS TABLE(id integer, companyid integer, company character varying, title character varying, documentjustification character varying, documentnumber character varying, documenttype character varying, documenttypecode character varying, versioncontent text, proposedversionnumber character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, createdat timestamp without time zone, createdby character varying, requestcreatedby character varying, requestcreatedat timestamp without time zone, draftfileurl character varying, requestjustification character varying, executionid integer, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying, activitytypecode character varying)
    LANGUAGE plpgsql
    AS $_$
 BEGIN
     RETURN QUERY

     -- Pending is a list of actions, so it stays keyed per step: two steps waiting on the same
     -- document are two things to do. Approved and Reverted/Rejected are lists of documents, so
     -- they are keyed per document and keep the most recent decision -- otherwise a document that
     -- was approved once at creation and again at revision appeared twice, with every visible
     -- column identical because they are all read from the document as it stands now.
     SELECT t.id, t.companyid, t.company, t.title, t.documentjustification, t.documentnumber,
            t.documenttype, t.documenttypecode, t.versioncontent, t.proposedversionnumber,
            t.division, t.divisioncode, t.department, t.departmentcode, t.subdepartment,
            t.subdepartmentcode, t.businessdomain, t.businessdomaincode, t.createdat, t.createdby,
            t.requestcreatedby, t.requestcreatedat, t.draftfileurl, t.requestjustification,
            t.executionid, t.stepid, t.steporder, t.steptype, t.executionstatus, t.startedat,
            t.previousversioncreatedon, t.previousversioncreatedby, t.activitytypecode
     FROM (
     SELECT DISTINCT ON (wes.Id)
                 d.Id,
         d.CompanyId,
         d.Company,
                 d.Title,
             d.Justification AS DocumentJustification,
         d.DocumentNumber,
         d.DocumentType,
         d.DocumentTypeCode,
         COALESCE(asofdv.Content, d.VersionContent) AS VersionContent,
                 COALESCE(asofdv.Version, d.Version) AS ProposedVersionNumber,
         d.Division,
         d.DivisionCode,
         d.Department,
         d.DepartmentCode,
         d.SubDepartment,
         d.SubDepartmentCode,
         d.BusinessDomain,
         d.BusinessDomainCode,
         d.CreatedAt, -- Date of Creation
                  -- Retrieve concatenated Employee Name, fallback to original CreatedBy code if not found
         COALESCE(
             NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
             d.CreatedBy
         )::character varying AS Createdby,

                 --dr.CreatedBy AS RequestCreatedBy,
                  -- Retrieve concatenated Employee Name, fallback to original CreatedBy code if not found
         COALESCE(
             NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
             dr.CreatedBy
         )::character varying AS RequestCreatedBy,
                 dr.CreatedAt AS RequestCreatedAt,
                 COALESCE(rawdoc.DocumentURL, dr.DraftFileURL) AS DraftFileURL,
                 dr.Justification AS RequestJustification,

                 we.Id AS ExecutionId,
                 wes.Id AS StepId,
         wsd.StepOrder,
         wsd.StepType,
         we.Status AS ExecutionStatus,  -- WorkflowExecution status with alias
         we.StartedAt,

                 -- Previous Version columns (only set when this document revises an earlier one)
                 prevver.CreatedAt AS PreviousVersionCreatedOn,
                 COALESCE(
                         NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                         prevver.CreatedBy
                 )::character varying AS PreviousVersionCreatedBy,

                 we.ActivityTypeCode

     FROM WorkflowExecutionSteps wes
     JOIN WorkflowExecutions we
         ON we.Id = wes.WorkflowExecutionId
         AND we.CompanyId = wes.CompanyId
         AND we.EntityType = 'Document'

     JOIN Vw_Documents d
         ON d.Id = we.EntityId
         AND d.CompanyId = we.CompanyId

     LEFT JOIN WorkflowStepDefinitions wsd
         ON wsd.Id = wes.StepDefinitionId

         -- The Request this execution actually concerns. d.RequestId is only the ORIGINAL
         -- creation request: a document that never had one (legacy / bulk import, RequestId NULL)
         -- got no request columns at all, and a revision of any document showed the creation
         -- request's Justification instead of the revision's own. A Revision/Obsoletion execution
         -- (ActivityTypeCode set) therefore prefers the latest request raised against this
         -- document (ParentDocumentId = d.Id) on or before the execution started, falling back
         -- to the creation request.
         LEFT JOIN LATERAL (
             SELECT r.*
             FROM DocumentRequests r
             WHERE r.CompanyId = d.CompanyId
               AND (
                    r.Id = d.RequestId
                    OR (we.ActivityTypeCode IS NOT NULL
                        AND r.ParentDocumentId = d.Id
                        AND r.CreatedAt <= we.StartedAt)
               )
             ORDER BY CASE WHEN r.ParentDocumentId = d.Id THEN 0 ELSE 1 END, r.Id DESC
             LIMIT 1
         ) dr ON TRUE
         -- Join tblEmployee using standard zero-trimmed empcode matching
     LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(dr.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) emp ON TRUE

         -- Raw document row, needed for ParentDocumentId (Vw_Documents may not expose it)
         LEFT JOIN Documents rawdoc
                 ON rawdoc.Id = d.Id
                 AND rawdoc.CompanyId = d.CompanyId

         -- The version that existed AS OF this execution's own StartedAt -- the one its decision
         -- actually concerns, whether that decision is still open or long since made. CreatedAt is
         -- immutable once a version is written, so this stays correct at every stage, unlike
         -- VersionType/Vw_Documents' resolution of it (which changes under a Draft as it's later
         -- promoted/archived). See header comment.
         LEFT JOIN LATERAL (
             SELECT v.Version, v.Content
             FROM DocumentVersions v
             WHERE v.DocumentId = d.Id
               AND v.CompanyId = d.CompanyId
               AND v.CreatedAt <= we.StartedAt
               AND COALESCE(v.IsDeleted, FALSE) = FALSE
             ORDER BY v.CreatedAt DESC, v.Id DESC
             LIMIT 1
         ) asofdv ON TRUE

         -- The version before this one -- what this record supersedes.
         --
         -- Not "the archived one": a version is only archived once its successor is authorised,
         -- so while a revision is out for approval nothing is archived yet and the approver
         -- deciding on it would see nothing. Ordering by version number and taking the second
         -- answers the same at every stage, before and after authorisation.
         --
         -- Reverted attempts are skipped: one an approver sent back was never in force, so it is
         -- not a predecessor and must not displace the release that is.
         LEFT JOIN LATERAL (
             SELECT v.CreatedAt, v.CreatedBy
             FROM DocumentVersions v
             WHERE v.DocumentId = d.Id
               AND v.CompanyId = d.CompanyId
               AND COALESCE(v.IsDeleted, FALSE) = FALSE
               AND COALESCE(v.ArchiveReason, '') <> 'Reverted'
             ORDER BY
                 CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                      THEN split_part(v.Version, '.', 1)::int ELSE -1 END DESC,
                 CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                      THEN split_part(v.Version, '.', 2)::int ELSE -1 END DESC,
                 v.Id DESC
             OFFSET 1 LIMIT 1
         ) prevver ON TRUE
         LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(prevver.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) prevemp ON TRUE

     WHERE wes.CompanyId = p_companyid
     AND (
         -- Pending for my approval
         (
                         p_status = 'Pending' AND
                         we.Status = 'Running'
             AND wes.IsActive = TRUE
             AND wes.Decision IS NULL
         )

         OR

         -- Approved by me
         (
                         p_status = 'Approved' AND
                         wes.Decision = 'Approved'
         )
                 OR
             -- Revert/Reworked by me
             (
                                 p_status IN ('Rejected', 'Reworked')
                 AND (
                     (we.Status = 'Rejected' AND wes.Decision = 'Rejected')
                     OR
                     (we.Status = 'Reworked' AND wes.Decision = 'Reworked')
                 )
             )
     )

     AND (
         (p_divisioncode IS NULL OR p_divisioncode = '' OR d.DivisionCode = p_divisioncode)
         AND (p_departmentcode IS NULL OR p_departmentcode = '' OR d.DepartmentCode = p_departmentcode)
         AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR d.SubDepartmentCode = p_subdepartmentcode)
         AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR d.BusinessDomainCode = p_businessdomaincode)
                 AND (p_documenttypecode IS NULL OR p_documenttypecode = '' OR dr.DocumentTypeCode = p_documenttypecode)
     )

     AND (
             wes.AssignedUserId::text =  p_userid::text
             OR
             wes.AssignedRoleId IN (
                 SELECT ejp.roleid
                 FROM public.tblempjobprofile ejp
                 INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                 WHERE e.CompanyId =  p_companyid
                   AND TRIM(e.empcode) =  p_userid
                   AND ejp.Active = TRUE
             )
             OR
             wes.AssignedDesignationId IN (
                 SELECT ejp.dsgid
                 FROM public.tblempjobprofile ejp
                 INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                 WHERE e.CompanyId =  p_companyid
                   AND TRIM(e.empcode) =  p_userid
                   AND ejp.Active = TRUE
             )
     )

     -- DISTINCT ON needs its key to lead the ordering.
     ORDER BY wes.Id
     ) t
     ORDER BY t.startedat DESC;
 END;

$_$;

CREATE FUNCTION public.fn_get_my_inbox_requests(p_companyid integer, p_userid text, p_requeststatus text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text) RETURNS TABLE(id integer, companyid integer, company character varying, requestnumber character varying, documentrequesttypecode character varying, documentid integer, documenttype character varying, documenttypecode character varying, documentname character varying, justification character varying, proposedcontent text, iscontentfinalized boolean, draftcontentlastmodifiedat timestamp without time zone, draftcontentlastmodifiedby character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, requeststatus integer, rowversion character varying, createdat timestamp without time zone, createdby character varying, draftfileurl character varying, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying, targetdocumentnumber character varying)
    LANGUAGE plpgsql
    AS $_$
BEGIN
    RETURN QUERY

    SELECT
        dr.Id, dr.CompanyId, dr.Company, dr.RequestNumber, dr.DocumentRequestTypeCode,
        dr.DocumentId, dr.DocumentType, dr.DocumentTypeCode, dr.DocumentName, dr.Justification,
        dr.ProposedContent, dr.IsContentFinalized, dr.DraftContentLastModifiedAt, dr.DraftContentLastModifiedBy,
        dr.Division, dr.DivisionCode, dr.Department, dr.DepartmentCode, dr.SubDepartment, dr.SubDepartmentCode,
        dr.BusinessDomain, dr.BusinessDomainCode, dr.Status, dr.RowVersion, dr.CreatedAt,
        COALESCE(
            NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
            dr.CreatedBy
        )::character varying AS createdby,
        dr.DraftFileURL,
        wes.Id AS StepId, wsd.StepOrder, wsd.StepType, we.Status AS ExecutionStatus, we.StartedAt,
        prevver.CreatedAt AS PreviousVersionCreatedOn,
        COALESCE(
            NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
            prevver.CreatedBy
        )::character varying AS PreviousVersionCreatedBy,
        -- The ACTUAL document being revised/obsoleted, so the approver can tell which document
        -- this request is against -- blank for a plain Creation request, which has no
        -- ParentDocumentId to join on.
        targetdoc.DocumentNumber AS TargetDocumentNumber
    FROM WorkflowExecutionSteps wes
    JOIN WorkflowExecutions we
        ON we.Id = wes.WorkflowExecutionId
        AND we.CompanyId = wes.CompanyId
        AND we.EntityType = 'Request'
    JOIN Vw_DocumentRequests dr
        ON dr.Id = we.EntityId
        AND dr.CompanyId = we.CompanyId
    LEFT JOIN WorkflowStepDefinitions wsd
        ON wsd.Id = wes.StepDefinitionId
    LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(dr.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) emp ON TRUE
    LEFT JOIN DocumentRequests rawdr
        ON rawdr.Id = dr.Id
        AND rawdr.CompanyId = dr.CompanyId
    LEFT JOIN Documents targetdoc
        ON targetdoc.Id = rawdr.ParentDocumentId
        AND targetdoc.CompanyId = dr.CompanyId
    -- The version this request would supersede: the newest live version of the document it
    -- targets.
    --
    -- This used to read the target DOCUMENT's own CreatedAt/CreatedBy, which is when the
    -- document was first created -- not the release the approver is about to replace. After a
    -- couple of revisions those are nothing like each other.
    --
    -- No OFFSET, unlike the documents inbox: a request has no version row of its own yet, so the
    -- newest live version of the target IS the one it supersedes. Reverted attempts are skipped;
    -- one an approver sent back was never in force.
    LEFT JOIN LATERAL (
        SELECT v.CreatedAt, v.CreatedBy
        FROM DocumentVersions v
        WHERE v.DocumentId = rawdr.ParentDocumentId
          AND v.CompanyId = dr.CompanyId
          AND COALESCE(v.IsDeleted, FALSE) = FALSE
          AND COALESCE(v.ArchiveReason, '') <> 'Reverted'
        ORDER BY
            CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                 THEN split_part(v.Version, '.', 1)::int ELSE -1 END DESC,
            CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                 THEN split_part(v.Version, '.', 2)::int ELSE -1 END DESC,
            v.Id DESC
        LIMIT 1
    ) prevver ON TRUE
    LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(prevver.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) prevemp ON TRUE

    WHERE wes.CompanyId = p_companyid
    AND (
        (p_requeststatus = 'Pending' AND we.Status = 'Running' AND wes.IsActive = TRUE AND wes.Decision IS NULL)
        OR (p_requeststatus = 'Approved' AND wes.Decision = 'Approved')
        OR (p_requeststatus IN ('Rejected', 'Reworked') AND (
                (we.Status = 'Rejected' AND wes.Decision = 'Rejected')
                OR (we.Status = 'Reworked' AND wes.Decision = 'Reworked')
            ))
    )
    AND (
        (p_divisioncode IS NULL OR p_divisioncode = '' OR dr.DivisionCode = p_divisioncode)
        AND (p_departmentcode IS NULL OR p_departmentcode = '' OR dr.DepartmentCode = p_departmentcode)
        AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR dr.SubDepartmentCode = p_subdepartmentcode)
        AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR dr.BusinessDomainCode = p_businessdomaincode)
        AND (p_documenttypecode IS NULL OR p_documenttypecode = '' OR dr.DocumentTypeCode = p_documenttypecode)
    )
    AND (
        wes.AssignedUserId::text = p_userid::text
        OR wes.AssignedRoleId IN (
            SELECT ejp.roleid FROM public.tblempjobprofile ejp
            INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
            WHERE e.CompanyId = p_companyid AND TRIM(e.empcode) = p_userid AND ejp.Active = TRUE
        )
        OR wes.AssignedDesignationId IN (
            SELECT ejp.dsgid FROM public.tblempjobprofile ejp
            INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
            WHERE e.CompanyId = p_companyid AND TRIM(e.empcode) = p_userid AND ejp.Active = TRUE
        )
    )
    ORDER BY we.StartedAt DESC;
END;
$_$;

CREATE PROCEDURE public.sp_get_my_workflow_requests(IN p_companyid integer, IN p_userid integer, IN p_requeststatus character varying, IN p_divisioncode character varying DEFAULT NULL::character varying, IN p_departmentcode character varying DEFAULT NULL::character varying, IN p_subdepartmentcode character varying DEFAULT NULL::character varying, IN p_businessdomaincode character varying DEFAULT NULL::character varying, IN p_documenttypecode character varying DEFAULT NULL::character varying, IN p_result refcursor DEFAULT 'mycursor'::refcursor)
    LANGUAGE plpgsql
    AS $$
BEGIN
    OPEN p_result FOR
    SELECT
        dr.*,

        wes.Id              AS StepId,
        wsd.StepOrder,
        wsd.StepType,

        we.StartedAt

    FROM WorkflowExecutionSteps wes

    INNER JOIN WorkflowExecutions we
        ON we.Id = wes.WorkflowExecutionId
        AND we.CompanyId = wes.CompanyId
        AND we.Status = 'Running'

    INNER JOIN Vw_DocumentRequests dr
        ON dr.Id = we.EntityId
        AND dr.CompanyId = we.CompanyId

    INNER JOIN WorkflowStepDefinitions wsd
        ON wsd.Id = wes.StepDefinitionId

    WHERE wes.CompanyId = p_companyid

    AND (
        -- 🟡 PENDING (only active steps without decision)
        (p_requeststatus = 'Pending'
            AND we.Status = 'Running'
            AND wes.IsActive = TRUE
            AND wes.Decision IS NULL
        )

        OR

        -- 🟢 APPROVED BY ME
        (p_requeststatus = 'Approved'
            AND wes.Decision = 'Approved'
        )

        OR

        -- 🔴 REJECTED BY ME
        (p_requeststatus = 'Rejected'
            AND wes.Decision = 'Rejected'
        )

        -- If p_requeststatus is NULL or empty → return nothing (or remove this OR to return all)
        OR (p_requeststatus IS NULL OR p_requeststatus = '')
    )

    AND (
           (p_divisioncode      IS NULL OR p_divisioncode      = '' OR dr.DivisionCode       = p_divisioncode)
        AND (p_departmentcode   IS NULL OR p_departmentcode   = '' OR dr.DepartmentCode     = p_departmentcode)
        AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR dr.SubDepartmentCode = p_subdepartmentcode)
        AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR dr.BusinessDomainCode = p_businessdomaincode)
        AND (p_documenttypecode  IS NULL OR p_documenttypecode  = '' OR dr.DocumentTypeCode  = p_documenttypecode)
    )

    AND (
           wes.AssignedUserId = p_userid
        OR wes.AssignedRoleId IN (
            SELECT RoleId
            FROM UserRoles
            WHERE UserId = p_userid
        )
    )

    ORDER BY we.StartedAt DESC;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

CREATE VIEW public.vw_employeenames AS
 SELECT ltrim(rtrim((empcode)::text), '0'::text) AS cleanempcode,
    empcode AS originalempcode,
    NULLIF(ltrim(rtrim((((((COALESCE(firstname, ''::character varying))::text || ' '::text) || (COALESCE(midname, ''::character varying))::text) || ' '::text) || (COALESCE(lastname, ''::character varying))::text))), ''::text) AS employeename,
    companyid
   FROM public.tblemployee;

CREATE VIEW public.vw_documentrequests AS
 SELECT DISTINCT d.id,
    d.companyid,
    d.requestnumber,
    d.documentrequesttypecode,
    d.documentid,
    d.documenttypecode,
    d.divisioncode,
    d.departmentcode,
    d.subdepartmentcode,
    d.businessdomaincode,
    d.documentname,
    d.justification,
    d.proposedcontent,
    d.iscontentfinalized,
    d.draftcontentlastmodifiedat,
    d.draftcontentlastmodifiedby,
    d.submittedat,
    d.submittedby,
    d.status,
    d.rowversion,
    d.isactive,
    d.isdeleted,
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.createdat,
    d.createdby,
    d.lastmodifiedat,
    d.lastmodifiedby,
    d.draftfileurl,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    bd.name AS businessdomain,
    subd.name AS subdepartment,
    dt.name AS documenttype
   FROM ((((((((public.documentrequests d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN public.vw_employeenames e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN public.vw_employeenames m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));

CREATE VIEW public.vw_documentrequests_latest_count AS
 WITH ranked_requests AS (
         SELECT d.id,
            d.companyid,
            d.documentname,
            d.status,
            d.createdby,
            d.createdat,
            row_number() OVER (PARTITION BY d.companyid, d.documentname ORDER BY d.createdat DESC, d.id DESC) AS rn
           FROM public.documentrequests d
        )
 SELECT id,
    companyid,
    documentname,
    status,
    createdby,
    createdat
   FROM ranked_requests
  WHERE (rn = 1);

CREATE VIEW public.vw_documents AS
 SELECT d.id,
    d.companyid,
    d.documentnumber,
    d.requestid,
    d.documenttypecode,
    d.parentdocumentid,
    d.title,
    d.justification,
    d.divisioncode,
    d.departmentcode,
    d.subdepartmentcode,
    d.businessdomaincode,
    d.nextreviewdate,
    d.isactive,
    d.isdeleted,
    d.createdat,
    d.createdby,
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.lastmodifiedat,
    d.lastmodifiedby,
    (COALESCE(dv.documenturl, d.documenturl))::character varying(500) AS documenturl,
    dv.content AS versioncontent,
    dv.version,
    dv.versiontype,
    dv.changedescription,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    bd.name AS businessdomain,
    subd.name AS subdepartment,
    dt.name AS documenttype
   FROM (((((((((public.documents d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content,
            documentversions.version,
            documentversions.versiontype,
            documentversions.changedescription,
            documentversions.documenturl
           FROM public.documentversions
          WHERE ((COALESCE(documentversions.isdeleted, false) = false) AND (COALESCE(documentversions.isactive, true) = true) AND (documentversions.versiontype <> 3))
          ORDER BY documentversions.documentid,
                CASE documentversions.versiontype
                    WHEN 2 THEN 0
                    WHEN 1 THEN 1
                    ELSE 2
                END, ((documentversions.content IS NOT NULL) AND (documentversions.content <> ''::text)) DESC, documentversions.id DESC) dv ON ((d.id = dv.documentid)))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((d.isdeleted = false) AND (d.isactive = true) AND ((dt.id IS NULL) OR (dt.isdeleted = false)))
  ORDER BY d.id;

CREATE VIEW public.vw_documents_all AS
 SELECT d.id,
    d.companyid,
    d.documentnumber,
    d.requestid,
    d.documenttypecode,
    d.parentdocumentid,
    d.title,
    d.justification,
    d.divisioncode,
    d.departmentcode,
    d.subdepartmentcode,
    d.businessdomaincode,
    d.nextreviewdate,
    d.isactive,
    d.isdeleted,
    d.createdat,
    d.createdby,
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.lastmodifiedat,
    d.lastmodifiedby,
    (COALESCE(dv.documenturl, d.documenturl))::character varying(500) AS documenturl,
    dv.content AS versioncontent,
    dv.version,
    dv.versiontype,
    dv.changedescription,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    bd.name AS businessdomain,
    subd.name AS subdepartment,
    dt.name AS documenttype
   FROM (((((((((public.documents d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content,
            documentversions.version,
            documentversions.versiontype,
            documentversions.changedescription,
            documentversions.documenturl
           FROM public.documentversions
          WHERE ((COALESCE(documentversions.isdeleted, false) = false) AND (((COALESCE(documentversions.isactive, true) = true) AND (documentversions.versiontype <> 3)) OR ((documentversions.archivereason)::text = 'Obsoleted'::text)))
          ORDER BY documentversions.documentid,
                CASE documentversions.versiontype
                    WHEN 2 THEN 0
                    WHEN 1 THEN 1
                    ELSE 2
                END, ((documentversions.content IS NOT NULL) AND (documentversions.content <> ''::text)) DESC, documentversions.id DESC) dv ON ((d.id = dv.documentid)))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((d.isdeleted = false) AND ((d.isactive = true) OR ((( SELECT ds.code
           FROM (public.documentstatehistory dsh
             JOIN public.documentstates ds ON ((ds.id = dsh.tostateid)))
          WHERE (dsh.documentid = d.id)
          ORDER BY dsh.changedat DESC, dsh.id DESC
         LIMIT 1))::text = 'OBSOLETE'::text)) AND ((dt.id IS NULL) OR (dt.isdeleted = false)))
  ORDER BY d.id;

CREATE VIEW public.vw_useraccesslevelcabinets AS
 SELECT ual.id AS useraccesslevelid,
    ual.companyid,
    ual.employeecode,
    ual.documenttypecode,
    dt.name AS documenttype,
    ual.divisioncode,
    div.name AS divisionname,
    ual.departmentcode,
    dep.name AS departmentname,
    ual.subdepartmentcode,
    sdep.name AS subdepartmentname,
    ual.businessdomaincode,
    bd.name AS businessdomainname,
    ual.isactive,
    ual.isdeleted,
    ual.createdat,
    ual.createdby
   FROM (((((public.useraccesslevels ual
     LEFT JOIN public.divisions div ON ((((ual.divisioncode)::text = (div.code)::text) AND (ual.companyid = div.companyid))))
     LEFT JOIN public.departments dep ON ((((ual.departmentcode)::text = (dep.code)::text) AND (ual.companyid = dep.companyid))))
     LEFT JOIN public.subdepartments sdep ON ((((ual.subdepartmentcode)::text = (sdep.code)::text) AND (ual.companyid = sdep.companyid))))
     LEFT JOIN public.businessdomains bd ON ((((ual.businessdomaincode)::text = (bd.code)::text) AND (ual.companyid = bd.companyid))))
     LEFT JOIN public.documenttypes dt ON ((((ual.documenttypecode)::text = (dt.code)::text) AND (dt.companyid = ual.companyid))))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));

CREATE VIEW public.vw_users AS
 SELECT u.id,
    u.companyid,
    u.employeecode,
    u.employeename,
    u.email,
    u.divisioncode,
    u.departmentcode,
    u.subdepartmentcode,
    u.businessdomaincode,
    u.designationcode,
    u.reportingto,
    u.grade,
    u.dateofjoining,
    u.isactive,
    u.isdeleted,
    u.createdat,
    u.createdby,
    u.lastmodifiedat,
    u.lastmodifiedby,
    div.name AS division,
    dep.name AS department,
    sdep.name AS subdepartment,
    c.name AS company,
    bd.name AS businessdomain,
    des.name AS designation
   FROM ((((((public.users u
     LEFT JOIN public.divisions div ON (((u.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((u.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments sdep ON (((u.subdepartmentcode)::text = (sdep.code)::text)))
     LEFT JOIN public.designations des ON (((u.designationcode)::text = (des.code)::text)))
     LEFT JOIN public.companies c ON ((u.companyid = c.id)))
     LEFT JOIN public.businessdomains bd ON (((u.businessdomaincode)::text = (bd.code)::text)));

CREATE VIEW public.vw_workflowpolicy AS
 SELECT t.id,
    t.companyid,
    t.name,
    t.entitytype,
    t.divisioncode,
    t.departmentcode,
    t.subdepartmentcode,
    t.businessdomaincode,
    t.documenttypecode,
    t.isactive,
    t.isdeleted,
    t.createdat,
    t.createdby,
    t.lastmodifiedat,
    t.lastmodifiedby,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    sdep.name AS subdepartment,
    bd.name AS businessdomain,
    dt.name AS documenttype
   FROM ((((((public.workflowpolicies t
     LEFT JOIN public.companies c ON ((t.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((t.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((t.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments sdep ON (((t.subdepartmentcode)::text = (sdep.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((t.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((t.documenttypecode)::text = (dt.code)::text)))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));

CREATE VIEW public.vw_workflowstepdefinitions AS
 SELECT ws.id,
    ws.companyid,
    ws.workflowpolicyversionid,
    ws.steporder,
    ws.stepgroup,
    ws.steptype,
    ws.roleid,
    ws.designationid,
    ws.userid,
    ws.requiresallapprovals,
    ws.isactive,
    ws.isdeleted,
    ws.createdat,
    ws.createdby,
    ws.lastmodifiedat,
    ws.lastmodifiedby,
    wp.name AS workflowpolicyname,
    e.empcode AS employeecode,
    e.firstname,
    e.midname,
    e.lastname,
    dt.name AS documenttype,
    wp.documenttypecode,
    wfp.workflowpolicyid,
    c.name AS company,
    wp.divisioncode,
    wp.departmentcode,
    wp.subdepartmentcode,
    wp.businessdomaincode,
    COALESCE(des.name, des_fallback.name) AS designation,
    COALESCE(des.code, des_fallback.code) AS designationcode,
    COALESCE(r.name, ejp.jobtitle) AS role,
    ws.canaddadhocapprover
   FROM ((((((((((((public.workflowstepdefinitions ws
     LEFT JOIN public.workflowpolicyversions wfp ON ((ws.workflowpolicyversionid = wfp.id)))
     LEFT JOIN public.workflowpolicies wp ON ((wfp.workflowpolicyid = wp.id)))
     LEFT JOIN public.documenttypes dt ON (((wp.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN public.companies c ON ((ws.companyid = c.id)))
     LEFT JOIN public.tblemployee e_direct ON (((ws.userid IS NOT NULL) AND ((ws.userid)::text = (e_direct.empcode)::text) AND (COALESCE(e_direct.active, 1) = 1))))
     LEFT JOIN public.tblempjobprofile ejp_dynamic ON ((((ws.roleid IS NOT NULL) AND (ejp_dynamic.roleid = ws.roleid) AND (COALESCE(ejp_dynamic.active, true) = true)) OR ((ws.designationid IS NOT NULL) AND (ejp_dynamic.dsgid = ws.designationid) AND (COALESCE(ejp_dynamic.active, true) = true)))))
     LEFT JOIN public.tblemployee e_dynamic ON (((ws.userid IS NULL) AND (e_dynamic.empid = ejp_dynamic.empid) AND (COALESCE(e_dynamic.active, 1) = 1))))
     LEFT JOIN public.tblemployee e ON ((e.empid = COALESCE(e_direct.empid, e_dynamic.empid))))
     LEFT JOIN public.tblempjobprofile ejp ON (((e.empid = ejp.empid) AND (COALESCE(ejp.active, true) = true))))
     LEFT JOIN public.tblsetupsdetail des ON ((ejp.dsgid = des.sdlid)))
     LEFT JOIN public.tblsetupsdetail des_fallback ON ((e.dsgid = des_fallback.sdlid)))
     LEFT JOIN public.tblsetupsdetail r ON ((ejp.roleid = r.sdlid)))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));


-- ============================================================================
-- 5. Reference data the application depends on (only what is missing)
-- ============================================================================

-- the company the rows hang off (almost every table has a CompanyId foreign key to it)
INSERT INTO public.companies (id, code, name, status, subscriptionplan, storagequotagb, createdat)
SELECT 1, 'COM-0001', 'ATCO', 'Active', 'Standard', 10, NOW()
WHERE NOT EXISTS (SELECT 1 FROM public.companies WHERE id = 1);


INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 1, 1, 'Division', true, false, '2026-07-25 07:48:49.303015', '011001011', '2026-10-14 03:45:24.26021', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.cabinetstructuretabsconfig WHERE id = 1);

INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 2, 1, 'Department', true, false, '2026-07-25 07:49:07.527504', '000100012', '2026-10-14 03:45:33.68016', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.cabinetstructuretabsconfig WHERE id = 2);

INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 4, 1, 'BusinessDomain', false, false, '2026-07-25 08:13:27.061671', '011001011', '2026-10-14 03:45:38.196863', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.cabinetstructuretabsconfig WHERE id = 4);

INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 3, 1, 'Sub Department', true, false, '2026-07-25 08:13:16.233581', '011001011', '2026-10-15 14:08:22.775662', '011001277' WHERE NOT EXISTS (SELECT 1 FROM public.cabinetstructuretabsconfig WHERE id = 3);

INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 1, 'Date', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System' WHERE NOT EXISTS (SELECT 1 FROM public.controltypes WHERE id = 1);

INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 2, 'Numeric', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System' WHERE NOT EXISTS (SELECT 1 FROM public.controltypes WHERE id = 2);

INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 4, 'List', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System' WHERE NOT EXISTS (SELECT 1 FROM public.controltypes WHERE id = 4);

INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 3, 'Textbox', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System' WHERE NOT EXISTS (SELECT 1 FROM public.controltypes WHERE id = 3);

INSERT INTO public.distributiontypes (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 1, 1, 'Physical', true, false, '2026-02-15 12:34:39.085013', 'system', '2026-02-15 12:34:39.085013', 'system' WHERE NOT EXISTS (SELECT 1 FROM public.distributiontypes WHERE id = 1);

INSERT INTO public.distributiontypes (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 2, 1, 'Digital', true, false, '2026-02-15 12:34:39.085013', 'system', '2026-02-15 12:34:39.085013', 'system' WHERE NOT EXISTS (SELECT 1 FROM public.distributiontypes WHERE id = 2);

INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 1, 1, 'DRT-0001', 'Creation of new document', 'Creation of new document', true, false, '2026-02-01 11:45:32.352813', '000100012', '2026-02-01 11:45:32.352813', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE code = 'DRT-0001') AND NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE id = 1);

INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 2, 1, 'DRT-0002', 'Revision of existing document', 'Revision of existing document', true, false, '2026-02-01 11:46:37.519482', '000100012', '2026-02-01 11:46:37.519482', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE code = 'DRT-0002') AND NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE id = 2);

INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) SELECT 3, 1, 'DRT-0003', 'Obsoletion of existing document', 'Obsoletion of existing document', true, false, '2026-02-01 11:46:51.683436', '000100012', '2026-02-01 11:46:51.683436', '000100012' WHERE NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE code = 'DRT-0003') AND NOT EXISTS (SELECT 1 FROM public.documentrequesttypes WHERE id = 3);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 1, 'DRAFT', 'Draft', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'DRAFT') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 1);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 2, 'PENDING_APPROVAL', 'Pending Approval', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'PENDING_APPROVAL') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 2);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 3, 'APPROVED', 'Approved', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'APPROVED') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 3);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 4, 'TRAINING_PENDING', 'Training Pending', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'TRAINING_PENDING') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 4);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 5, 'AUTHORIZED', 'Authorized', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'AUTHORIZED') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 5);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 6, 'EFFECTIVE', 'Effective', false WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'EFFECTIVE') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 6);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 8, 'REJECTED', 'Rejected', true WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'REJECTED') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 8);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 9, 'AUTHORIZATION_PENDING', 'Authorization Pending', true WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'AUTHORIZATION_PENDING') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 9);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 10, 'REVISED', 'Revised', true WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'REVISED') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 10);

INSERT INTO public.documentstates (id, code, name, isterminal) SELECT 7, 'OBSOLETE', 'Obsoleted', true WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = 'OBSOLETE') AND NOT EXISTS (SELECT 1 FROM public.documentstates WHERE id = 7);

INSERT INTO public.workflowsteptypes (id, code, name) SELECT 1, 'REVIEW', 'Review' WHERE NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE code = 'REVIEW') AND NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE id = 1);

INSERT INTO public.workflowsteptypes (id, code, name) SELECT 2, 'APPROVAL', 'Approval' WHERE NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE code = 'APPROVAL') AND NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE id = 2);

INSERT INTO public.workflowsteptypes (id, code, name) SELECT 3, 'AUTHORIZATION', 'Authorization' WHERE NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE code = 'AUTHORIZATION') AND NOT EXISTS (SELECT 1 FROM public.workflowsteptypes WHERE id = 3);


-- the name of a document state is filled in only where it is empty; an existing name is never changed
UPDATE public.documentstates AS s SET name = v.name
  FROM (VALUES ('DRAFT', 'Draft'), ('PENDING_APPROVAL', 'Pending Approval'), ('APPROVED', 'Approved'), ('TRAINING_PENDING', 'Training Pending'), ('AUTHORIZED', 'Authorized'), ('EFFECTIVE', 'Effective'), ('REJECTED', 'Rejected'), ('AUTHORIZATION_PENDING', 'Authorization Pending'), ('REVISED', 'Revised'), ('OBSOLETE', 'Obsoleted')) AS v(code, name)
 WHERE s.code = v.code AND s.name IS NULL;

-- sequences never behind the data
DO $sync$
DECLARE r record; v_seq text; v_max bigint;
BEGIN
  FOR r IN SELECT unnest(ARRAY['companies', 'cabinetstructuretabsconfig', 'controltypes', 'distributiontypes', 'documentrequesttypes', 'documentstates', 'workflowsteptypes']) AS tbl LOOP
    v_seq := pg_get_serial_sequence('public.' || r.tbl, 'id');
    IF v_seq IS NOT NULL THEN
      EXECUTE format('SELECT MAX(id) FROM public.%I', r.tbl) INTO v_max;
      IF v_max IS NOT NULL THEN PERFORM setval(v_seq, GREATEST(v_max, (SELECT last_value FROM pg_sequences WHERE schemaname = 'public' AND sequencename = split_part(v_seq, '.', 2))), true); END IF;
    END IF;
  END LOOP;
END $sync$;

-- columns that had to be added as nullable become NOT NULL again when they hold no empty value
DO $sync$
DECLARE r record;
BEGIN
  FOR r IN SELECT tbl, col FROM _sync_nullable LOOP
    BEGIN
      EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET NOT NULL', r.tbl, r.col);
      RAISE NOTICE 'Column %.% is NOT NULL again', r.tbl, r.col;
    EXCEPTION WHEN OTHERS THEN
      RAISE NOTICE 'CHECK: column %.% is NOT NULL in UAT but has empty values here, so it stays nullable', r.tbl, r.col;
    END;
  END LOOP;
END $sync$;

-- ----------------------------------------------------------------------------
-- 5a. Data written by an OLDER build, converted to the form the new build reads (only rows still in the old form are
--     touched; a database that has none of them is not changed at all)
-- ----------------------------------------------------------------------------

-- (1) An ad-hoc approver step added under the old author-level feature is flagged as ad-hoc.
UPDATE public.workflowexecutionsteps wes SET isadhoc = TRUE
  FROM public.workflowstepdefinitions wsd
  JOIN public.workflowpolicyversions wpv ON wpv.id = wsd.workflowpolicyversionid
  JOIN public.workflowpolicies wp ON wp.id = wpv.workflowpolicyid
 WHERE wsd.id = wes.stepdefinitionid AND wp.name = 'System Generated - Ad-hoc Approvers' AND wes.isadhoc = FALSE;

-- (2) A document that has been obsoleted reads "Obsoleted" (the code OBSOLETE, which the application uses, does not change).
UPDATE public.documentstates SET name = 'Obsoleted' WHERE code = 'OBSOLETE' AND name <> 'Obsoleted';

-- (3) The Document Users and Distribution List rows of an older build hold DMS cabinet codes (DIV-0001, DIV-0001-DPT-0002, ...)
--     in Division / Department / Sub Department. The new build keeps the People Partners ids there. A cabinet unit is matched to
--     the People Partners unit of the SAME NAME (trailing digits in the name are ignored). The four tables are copied to
--     <table>_bak_20261016 first (once). A unit with no same-name match is NOT changed and is listed as CHECK.
DO $sync$
DECLARE
  t text; r record; v_found boolean; v_n bigint; v_total bigint := 0;
BEGIN
  SELECT EXISTS (SELECT 1 FROM public.documentroledistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentuserdistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentrequestroledistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentrequestuserdistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
    INTO v_found;
  IF NOT v_found THEN RETURN; END IF;

  FOREACH t IN ARRAY ARRAY['documentroledistributions', 'documentuserdistributions', 'documentrequestroledistributions', 'documentrequestuserdistributions'] LOOP
    IF to_regclass('public.' || t || '_bak_20261016') IS NULL THEN
      EXECUTE format('CREATE TABLE public.%I AS SELECT * FROM public.%I', t || '_bak_20261016', t);
      RAISE NOTICE 'Copied % to %_bak_20261016', t, t;
    END IF;
  END LOOP;

  CREATE TEMP TABLE legacy_map ON COMMIT DROP AS
  SELECT 'division'::text AS lvl, c.companyid, c.code AS legacy, c.name AS legacy_name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 70 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1) AS ppid
    FROM public.divisions c
  UNION ALL
  SELECT 'department', c.companyid, c.code, c.name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 84 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1)
    FROM public.departments c
  UNION ALL
  SELECT 'subdepartment', c.companyid, c.code, c.name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 24 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1)
    FROM public.subdepartments c;

  FOR r IN SELECT lvl, legacy, legacy_name FROM legacy_map m
            WHERE ppid IS NULL AND (
                   EXISTS (SELECT 1 FROM public.documentroledistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentuserdistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentrequestroledistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentrequestuserdistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode)))
            ORDER BY lvl, legacy LOOP
    RAISE NOTICE 'CHECK: cabinet % % (%) has no same-name People Partners unit; its rows are left unchanged', r.lvl, r.legacy, r.legacy_name;
  END LOOP;

  FOREACH t IN ARRAY ARRAY['documentroledistributions', 'documentuserdistributions', 'documentrequestroledistributions', 'documentrequestuserdistributions'] LOOP
    EXECUTE format('UPDATE public.%1$I x SET divisioncode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''division'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.divisioncode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    EXECUTE format('UPDATE public.%1$I x SET departmentcode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''department'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.departmentcode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    EXECUTE format('UPDATE public.%1$I x SET subdepartmentcode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''subdepartment'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.subdepartmentcode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
  END LOOP;
  RAISE NOTICE 'Converted % Division / Department / Sub Department value(s) of Document Users and Distribution List rows from cabinet codes to People Partners ids', v_total;
END $sync$;


-- ============================================================================
-- 5b. Audit triggers (created after the reference data, so it is not audited)
-- ============================================================================

CREATE TRIGGER trg_audit_alert_configs AFTER INSERT OR DELETE OR UPDATE ON public.alert_configs FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_attributemandatoryscopes AFTER INSERT OR DELETE OR UPDATE ON public.attributemandatoryscopes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_businessdomains AFTER INSERT OR DELETE OR UPDATE ON public.businessdomains FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_cabinetstructuretabsconfig AFTER INSERT OR DELETE OR UPDATE ON public.cabinetstructuretabsconfig FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_companies AFTER INSERT OR DELETE OR UPDATE ON public.companies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_controltypes AFTER INSERT OR DELETE OR UPDATE ON public.controltypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_departments AFTER INSERT OR DELETE OR UPDATE ON public.departments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_designations AFTER INSERT OR DELETE OR UPDATE ON public.designations FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_distributiontypes AFTER INSERT OR DELETE OR UPDATE ON public.distributiontypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_divisions AFTER INSERT OR DELETE OR UPDATE ON public.divisions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentadhocapprovers AFTER INSERT OR DELETE OR UPDATE ON public.documentadhocapprovers FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentattributes AFTER INSERT OR DELETE OR UPDATE ON public.documentattributes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentattributevalues AFTER INSERT OR DELETE OR UPDATE ON public.documentattributevalues FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentrequesttypes AFTER INSERT OR DELETE OR UPDATE ON public.documentrequesttypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentreviewpolicies AFTER INSERT OR DELETE OR UPDATE ON public.documentreviewpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentstates AFTER INSERT OR DELETE OR UPDATE ON public.documentstates FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentstatetransitions AFTER INSERT OR DELETE OR UPDATE ON public.documentstatetransitions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documenttraining AFTER INSERT OR DELETE OR UPDATE ON public.documenttraining FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documenttrainingauthorizations AFTER INSERT OR DELETE OR UPDATE ON public.documenttrainingauthorizations FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documenttypes AFTER INSERT OR DELETE OR UPDATE ON public.documenttypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentusertraining AFTER INSERT OR DELETE OR UPDATE ON public.documentusertraining FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_documentversions AFTER INSERT OR DELETE OR UPDATE ON public.documentversions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_employeedraftobservation AFTER INSERT OR DELETE OR UPDATE ON public.employeedraftobservation FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_esignatures AFTER INSERT OR DELETE OR UPDATE ON public.esignatures FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_orgunits AFTER INSERT OR DELETE OR UPDATE ON public.orgunits FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_policyassignments AFTER INSERT OR DELETE OR UPDATE ON public.policyassignments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_responsibilitytransfers AFTER INSERT OR DELETE OR UPDATE ON public.responsibilitytransfers FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_roles AFTER INSERT OR DELETE OR UPDATE ON public.roles FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_subdepartments AFTER INSERT OR DELETE OR UPDATE ON public.subdepartments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_templates AFTER INSERT OR DELETE OR UPDATE ON public.templates FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_trainingpolicies AFTER INSERT OR DELETE OR UPDATE ON public.trainingpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_transferscopepolicies AFTER INSERT OR DELETE OR UPDATE ON public.transferscopepolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_transferworkflowpolicies AFTER INSERT OR DELETE OR UPDATE ON public.transferworkflowpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_uncontrolleddocumenthistory AFTER INSERT OR DELETE OR UPDATE ON public.uncontrolleddocumenthistory FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_uncontrolleddocuments AFTER INSERT OR DELETE OR UPDATE ON public.uncontrolleddocuments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_useraccesslevels AFTER INSERT OR DELETE OR UPDATE ON public.useraccesslevels FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_userroles AFTER INSERT OR DELETE OR UPDATE ON public.userroles FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_users AFTER INSERT OR DELETE OR UPDATE ON public.users FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_workflowpolicies AFTER INSERT OR DELETE OR UPDATE ON public.workflowpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_workflowpolicyversions AFTER INSERT OR DELETE OR UPDATE ON public.workflowpolicyversions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_workflowstepdefinitions AFTER INSERT OR DELETE OR UPDATE ON public.workflowstepdefinitions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();

CREATE TRIGGER trg_audit_workflowsteptypes AFTER INSERT OR DELETE OR UPDATE ON public.workflowsteptypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


-- ============================================================================
-- 6. Check: everything must exist now, or the whole run is undone
-- ============================================================================

DO $sync$
DECLARE v_missing text := ''; v_count int;
BEGIN
  SELECT string_agg(t, ', ') INTO v_missing FROM unnest(ARRAY['alert_configs', 'alert_document_scopes', 'alert_recipients', 'attributemandatoryscopes', 'auditlogs', 'businessdomains', 'cabinetstructuretabsconfig', 'companies', 'controltypes', 'departments', 'designations', 'distributiontypes', 'divisions', 'documentadhocapprovers', 'documentattributes', 'documentattributevalues', 'documentevents', 'documentrequesthistory', 'documentrequestroledistributions', 'documentrequests', 'documentrequesttypes', 'documentrequestuserdistributions', 'documentreviewpolicies', 'documentrevisiondistributions', 'documentrevisionroledistributions', 'documentrevisionuserdistributions', 'documentroledistributions', 'documents', 'documentstatehistory', 'documentstates', 'documentstatetransitions', 'documenttraining', 'documenttrainingauthorizations', 'documenttypes', 'documentuserdistributions', 'documentusertraining', 'documentversions', 'emailoutbox', 'employeedraftobservation', 'esignatures', 'notifications', 'orgunits', 'policyassignments', 'responsibilitytransfers', 'rolecabinetmappings', 'roles', 'subdepartments', 'tbldeptstrdetail', 'tbldeptstrmaster', 'tblempjobprofile', 'tblemployee', 'tblsetupsdetail', 'templates', 'trainingpolicies', 'transferscopepolicies', 'transferworkflowpolicies', 'uncontrolleddocumenthistory', 'uncontrolleddocuments', 'useraccesslevels', 'userroles', 'users', 'workflowpolicies', 'workflowpolicyversions', 'workflowstepdefinitions', 'workflowexecutions', 'workflowexecutionsteps', 'workflowsteptypes']) AS t
   WHERE to_regclass('public.' || t) IS NULL;
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Tables still missing: %', v_missing; END IF;

  SELECT string_agg(v, ', ') INTO v_missing FROM unnest(ARRAY['vw_employeenames', 'vw_documentrequests', 'vw_documentrequests_latest_count', 'vw_documents', 'vw_documents_all', 'vw_useraccesslevelcabinets', 'vw_users', 'vw_workflowpolicy', 'vw_workflowstepdefinitions']) AS v
   WHERE NOT EXISTS (SELECT 1 FROM pg_views WHERE schemaname = 'public' AND viewname = v);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Views still missing: %', v_missing; END IF;

  SELECT string_agg(f, ', ') INTO v_missing FROM unnest(ARRAY['fn_dms_audit_log', 'fn_get_my_inbox_documents', 'fn_get_my_inbox_requests', 'sp_get_my_workflow_requests']) AS f
   WHERE NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public' AND p.proname = f);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Functions/procedures still missing: %', v_missing; END IF;

  SELECT string_agg(t, ', ') INTO v_missing FROM unnest(ARRAY['trg_audit_alert_configs', 'trg_audit_attributemandatoryscopes', 'trg_audit_businessdomains', 'trg_audit_cabinetstructuretabsconfig', 'trg_audit_companies', 'trg_audit_controltypes', 'trg_audit_departments', 'trg_audit_designations', 'trg_audit_distributiontypes', 'trg_audit_divisions', 'trg_audit_documentadhocapprovers', 'trg_audit_documentattributes', 'trg_audit_documentattributevalues', 'trg_audit_documentrequesttypes', 'trg_audit_documentreviewpolicies', 'trg_audit_documentstates', 'trg_audit_documentstatetransitions', 'trg_audit_documenttraining', 'trg_audit_documenttrainingauthorizations', 'trg_audit_documenttypes', 'trg_audit_documentusertraining', 'trg_audit_documentversions', 'trg_audit_employeedraftobservation', 'trg_audit_esignatures', 'trg_audit_orgunits', 'trg_audit_policyassignments', 'trg_audit_responsibilitytransfers', 'trg_audit_roles', 'trg_audit_subdepartments', 'trg_audit_templates', 'trg_audit_trainingpolicies', 'trg_audit_transferscopepolicies', 'trg_audit_transferworkflowpolicies', 'trg_audit_uncontrolleddocumenthistory', 'trg_audit_uncontrolleddocuments', 'trg_audit_useraccesslevels', 'trg_audit_userroles', 'trg_audit_users', 'trg_audit_workflowpolicies', 'trg_audit_workflowpolicyversions', 'trg_audit_workflowstepdefinitions', 'trg_audit_workflowsteptypes']) AS t
   WHERE NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = t AND NOT tgisinternal);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Triggers still missing: %', v_missing; END IF;

  SELECT string_agg(c, ', ') INTO v_missing FROM unnest(ARRAY['DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'TRAINING_PENDING', 'AUTHORIZED', 'EFFECTIVE', 'REJECTED', 'AUTHORIZATION_PENDING', 'REVISED', 'OBSOLETE']) AS c
   WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = c);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Document states still missing: %', v_missing; END IF;

  SELECT COUNT(*) INTO v_count FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
  RAISE NOTICE 'OK: all 67 tables, 9 views, 4 functions/procedures and 42 triggers are in place.';
END $sync$;

COMMIT;
