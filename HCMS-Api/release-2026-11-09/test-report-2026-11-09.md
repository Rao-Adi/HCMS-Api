# Test report - 2026-11-09 (VPN connected)

Everything built on 2026-11-08 and 2026-11-09, tested against a **local clone of the real UAT database** (`ATCO_DMS` on 10.10.0.80, copied read-only on 2026-11-09). UAT itself was only **read** (SELECT) - nothing was written to it. In the clone every employee email was blanked and all alerts switched off, and the API ran with its mail host set to localhost, so no mail could leave the machine.

The API was called as the real UAT approvers (Asif Baig, Muhammad Wajahat Shahid, Muhammad Ghayas Siddiqui, Abdur Rehman Qureshi, Mohammad Iqbal, and the real heads Muhammad Imtiaz and Nazim Uddin) through the same HTTP endpoints the screens use.

## Result: 97 of 97 checks pass

| Scenario | Checks | Pass |
|---|---|---|
| S1 Creation (3 approvers, one rework) | 11 | 11 |
| S2 Revision (version 2.0 / history) | 10 | 10 |
| S3 One modal: Signing Authorities + Approval History | 3 | 3 |
| S4 Periodic review: re-approve without change | 11 | 11 |
| S5 Reject / Revert -> initiator + previous approvers | 3 | 3 |
| S6 Obsoletion (reworked + revised document) | 5 | 5 |
| S0 setup | 1 | 1 |
| S7 Head steps show the person (name / designation / role) | 12 | 12 |
| S7b Policy 5 (the screenshot policy), no access levels | 3 | 3 |
| S8 Document footer (signatory block) | 3 | 3 |
| S9 Head cannot be found | 3 | 3 |
| S10 Data-fix script on real UAT document IT-POL-001 | 3 | 3 |
| R0 setup | 1 | 1 |
| R1 Remove an approver from the policy | 4 | 4 |
| R2 Sequence changed | 2 | 2 |
| R3 Remove the approver the document is with | 4 | 4 |
| R4 Add an approver | 2 | 2 |
| R5 Nobody left to approve | 6 | 6 |
| R6 Head of Division removed after the document was submitted | 3 | 3 |
| R7 Policy emptied | 2 | 2 |
| R8 Request workflow follows the policy too | 3 | 3 |
| R9 History kept | 2 | 2 |

## 1. Every check

| Scenario | Check | Result | Evidence |
|---|---|---|---|
| S1 Creation (3 approvers, one rework) | request submitted | PASS | {"id":48,"msg":"Document Request submitted successfully."} |
| S1 Creation (3 approvers, one rework) | request approved by its approver | PASS | ["011001011:200"] |
| S1 Creation (3 approvers, one rework) | document created, draft version 1.0 | PASS | ["1.0\|1\|true\|\|false"] |
| S1 Creation (3 approvers, one rework) | document submitted | PASS | Document Created Successfully |
| S1 Creation (3 approvers, one rework) | document workflow has 3 steps: Ghayas > Wajahat > Abdur | PASS | ["011001116","011001011","011001277"] |
| S1 Creation (3 approvers, one rework) | 1st approver sends it back (rework) | PASS | Document Reverted Successfully |
| S1 Creation (3 approvers, one rework) | revert notification to the initiator only (nobody had approved yet) | PASS | ["9:002006129"] |
| S1 Creation (3 approvers, one rework) | resubmitted -> creation phase bumps to 1.1, 1.0 archived Reverted (by design) | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|1\|true\|\|false"] |
| S1 Creation (3 approvers, one rework) | all 3 approvers approve | PASS | ["011001116:200","011001011:200","011001277:200"] |
| S1 Creation (3 approvers, one rework) | authorized -> Effective | PASS | Document successfully authorized and made effective. |
| S1 Creation (3 approvers, one rework) | only initiator + authorizer get "Authorized" (creation unchanged) | PASS | ["12:002006129","12:011001011"] |
| S2 Revision (version 2.0 / history) | revision request submitted | PASS | Revision Request submitted successfully. |
| S2 Revision (version 2.0 / history) | request proposes version 2.0 | PASS | 2.0 |
| S2 Revision (version 2.0 / history) | draft opened at 2.0 after request approval (Create/Update screen) | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|2\|true\|\|false","2.0\|1\|true\|\|false"] |
| S2 Revision (version 2.0 / history) | document submitted from Create/Update | PASS | Document Created Successfully |
| S2 Revision (version 2.0 / history) | version STILL 2.0 after submit (this was 2.1 on production) | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|2\|true\|\|false","2.0\|1\|true\|\|false"] |
| S2 Revision (version 2.0 / history) | Revision History while pending: 2.0 Pending Approval, no Reverted on 2.0 | PASS | ["2.0 Pending Approval","1.1 Effective","1.0 Reverted"] |
| S2 Revision (version 2.0 / history) | Revision History after approval (screenshot screen): 2.0 Approved | PASS | ["2.0 Approved","1.1 Effective","1.0 Reverted"] |
| S2 Revision (version 2.0 / history) | authorized -> 2.0 Effective, 1.1 Revised | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|3\|false\|Revised\|false","2.0\|2\|true\|\|false"] |
| S2 Revision (version 2.0 / history) | Next Review Date restarted by the revision (today + 2 years) | PASS | {"before":"2026-10-29","after":"2028-11-09"} |
| S2 Revision (version 2.0 / history) | "Document Revised" notification to everyone in the workflow | PASS | ["20:002006129","20:011001011","20:011001101"] |
| S3 One modal: Signing Authorities + Approval History | the reworked round shows who sent it back: Reworked / Pending / Pending | PASS | ["Muhammad Ghayas Uddin Siddiqui:Reworked","Muhammad Wajahat Shahid:Pending","Abdur Rehman  Qureshi:Pending"] |
| S3 One modal: Signing Authorities + Approval History | finished workflow still returns its full chain (was empty) | PASS | ["Muhammad Ghayas Uddin Siddiqui:Approved","Muhammad Wajahat Shahid:Approved","Abdur Rehman  Qureshi:Approved"] |
| S3 One modal: Signing Authorities + Approval History | every approved step has Decision Approved + both dates | PASS |  |
| S4 Periodic review: re-approve without change | document B Effective with 3 approvers | PASS | ["1.0\|2\|true\|\|false"] |
| S4 Periodic review: re-approve without change | not due yet (review date 2 years away) -> not listed | PASS |  |
| S4 Periodic review: re-approve without change | due in 30 days -> listed for approver Ghayas | PASS | {} |
| S4 Periodic review: re-approve without change | tab badge count > 0 | PASS | 1 |
| S4 Periodic review: re-approve without change | initiator who is NOT an approver cannot re-approve (403) | PASS | Only an approver from this document's approval workflow can re-approve it. |
| S4 Periodic review: re-approve without change | approver re-approves | PASS | Document re-approved. Its next review date is 2028-11-09. |
| S4 Periodic review: re-approve without change | version unchanged | PASS | ["1.0\|2\|true\|\|false"] |
| S4 Periodic review: re-approve without change | Next Review Date = today + Review Years (2) | PASS | 2028-11-09 |
| S4 Periodic review: re-approve without change | second approver trying again is refused (already re-approved) | PASS | This document is not due for review: it is not in force, a revision or obsoletio |
| S4 Periodic review: re-approve without change | other approvers + initiator informed, reviewer not | PASS | ["21:002006129","21:011001011","21:011001277"] |
| S4 Periodic review: re-approve without change | Revision History shows "Re-approved - no change" | PASS | ["1.0 Re-approved - no change","1.0 Effective"] |
| S5 Reject / Revert -> initiator + previous approvers | REVERT by 2nd approver -> initiator + 1st approver (not the one who reverted) | PASS | ["9:002006129","9:011001116"] |
| S5 Reject / Revert -> initiator + previous approvers | REJECT by 2nd approver -> initiator + 1st approver | PASS | ["8:002006129","8:011001116"] |
| S5 Reject / Revert -> initiator + previous approvers | wording differs: "Your document" vs "A document you approved" | PASS | [["002006129","Your document (E2E Reject Revert Policy, Version: 1.1) has been rejected by Muhammad Wajah"],["011001116","A document you approved (E2E Reject Revert Policy, Version: 1.1) has been reje |
| S6 Obsoletion (reworked + revised document) | obsoletion request submitted | PASS | Revision Request submitted successfully. |
| S6 Obsoletion (reworked + revised document) | Obsoletion submit works (was "No valid document content found to promote.") | PASS | Document Created Successfully |
| S6 Obsoletion (reworked + revised document) | document OBSOLETE | PASS | OBSOLETE |
| S6 Obsoletion (reworked + revised document) | version row marked Obsoleted, no false Reverted | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|3\|false\|Revised\|false","2.0\|3\|false\|Obsoleted\|false"] |
| S6 Obsoletion (reworked + revised document) | "Document Obsoleted" goes to initiator + everyone in the workflow | PASS | ["14:002006129","14:011001101"] |
| S0 setup | UAT-like data: NO User Access Levels at all (as in UAT) | PASS | 0 |
| S7 Head steps show the person (name / designation / role) | admin adds "Head of division" to policy 39 | PASS | Workflow Step created successfully. |
| S7 Head steps show the person (name / designation / role) | admin adds "Head of department" to policy 39 | PASS | Workflow Step created successfully. |
| S7 Head steps show the person (name / designation / role) | POLICY SCREEN: Head of Division shows the head's NAME, designation and role (not "Head of Division") | PASS | [{"order":1,"name":"Muhammad Wajahat Shahid","code":"011001011","designation":"Associate Manager - Process Improvement","role":"Process Improvement Manager","headScope":""},{"order":2,"name":"Muhammad |
| S7 Head steps show the person (name / designation / role) | POLICY SCREEN: a Division-level policy cannot know the Department -> keeps the label with a note | PASS | Worked out when submitted, from the document's Department |
| S7 Head steps show the person (name / designation / role) | REQUEST / DOCUMENT FORM preview (Workflow Authorities): names of Division head AND Department head | PASS | [{"order":1,"name":"Muhammad Wajahat Shahid","code":"011001011","designation":"Associate Manager - Process Improvement","role":"Process Improvement Manager","headScope":""},{"order":2,"name":"Muhammad |
| S7 Head steps show the person (name / designation / role) | request submitted | PASS | Document Request submitted successfully. |
| S7 Head steps show the person (name / designation / role) | document submitted: heads resolved from the document's cabinet | PASS | Document Created Successfully |
| S7 Head steps show the person (name / designation / role) | execution steps: Wajahat > Imtiaz (Head of Division) > Nazim (Head of Department) | PASS | ["011001011","011000923","011001245"] |
| S7 Head steps show the person (name / designation / role) | SIGNING AUTHORITIES modal: real names + designations, all Pending, first one received | PASS | [{"no":1,"name":"Muhammad Wajahat Shahid","designation":"Associate Manager - Process Improvement","status":"Pending","received":"yes"},{"no":2,"name":"Muhammad  Imtiaz","designation":"Sr. Director - Q |
| S7 Head steps show the person (name / designation / role) | after Wajahat approves: his row Approved (tick), the heads Pending | PASS | ["Muhammad Wajahat Shahid:Approved","Muhammad  Imtiaz:Pending","Nazim  Uddin:Pending"] |
| S7 Head steps show the person (name / designation / role) | APPROVAL HISTORY data (other screens still use it): real names, not "Head of ..." | PASS | ["1:Muhammad Wajahat Shahid:Approved","2:Muhammad  Imtiaz:Pending","3:Nazim  Uddin:Pending"] |
| S7 Head steps show the person (name / designation / role) | document authorized -> Effective | PASS | Document successfully authorized and made effective. |
| S7b Policy 5 (the screenshot policy), no access levels | Head of Division added to policy 5 | PASS | Workflow Step created successfully. |
| S7b Policy 5 (the screenshot policy), no access levels | row 3 shows the Division head BY NAME with designation and role (not "Head of Division") | PASS | {"name":"Muhammad  Imtiaz","role":"Group Head of Quality Operations","designation":"Sr. Director - Quality Operations"} |
| S7b Policy 5 (the screenshot policy), no access levels | save / reorder keeps it a Head step (not turned into a fixed employee) | PASS | ["011001011/-","002006086/-","-/DIVISION"] |
| S8 Document footer (signatory block) | document downloaded (200, 97729 bytes) | PASS |  |
| S8 Document footer (signatory block) | footer names the Division head and the Department head (by name) | PASS | {"Imtiaz":true,"Nazim":true} |
| S8 Document footer (signatory block) | footer does not say "Head of Division / Department" in place of a person | PASS |  |
| S9 Head cannot be found | no role rule matches the Department -> submit refused with a clear message | PASS | The approval workflow routes this document to the Department Head of Department "Quality Control", but no active employee is set up as that. An employee needs a role that counts as Department Head (se |
| S9 Head cannot be found | a Head of Sub-Department step but the document has no Sub-Department -> refused, says why | PASS | The approval workflow has a "Head of Sub-Department" step, but this document is not filed under a Sub-Department, so there is no sub-department head to send it to. Choose a Sub-Department for the docu |
| S9 Head cannot be found | nothing was started (still draft, no workflow execution) | PASS | DRAFT |
| S10 Data-fix script on real UAT document IT-POL-001 | BEFORE: 2.0 Reverted + 2.1 Effective (false revert, as on production) | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|3\|false\|Revised\|false","2.0\|3\|false\|Reverted\|false","2.1\|2\|true\|\|false"] |
| S10 Data-fix script on real UAT document IT-POL-001 | AFTER: 2.1 renumbered 2.0 and the false Reverted row removed; genuine 1.0 Reverted kept | PASS | ["1.0\|3\|false\|Reverted\|false","1.1\|3\|false\|Revised\|false","2.0\|3\|false\|Reverted\|true","2.0\|2\|true\|\|false"] |
| S10 Data-fix script on real UAT document IT-POL-001 | Revision History after: 2.0 Effective, 1.1 Revised, 1.0 Reverted (genuine) | PASS | ["2.0 \| Effective","1.1 \| Revised","1.0 \| Reverted"] |
| R0 setup | policy 26 starts as Ghayas > Wajahat > Abdur | PASS | ["011001116","011001011","011001277"] |
| R1 Remove an approver from the policy | document submitted with the full list | PASS | Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting |
| R1 Remove an approver from the policy | policy saved without Abdur | PASS | Approval sequence updated successfully. |
| R1 Remove an approver from the policy | the running document no longer goes to Abdur | PASS | Ghayas:ACTIVE > Wajahat:waiting |
| R1 Remove an approver from the policy | it completes after Ghayas and Wajahat: state APPROVED / next stage | PASS | APPROVED |
| R2 Sequence changed | new order Ghayas > Abdur > Wajahat applied to the running document | PASS | Ghayas:ACTIVE > Abdur:waiting > Wajahat:waiting |
| R2 Sequence changed | after Ghayas approves the document goes to ABDUR (not Wajahat) | PASS | Ghayas:Approved > Abdur:ACTIVE > Wajahat:waiting |
| R3 Remove the approver the document is with | Ghayas is out; the document is now with Wajahat | PASS | Wajahat:ACTIVE > Abdur:waiting |
| R3 Remove the approver the document is with | Wajahat is told it is waiting for him | PASS | 1 |
| R3 Remove the approver the document is with | nothing pending with Ghayas for this document any more | PASS | 0 |
| R3 Remove the approver the document is with | (finding) approve-document does not verify the caller is the active approver - same as before this change | PASS | approve-document by a user who is not the active approver returned: 200 Document Approved Successfully |
| R4 Add an approver | Iqbal added to the policy with the + button | PASS | Workflow Step created successfully. |
| R4 Add an approver | the running document now also goes to Iqbal, last | PASS | Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting > Iqbal:waiting |
| R5 Nobody left to approve | document is with Abdur (Ghayas and Wajahat approved) | PASS | Ghayas:Approved > Wajahat:Approved > Abdur:ACTIVE |
| R5 Nobody left to approve | Abdur removed -> workflow completed (both remaining steps are Approved) | PASS | Ghayas:Approved > Wajahat:Approved |
| R5 Nobody left to approve | document moved on after approval (as if the last approver approved) | PASS | APPROVED |
| R5 Nobody left to approve | workflow marked Completed | PASS | Completed |
| R5 Nobody left to approve | initiator told it was approved | PASS |  |
| R5 Nobody left to approve | it can then be authorized -> Effective | PASS | Document successfully authorized and made effective. |
| R6 Head of Division removed after the document was submitted | document goes Wajahat > Imtiaz (Head of Division) > Nazim (Head of Department) | PASS | Wajahat:ACTIVE > Imtiaz:waiting > Nazim:waiting |
| R6 Head of Division removed after the document was submitted | Head of Division removed from the policy -> the document no longer goes to him | PASS | Wajahat:ACTIVE > Nazim:waiting |
| R6 Head of Division removed after the document was submitted | put back -> the document goes to him again, in his place | PASS | Wajahat:ACTIVE > Imtiaz:waiting > Nazim:waiting |
| R7 Policy emptied | removing EVERY step does not touch or auto-approve running documents | PASS | {"before":"Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting","after":"Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting","state":"PENDING_APPROVAL"} |
| R7 Policy emptied | policy restored | PASS | ["011001116","011001011","011001277"] |
| R8 Request workflow follows the policy too | request goes Wajahat > Ghayas | PASS | Wajahat:ACTIVE > Ghayas:waiting |
| R8 Request workflow follows the policy too | Wajahat approved, request is with Ghayas | PASS | Wajahat:Approved > Ghayas:ACTIVE |
| R8 Request workflow follows the policy too | Ghayas taken out -> nobody left -> request approved and its document created | PASS | {"status":"3","doc":"59"} |
| R9 History kept | the approvals already given stay in the chain with their decisions and dates | PASS | ["Muhammad Ghayas Uddin Siddiqui:Approved","Muhammad Wajahat Shahid:Approved"] |
| R9 History kept | every change is in the audit log | PASS | ["Approval workflow updated to follow the policy (removed: 011001277)","Approval workflow updated to follow the policy (order changed)","Approval workflow updated to follow the policy (order changed)" |

## 2. Revision after a rework - the bug the client reported

Document created, sent back once, made Effective (1.1), then revised. Revision History after approval (the screenshot screen):
```
2.0 | Effective
1.1 | Revised
1.0 | Reverted
```

Production showed `2.0 Reverted` + `2.1 Approved` here. Now the revision stays **2.0** at request, at Create/Update, in My Approvals and in Revision History.

## 3. Signing Authorities + Approval History in ONE modal (completed workflow)

| # | Name | Designation | Status | Received on | Status updated on |
|---|---|---|---|---|---|
| 1 | Muhammad Ghayas Uddin Siddiqui | Sr. Manager - IT Infrastructure | Approved | 2026-11-08T13:14:11.949681 | 2026-11-08T13:14:12.812089 |
| 2 | Muhammad Wajahat Shahid | Associate Manager - Process Improvement | Approved | 2026-11-08T13:14:12.812089 | 2026-11-08T13:14:13.393487 |
| 3 | Abdur Rehman  Qureshi | Sr. Associate - Process Improvement | Approved | 2026-11-08T13:14:13.393487 | 2026-11-08T13:14:13.971318 |

Pending example (Head steps):

| # | Name | Designation | Status |
|---|---|---|---|
| 1 | Muhammad Wajahat Shahid | Associate Manager - Process Improvement | Pending |
| 2 | Muhammad  Imtiaz | Sr. Director - Quality Operations | Pending |
| 3 | Nazim  Uddin | General Manager - Quality Assurance | Pending |

## 4. Head of Division / Department - the person is shown, not the label (no User Access Levels exist, as in UAT)

Policy from your screenshot (policy 5, Quality Operations / Quality Assurance / Batch Release and IPC), after adding Head of Division:

| Order | Name | Designation | Role |
|---|---|---|---|
| 1 | Muhammad Wajahat Shahid | Associate Manager - Process Improvement | Process Improvement Manager |
| 2 | Aafaq Ahmad | Sales Promotion Officer | Territory Sales Manager (TSM) |
| 3 | Muhammad  Imtiaz | Sr. Director - Quality Operations | Group Head of Quality Operations |

Policy 39 (Document, Quality Operations) with Head of Division + Head of Department added:

| Where | Order | Name | Designation | Role |
|---|---|---|---|---|
| Policy screen | 1 | Muhammad Wajahat Shahid | Associate Manager - Process Improvement | Process Improvement Manager |
| Policy screen | 2 | Muhammad  Imtiaz | Sr. Director - Quality Operations | Group Head of Quality Operations |
| Policy screen | 3 | Head of Department | undefined | Worked out when submitted, from the document's Department |
| Request / document form preview (Quality Assurance dept) | 1 | Muhammad Wajahat Shahid | Associate Manager - Process Improvement | Process Improvement Manager |
| Request / document form preview (Quality Assurance dept) | 2 | Muhammad  Imtiaz | Sr. Director - Quality Operations | Group Head of Quality Operations |
| Request / document form preview (Quality Assurance dept) | 3 | Nazim  Uddin | General Manager - Quality Assurance | Head of Quality Assurance |

## 5. Periodic review (Due for Review tab)

Row the approver sees: `{"id":48,"documentnumber":"IT-POL-007","title":"E2E Periodic Review Policy","documenttype":"Policy","documenttypecode":"DT-0006","company":"ATCO","division":"Information Technology","department":null,"subdepartment":null,"businessdomain":null,"version":"1.0","nextreviewdate":"2026-12-08","daysleft":30,"reviewperiodyears":2,"lastreviewedat":null,"lastreviewedby":null}`

Review record written:
```
1.0 | 011001116 | Reviewed, still current | 2026-12-08 | 2028-11-09 | 2
```
Revision History afterwards:
```
1.0 | Re-approved - no change | Muhammad Ghayas Uddin Siddiqui | Reviewed, still current
1.0 | Effective | Abdur Rehman  Qureshi | e2e
```

## 6. Document footer / signature block (generated document, doc with Head approvers)

Extracted from the downloaded .docx (`test-evidence/sample_document_with_head_approvers.docx`):
```
Written By     Asif Baig
Reviewed By    Muhammad Wajahat Shahid
Reviewed By    Muhammad Imtiaz        Group Head of Quality Operations   (Head of Division)
Approved By    Nazim Uddin            Head of Quality Assurance          (Head of Department)
Authorized By  Muhammad Wajahat Shahid
```

## 7. Reject / Revert recipients

```
002006129 | Your document (E2E Reject Revert Policy, Version: 1.1) has been rejected by Muhammad Wajah
011001116 | A document you approved (E2E Reject Revert Policy, Version: 1.1) has been rejected by Muha
```
Revised recipients: 20:002006129, 20:011001011, 20:011001101  |  Obsoleted recipients: 14:002006129, 14:011001101

## 8. Data-fix script on the real UAT document IT-POL-001

Before: `1.0|3|false|Reverted|false  1.1|3|false|Revised|false  2.0|3|false|Reverted|false  2.1|2|true||false`  
After:  `1.0|3|false|Reverted|false  1.1|3|false|Revised|false  2.0|3|false|Reverted|true  2.0|2|true||false`  
Revision History after: `2.0 | Effective , 1.1 | Revised , 1.0 | Reverted`

(Run on the clone. On UAT, PART 1 of the script lists exactly this one row - see `test-evidence/uat_false_reverts_part1.txt`.)

## 9. What the real UAT data shows (read-only)

**Documents:**
```
id | documentnumber | title | t | state | versions | nextreviewdate
27 | IT-POL-001 | Testing 14/10 | DT-0006 | EFFECTIVE | 1.0(Reverted) 1.1(Revised) 2.0(Reverted) 2.1 | 2028-10-14
28 | HR-HO-TRA-POL-001 | Testing Hr Policy | DT-0006 | PENDING_APPROVAL | 1.0 | 2028-10-14
29 | IT-POL-002 | Testing  | DT-0006 | APPROVED | 1.0 | 2028-10-14
30 | HR-HO-TRA-POL-002 | Testing - Revision Workflow | DT-0006 | OBSOLETE | 1.0(Revised) 2.0(Obsoleted) | 2028-10-15
31 | IT-II-POL-001 | Testing - IT policy  | DT-0006 | DRAFT | 1.0 | 2028-10-15
32 | IT-POL-003 | test  | DT-0006 | PENDING_APPROVAL | 1.0 | 2028-10-15
33 | IT-POL-004 | Testing AD-hoc approver | DT-0006 | PENDING_APPROVAL | 1.0 | 2028-10-16
34 | IT-II-POL-002 | Testing - Revision | DT-0006 | OBSOLETE | 1.0(Revised) 2.0(Obsoleted) | 2028-10-20
35 | IT-II-POL-003 | Testing Doc 23/10 | DT-0006 | EFFECTIVE | 1.0(Revised) 2.0 | 2028-10-23
36 | IT-POL-005 | testing 26/10 | DT-0006 | EFFECTIVE | 1.0(Reverted) 1.1 | 2028-10-26
37 | 23 | 23 | DT-0006 | EFFECTIVE | 3 | 2026-10-21
38 | IT-II-SOP-001 | Doc-color-test-1 | DT-0001 | PENDING_APPROVAL | 1.0 | 2028-10-27
39 | IT-II-SOP-002 | Doc-color-test-2 | DT-0001 | PENDING_APPROVAL | 1.0 | 2028-10-27
40 | SC-DIS-OP-POL-001 | Testing | DT-0006 | APPROVED | 1.0 | 2028-10-28
41 | QC/STM/RM/013 | ANHYDROUS SODIUM SULPHITE | DT-0006 | EFFECTIVE | 1 | 2027-06-29
42 | IT-II-SOP-003 | Distributionlist all test 1 | DT-0001 | PENDING_APPROVAL | 1.0 | 2028-10-28
43 | IT-II-SOP-004 | Doc-17/10/2026-test 1 | DT-0001 | EFFECTIVE | 1.0 | 2028-10-28
44 | IT-II-POL-004 | Testing 29/10 | DT-0006 | EFFECTIVE | 1.0 | 2028-10-29
45 | IT-II-POL-005 | testung | DT-0006 | PENDING_APPROVAL | 1.0 | 2028-11-04
46 | IT-II-SOP-005 | Doc-Adhock-test-1 | DT-0001 | PENDING_APPROVAL | 1.0 | 2028-11-04
(20 rows)
```
**False reverts (data-fix script, PART 1):**
```
documentnumber | reverted_row_id | shown_as_reverted | live_row_id | live_version_now | live_version_after_fix | live_row_state
IT-POL-001 | 39 | 2.0 | 40 | 2.1 | 2.0 | Effective
(1 row)
```
**Who resolves as Head of each unit (role rules only; UAT has no access levels and none are needed for the unit-named rules):**
```
scope | code | unit | heads
DIVISION | DIV-0001 | Information Technology | -- none (no active holder / no matching role) --
DIVISION | DIV-0002 | Supply Chain | Syed Javed Fatmi [011000866] as Group Head of Supply Chain
DIVISION | DIV-0003 | Quality Operations | Muhammad Imtiaz [011000923] as Group Head of Quality Operations
DIVISION | DIV-0004 | Human Resource | Mehreen Khan [011001280] as Group Head of Human Resources
DEPARTMENT | DIV-0001-DPT-0001 | IT Support | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0001-DPT-0002 | IT Infrastructure | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0002-DPT-0001 | Procurement | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0002-DPT-0002 | Supply Planning | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0002-DPT-0003 | FG Warehouse | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0002-DPT-0004 | Distribution | -- none (no active holder / no matching role) --
DEPARTMENT | DIV-0003-DPT-0001 | Quality Control | Muhammad Adil [011000229] as Head of Quality Control
DEPARTMENT | DIV-0003-DPT-0002 | Quality Assurance | Nazim Uddin [011001245] as Head of Quality Assurance
DEPARTMENT | DIV-0004-DPT-0001 | HR Ops | Qurat Zafar [011001031] as Head of HR Ops
DEPARTMENT | DIV-0004-DPT-0002 | Talent Acquisition | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0002-DPT-0004-SCT-0001 | Order Processing | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0002-DPT-0004-SCT-0002 | Claim Processing | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0003-DPT-0001-SCT-0001 | Packaging and Artwork | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0003-DPT-0001-SCT-0002 | Process - Validation | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0003-DPT-0002-SCT-0001 | Quality Management System | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0003-DPT-0002-SCT-0002 | Batch Release and IPC | -- none (no active holder / no matching role) --
SUBDEPARTMENT | DIV-0003-DPT-0002-SCT-0003 | Validation | Kamran Chandio [011001324] as Validation Manager
SUBDEPARTMENT | DIV-0004-DPT-0001-SCT-0001 | Transport | -- none (no active holder / no matching role) --
(22 rows)
```

## 9b. Running workflows follow the policy (reconcile)

The policy of documents already out for approval is edited; what the running document does (policy 26 = Ghayas > Wajahat > Abdur):

| Policy edit | The running document afterwards |
|---|---|
| Abdur removed | Ghayas:ACTIVE > Wajahat:waiting |
| Order changed to Ghayas > Abdur > Wajahat | Ghayas:ACTIVE > Abdur:waiting > Wajahat:waiting ; after Ghayas approves: Ghayas:Approved > Abdur:ACTIVE > Wajahat:waiting |
| The approver it is with (Ghayas) removed | Wajahat:ACTIVE > Abdur:waiting |
| Iqbal added with the + button | Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting > Iqbal:waiting |
| Head of Division removed (document with Wajahat > Imtiaz > Nazim) | Wajahat:ACTIVE > Nazim:waiting ; put back: Wajahat:ACTIVE > Imtiaz:waiting > Nazim:waiting |

Also tested: the last pending approver removed -> the workflow completes as approved and the document moves on (documents and requests); a policy emptied of ALL steps is never applied; approvals already given stay in the history.

Audit log entries written:
```
Approval workflow updated to follow the policy (removed: 011001277)
Approval workflow updated to follow the policy (order changed)
Approval workflow updated to follow the policy (order changed)
Approval workflow updated to follow the policy (removed: 011001116)
Approval workflow updated to follow the policy (added: 011001116)
Approval workflow updated to follow the policy (added: 011001101)
Approval workflow updated to follow the policy (added: 011001101)
Approval workflow updated to follow the policy (added: 011001101)
Approval workflow updated to follow the policy (removed: 011001101)
Approval workflow updated to follow the policy (removed: 011001101)
Approval workflow updated to follow the policy (removed: 011001101)
Approval workflow updated to follow the policy (removed: 011001277)
Approval workflow updated to follow the policy (removed: 011001277)
Approval workflow updated to follow the policy (removed: 011001277)
Approval workflow updated to follow the policy (removed: 011001277; nobody left, completed)
Approval workflow updated to follow the policy (removed: 011000923)
Approval workflow updated to follow the policy (added: 011000923)
Approval workflow updated to follow the policy (removed: 011000923, 011001245)
Approval workflow updated to follow the policy (added: 011001277)
Approval workflow updated to follow the policy (added: 011001277)
Approval workflow updated to follow the policy (added: 011001277)
```

## 9c. Preview of a Document Request: Distribution List / Document Users inside the document

The downloaded request (`test-evidence/sample_request_preview_with_lists_inside.docx`) now carries them right after the content and before the signature block ("Distribution List" is line 2, "Written By" line 19), and the response says so in the header `X-Preview-Lists-Embedded: true`, so the Preview stops drawing them again under the footer.

## 10. Not covered / things to know

- **Head resolution needs no access level** for the unit-named rules (UAT has 0 User Access Level rows). A unit shows no name only when no active employee holds a matching role (for example IT: the Group Head of Information Technology is inactive; most Supply Chain departments and most Sub-Departments have no matching role) - then the policy screen says so and a submit stops with a clear message. See section 9 for the list.
- Your UAT policy 5 gained its "Head of Division" step after the copy was taken, so the clone was given the same step to reproduce your screenshot.
- **Finding, not changed:** the approve / reject / revert endpoints do not check that the caller is the approver the active step belongs to (the screens only show the item to that person). A hand-made API call by someone else is accepted. Nothing in this change alters it.
- **The screens (Angular) were compiled but not clicked through in a browser** (no login possible here): tab/badge layout, the single Save button, the merged modal's look, the Due for Review tab, the request Preview. Their data endpoints were tested above.
- Emails were queued and their wording checked (earlier a sample went to your address); nothing was sent from these runs.
- UAT document 37 ("23") is overdue for review but has no approval workflow (imported), so nobody can re-approve it - it must be revised.