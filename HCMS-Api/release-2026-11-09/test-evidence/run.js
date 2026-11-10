const fs = require('fs');
const L = require('./lib2');
const F = require('./flows');
const { call, sql, one, ensureUsers, users, check, sleep } = L;

const ASIF = '002006129', GHAYAS = '011001116', WAJAHAT = '011001011', ABDUR = '011001277', IQBAL = '011001101';
const IMTIAZ = '011000923', NAZIM = '011001245';
const ymd = (d) => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
const today = () => ymd(new Date());
const plus = (y) => { const d = new Date(); d.setFullYear(d.getFullYear() + y); return ymd(d); };
const evidence = {};

(async () => {
  await ensureUsers([ASIF, GHAYAS, WAJAHAT, ABDUR, IQBAL, IMTIAZ, NAZIM]);
  const A = { division: 'DIV-0001' };

  // =====================================================================================================
  // S1  Creation with a rework (the client's precondition), 3 approvers in the document workflow
  // =====================================================================================================
  const S1 = 'S1 Creation (3 approvers, one rework)';
  let r = await F.createRequest(ASIF, { ...A, name: 'E2E Wireless Access Policy' });
  check(S1, 'request submitted', r.status === 200, { id: r.requestId, msg: r.msg });
  const req1 = r.requestId;
  const apr1 = await F.approveRequestAll(req1);
  check(S1, 'request approved by its approver', apr1.length >= 1 && apr1.every((x) => x.endsWith(':200')), apr1);
  const docA = F.documentIdOfRequest(req1);
  check(S1, 'document created, draft version 1.0', JSON.stringify(F.versions(docA)) === JSON.stringify(['1.0|1|true||false']), F.versions(docA));
  r = await F.submitDocument(ASIF, docA, { ...A, name: 'E2E Wireless Access Policy' });
  check(S1, 'document submitted', r.status === 200, r.msg);
  const steps1 = sql(`select trim(assigneduserid) from workflowexecutionsteps where workflowexecutionid=${F.latestExec('Document', docA)} order by steporder`).map((x) => x[0]);
  check(S1, 'document workflow has 3 steps: Ghayas > Wajahat > Abdur', JSON.stringify(steps1) === JSON.stringify([GHAYAS, WAJAHAT, ABDUR]), steps1);
  r = await F.docAction(docA, 'send-back-for-rework');
  check(S1, '1st approver sends it back (rework)', r.status === 200, r.msg);
  await L.sleep(3000);
  check(S1, 'revert notification to the initiator only (nobody had approved yet)', JSON.stringify(F.notifs(docA, '9')) === JSON.stringify(['9:' + ASIF]), F.notifs(docA, '9'));
  r = await F.submitDocument(ASIF, docA, { ...A, name: 'E2E Wireless Access Policy' });
  check(S1, 'resubmitted -> creation phase bumps to 1.1, 1.0 archived Reverted (by design)', r.status === 200 && F.versions(docA).join(',') === '1.0|3|false|Reverted|false,1.1|1|true||false', F.versions(docA));
  const ap = await F.approveDocAll(docA);
  check(S1, 'all 3 approvers approve', ap.length === 3 && ap.every((x) => x.endsWith(':200')), ap);
  r = await F.authorize(docA);
  check(S1, 'authorized -> Effective', r.status === 200 && F.state(docA) === 'EFFECTIVE', r.msg);
  await L.sleep(3000);
  check(S1, 'only initiator + authorizer get "Authorized" (creation unchanged)', F.notifs(docA, '12').sort().join(',') === [`12:${ASIF}`, `12:${WAJAHAT}`].sort().join(','), F.notifs(docA, '12'));
  evidence.docA = { id: docA, number: one(`select documentnumber from documents where id=${docA}`), versions: F.versions(docA) };
  sql(`update documents set nextreviewdate=current_date-10 where id=${docA}`);
  const reviewDateAfterCreate = one(`select nextreviewdate from documents where id=${docA}`);

  // =====================================================================================================
  // S2  Revision after that rework: version must stay 2.0, no false "Reverted"
  // =====================================================================================================
  const S2 = 'S2 Revision (version 2.0 / history)';
  r = await F.createRequest(ASIF, { ...A, name: 'E2E Wireless Access Policy', type: 'DRT-0002', parent: docA });
  check(S2, 'revision request submitted', r.status === 200, r.msg);
  const req2 = r.requestId;
  check(S2, 'request proposes version 2.0', one(`select rowversion from documentrequests where id=${req2}`) === '2.0', one(`select rowversion from documentrequests where id=${req2}`));
  await F.approveRequestAll(req2);
  check(S2, 'draft opened at 2.0 after request approval (Create/Update screen)', F.versions(docA).includes('2.0|1|true||false'), F.versions(docA));
  const draftBefore = F.versions(docA);
  r = await F.submitDocument(ASIF, docA, { ...A, name: 'E2E Wireless Access Policy', act: 'DRT-0002', parent: docA });
  check(S2, 'document submitted from Create/Update', r.status === 200, r.msg);
  check(S2, 'version STILL 2.0 after submit (this was 2.1 on production)', F.versions(docA).includes('2.0|1|true||false') && !F.versions(docA).some((v) => v.startsWith('2.1')), F.versions(docA));
  const h1 = await F.history(ASIF, docA);
  check(S2, 'Revision History while pending: 2.0 Pending Approval, no Reverted on 2.0', h1.some((x) => x.Version === '2.0' && /Pending/i.test(x.CurrentStatus)) && !h1.some((x) => x.Version === '2.0' && /Revert/i.test(x.CurrentStatus)), h1.map((x) => x.Version + ' ' + x.CurrentStatus));
  await F.approveDocAll(docA);
  const h2 = await F.history(ASIF, docA);
  check(S2, 'Revision History after approval (screenshot screen): 2.0 Approved', h2.some((x) => x.Version === '2.0' && /Approved/i.test(x.CurrentStatus)), h2.map((x) => x.Version + ' ' + x.CurrentStatus));
  r = await F.authorize(docA);
  check(S2, 'authorized -> 2.0 Effective, 1.1 Revised', r.status === 200 && F.versions(docA).join(',') === '1.0|3|false|Reverted|false,1.1|3|false|Revised|false,2.0|2|true||false', F.versions(docA));
  const dateAfterRevision = one(`select nextreviewdate from documents where id=${docA}`);
  check(S2, 'Next Review Date restarted by the revision (today + 2 years)', dateAfterRevision === plus(2), { before: reviewDateAfterCreate, after: dateAfterRevision });
  await L.sleep(3500);
  const n20 = F.notifs(docA, '20');
  check(S2, '"Document Revised" notification to everyone in the workflow', n20.length >= 2, n20);
  evidence.revisionRecipients = n20;
  evidence.revisionHistory = (await F.history(ASIF, docA)).map((x) => `${x.Version} | ${x.CurrentStatus}`);

  // =====================================================================================================
  // S3  Signing authorities + approval history in ONE modal (get-adhoc-options)
  // =====================================================================================================
  const S3 = 'S3 One modal: Signing Authorities + Approval History';
  const execRework = sql(`select we.id from workflowexecutions we where we.entitytype='Document' and we.entityid=${docA} and we.status='Reworked' order by we.id`)[0][0];
  const execCreate = sql(`select we.id from workflowexecutions we where we.entitytype='Document' and we.entityid=${docA} and we.status='Completed' order by we.id`)[0][0];
  const optRw = (await call(GHAYAS, 'GET', 'DMSDocument/get-adhoc-options/' + execRework)).body.Data;
  check(S3, 'the reworked round shows who sent it back: Reworked / Pending / Pending', optRw.Steps.length === 3 && optRw.Steps[0].Decision === 'Reworked', optRw.Steps.map((x) => x.EmployeeName + ':' + (x.Decision || 'Pending')));
  const opt = (await call(IQBAL, 'GET', 'DMSDocument/get-adhoc-options/' + execCreate)).body.Data;
  check(S3, 'finished workflow still returns its full chain (was empty)', opt.Steps && opt.Steps.length === 3, (opt.Steps || []).map((s) => `${s.EmployeeName}:${s.Decision || 'Pending'}`));
  check(S3, 'every approved step has Decision Approved + both dates', (opt.Steps || []).every((s) => s.Decision === 'Approved' && s.ReceivedOn && s.StatusUpdatedOn), '');
  evidence.signingModal = (opt.Steps || []).map((s) => ({ no: s.StepOrder, name: s.EmployeeName, designation: s.Designation, status: s.Decision || 'Pending', receivedOn: s.ReceivedOn, updatedOn: s.StatusUpdatedOn }));

  // =====================================================================================================
  // S4  Periodic review (re-approve without change) on a 3-approver document
  // =====================================================================================================
  const S4 = 'S4 Periodic review: re-approve without change';
  r = await F.createRequest(ASIF, { ...A, name: 'E2E Periodic Review Policy' });
  await F.approveRequestAll(r.requestId);
  const docB = F.documentIdOfRequest(r.requestId);
  await F.submitDocument(ASIF, docB, { ...A, name: 'E2E Periodic Review Policy' });
  await F.approveDocAll(docB); await F.authorize(docB);
  check(S4, 'document B Effective with 3 approvers', F.state(docB) === 'EFFECTIVE', F.versions(docB));
  let list = (await call(GHAYAS, 'POST', 'DMSDocument/get-documents-due-for-review', { PageNumber: 1, PageSize: 20, SortBy: 'ASC', SortColumn: 'NextReviewDate' })).body.Data;
  check(S4, 'not due yet (review date 2 years away) -> not listed', !(list.Items || []).some((x) => (x.Id || x.id) === docB), '');
  sql(`update documents set nextreviewdate=current_date+30 where id=${docB}`);
  list = (await call(GHAYAS, 'POST', 'DMSDocument/get-documents-due-for-review', { PageNumber: 1, PageSize: 20, SortBy: 'ASC', SortColumn: 'NextReviewDate' })).body.Data;
  const rowB = (list.Items || []).find((x) => (x.Id || x.id) === docB);
  check(S4, 'due in 30 days -> listed for approver Ghayas', !!rowB, rowB && { version: rowB.Version, next: rowB.NextReviewDate, left: rowB.DaysLeft, years: rowB.ReviewPeriodYears });
  const cnt = (await call(GHAYAS, 'GET', 'DMSDocument/get-documents-due-for-review-count')).body.Data;
  check(S4, 'tab badge count > 0', cnt >= 1, cnt);
  r = await call(ASIF, 'POST', 'DMSDocument/reapprove-document-review', { DocumentId: docB, Remarks: 'x' });
  check(S4, 'initiator who is NOT an approver cannot re-approve (403)', r.status === 403, r.msg);
  const before = F.versions(docB);
  r = await call(GHAYAS, 'POST', 'DMSDocument/reapprove-document-review', { DocumentId: docB, Remarks: 'Reviewed, still current' });
  check(S4, 'approver re-approves', r.status === 200, r.msg);
  check(S4, 'version unchanged', JSON.stringify(F.versions(docB)) === JSON.stringify(before), F.versions(docB));
  check(S4, 'Next Review Date = today + Review Years (2)', one(`select nextreviewdate from documents where id=${docB}`) === plus(2), one(`select nextreviewdate from documents where id=${docB}`));
  r = await call(WAJAHAT, 'POST', 'DMSDocument/reapprove-document-review', { DocumentId: docB, Remarks: 'again' });
  check(S4, 'second approver trying again is refused (already re-approved)', r.status === 409, r.msg.slice(0, 80));
  await L.sleep(3500);
  const n21 = F.notifs(docB, '21').sort();
  check(S4, 'other approvers + initiator informed, reviewer not', n21.join(',') === [`21:${WAJAHAT}`, `21:${ABDUR}`, `21:${ASIF}`].sort().join(','), n21);
  const hb = await F.history(ASIF, docB);
  check(S4, 'Revision History shows "Re-approved - no change"', hb.some((x) => /Re-approved/i.test(x.CurrentStatus)), hb.map((x) => x.Version + ' ' + x.CurrentStatus));
  evidence.reviewRow = rowB; evidence.reviewHistory = hb.map((x) => `${x.Version} | ${x.CurrentStatus} | ${x.ApprovedBy || ''} | ${x.Justification || ''}`);
  evidence.reviewRecord = sql(`select version, reviewedby, remarks, previousnextreviewdate::text, newnextreviewdate::text, reviewperiodyears from documentreviews where documentid=${docB}`);

  // =====================================================================================================
  // S5  Reject / Revert notifications to initiator + previous approvers
  // =====================================================================================================
  const S5 = 'S5 Reject / Revert -> initiator + previous approvers';
  r = await F.createRequest(ASIF, { ...A, name: 'E2E Reject Revert Policy' });
  await F.approveRequestAll(r.requestId);
  const docC = F.documentIdOfRequest(r.requestId);
  await F.submitDocument(ASIF, docC, { ...A, name: 'E2E Reject Revert Policy' });
  await F.docAction(docC, 'approve-document');             // Ghayas approves
  r = await F.docAction(docC, 'send-back-for-rework');     // Wajahat reverts
  await L.sleep(3000);
  const n9 = F.notifs(docC, '9').sort();
  check(S5, 'REVERT by 2nd approver -> initiator + 1st approver (not the one who reverted)', n9.join(',') === [`9:${ASIF}`, `9:${GHAYAS}`].sort().join(','), n9);
  await F.submitDocument(ASIF, docC, { ...A, name: 'E2E Reject Revert Policy' });
  await F.docAction(docC, 'approve-document');
  r = await F.docAction(docC, 'reject-document');
  await L.sleep(3000);
  const n8 = F.notifs(docC, '8').sort();
  check(S5, 'REJECT by 2nd approver -> initiator + 1st approver', n8.join(',') === [`8:${ASIF}`, `8:${GHAYAS}`].sort().join(','), n8);
  const msgs = sql(`select trim(employeecode), left(message,90) from notifications where relatedentityid=${docC} and notificationtype=8 order by id`);
  check(S5, 'wording differs: "Your document" vs "A document you approved"', msgs.some((m) => /^Your document/.test(m[1])) && msgs.some((m) => /^A document you approved/.test(m[1])), msgs);
  evidence.rejectMessages = msgs;

  // =====================================================================================================
  // S6  Obsoletion of the document that was reworked + revised (the production error)
  // =====================================================================================================
  const S6 = 'S6 Obsoletion (reworked + revised document)';
  r = await F.createRequest(ASIF, { ...A, name: 'E2E Wireless Access Policy', type: 'DRT-0003', parent: docA });
  check(S6, 'obsoletion request submitted', r.status === 200, r.msg);
  await F.approveRequestAll(r.requestId);
  sql(`update documentroledistributions set isretrieved=true where documentid=${docA}`);
  r = await F.submitDocument(ASIF, docA, { ...A, name: 'E2E Wireless Access Policy', act: 'DRT-0003', parent: docA });
  check(S6, 'Obsoletion submit works (was "No valid document content found to promote.")', r.status === 200, r.msg);
  await F.approveDocAll(docA);
  await L.sleep(3500);
  check(S6, 'document OBSOLETE', F.state(docA) === 'OBSOLETE', F.state(docA));
  check(S6, 'version row marked Obsoleted, no false Reverted', F.versions(docA).some((v) => v.includes('Obsoleted')), F.versions(docA));
  const n14 = F.notifs(docA, '14');
  check(S6, '"Document Obsoleted" goes to initiator + everyone in the workflow', n14.length >= 2, n14);
  evidence.obsoleteRecipients = n14;

  fs.writeFileSync(process.env.TEMP + '\\e2e_part1.json', JSON.stringify({ results: L.results, evidence }, null, 1));
  console.log('\nPART 1 done: ' + L.results.filter((x) => x.ok).length + ' pass, ' + L.results.filter((x) => !x.ok).length + ' fail');
  process.exit(0);
})().catch((e) => { console.error('RUN ERROR', e); process.exit(1); });
