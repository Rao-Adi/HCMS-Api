--
-- PostgreSQL database dump
--

\restrict eo9eabQqV27dxFG62dyxbVf1WkQE2B8c8dwnxjlx4elBq6d3l7SyM2z6mi6F6fa

-- Dumped from database version 17.2
-- Dumped by pg_dump version 18.1

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Data for Name: cabinetstructuretabsconfig; Type: TABLE DATA; Schema: public; Owner: -
--

--
-- The company the DMS rows hang off (almost every table has a CompanyId foreign key to Companies, so this has to exist first).
-- Only ATCO (id 1) is created here; add the other companies from the application's Companies screen, or with the same
-- INSERT. The id must be the CompanyId the employees carry in HCMS.
--
INSERT INTO public.companies (id, code, name, status, subscriptionplan, storagequotagb, createdat)
VALUES (1, 'COM-0001', 'ATCO', 'Active', 'Standard', 10, NOW());
SELECT pg_catalog.setval(pg_get_serial_sequence('public.companies', 'id'), (SELECT MAX(id) FROM public.companies), true);

INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (1, 1, 'Division', true, false, '2026-07-25 07:48:49.303015', '011001011', '2026-10-14 03:45:24.26021', '000100012');
INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (2, 1, 'Department', true, false, '2026-07-25 07:49:07.527504', '000100012', '2026-10-14 03:45:33.68016', '000100012');
INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (4, 1, 'BusinessDomain', false, false, '2026-07-25 08:13:27.061671', '011001011', '2026-10-14 03:45:38.196863', '000100012');
INSERT INTO public.cabinetstructuretabsconfig (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (3, 1, 'Sub Department', true, false, '2026-07-25 08:13:16.233581', '011001011', '2026-10-15 14:08:22.775662', '011001277');


--
-- Data for Name: controltypes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (1, 'Date', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System');
INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (2, 'Numeric', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System');
INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (4, 'List', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System');
INSERT INTO public.controltypes (id, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (3, 'Textbox', true, false, '2026-02-08 08:25:39.770436', 'System', '2026-02-08 08:25:39.770436', 'System');


--
-- Data for Name: distributiontypes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.distributiontypes (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (1, 1, 'Physical', true, false, '2026-02-15 12:34:39.085013', 'system', '2026-02-15 12:34:39.085013', 'system');
INSERT INTO public.distributiontypes (id, companyid, name, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (2, 1, 'Digital', true, false, '2026-02-15 12:34:39.085013', 'system', '2026-02-15 12:34:39.085013', 'system');


--
-- Data for Name: documentrequesttypes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (1, 1, 'DRT-0001', 'Creation of new document', 'Creation of new document', true, false, '2026-02-01 11:45:32.352813', '000100012', '2026-02-01 11:45:32.352813', '000100012');
INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (2, 1, 'DRT-0002', 'Revision of existing document', 'Revision of existing document', true, false, '2026-02-01 11:46:37.519482', '000100012', '2026-02-01 11:46:37.519482', '000100012');
INSERT INTO public.documentrequesttypes (id, companyid, code, name, description, isactive, isdeleted, createdat, createdby, lastmodifiedat, lastmodifiedby) VALUES (3, 1, 'DRT-0003', 'Obsoletion of existing document', 'Obsoletion of existing document', true, false, '2026-02-01 11:46:51.683436', '000100012', '2026-02-01 11:46:51.683436', '000100012');


--
-- Data for Name: documentstates; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (1, 'DRAFT', 'Draft', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (2, 'PENDING_APPROVAL', 'Pending Approval', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (3, 'APPROVED', 'Approved', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (4, 'TRAINING_PENDING', 'Training Pending', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (5, 'AUTHORIZED', 'Authorized', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (6, 'EFFECTIVE', 'Effective', false);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (8, 'REJECTED', 'Rejected', true);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (9, 'AUTHORIZATION_PENDING', 'Authorization Pending', true);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (10, 'REVISED', 'Revised', true);
INSERT INTO public.documentstates (id, code, name, isterminal) VALUES (7, 'OBSOLETE', 'Obsoleted', true);


--
-- Data for Name: workflowsteptypes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflowsteptypes (id, code, name) VALUES (1, 'REVIEW', 'Review');
INSERT INTO public.workflowsteptypes (id, code, name) VALUES (2, 'APPROVAL', 'Approval');
INSERT INTO public.workflowsteptypes (id, code, name) VALUES (3, 'AUTHORIZATION', 'Authorization');


--
-- Name: cabinetstructuretabsconfig_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.cabinetstructuretabsconfig_id_seq', 2, true);


--
-- Name: controltypes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.controltypes_id_seq', 4, true);


--
-- Name: distributiontypes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.distributiontypes_id_seq', 2, true);


--
-- Name: documentrequesttypes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.documentrequesttypes_id_seq', 3, true);


--
-- Name: documentstates_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.documentstates_id_seq', 11, true);


--
-- Name: workflowsteptypes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflowsteptypes_id_seq', 3, true);


--
-- PostgreSQL database dump complete
--

\unrestrict eo9eabQqV27dxFG62dyxbVf1WkQE2B8c8dwnxjlx4elBq6d3l7SyM2z6mi6F6fa

