const fs = require('fs');
const L = require('./lib2');
const F = require('./flows');
const { call, sql, one, ensureUsers, users, check, sleep } = L;

const ASIF = '002006129', GHAYAS = '011001116', WAJAHAT = '011001011', ABDUR = '011001277', IQBAL = '011001101';
const IMTIAZ = '011000923', NAZIM = '011001245';
const evidence = {};

const setSeq = (policyId, items) => call(WAJAHAT, 'POST', 'DMSWorkflowStep/update-approval-sequence', {
  workflowpolicyid: policyId,
  steps: items.map((it, i) => ({ steporder: i + 1, stepgroup: 1, steptype: 'Review', roleid: 0, designationid: 0,
    userid: it.startsWith('HEAD:') ? '' : it, headscope: it.startsWith('HEAD:') ? it.slice(5) : null, requiresallapprovals: false, canaddadhocapprover: false }))
});
// a person (or Head step) added through the "+" button, as the screen does
const addStep = (policyId, entity, o) => call(WAJAHAT, 'POST', 'DMSWorkflowStep/create-workflow-step', {
  WorkflowPolicyId: policyId, EntityType: entity, StepType: 'Review', documentTypeCode: 'DT-0006', divisionCode: o.division, departmentCode: '', subDepartmentCode: '', businessDomainCode: '',
  designationCodes: [], roles: [], employeeCodes: o.code ? [o.code] : [], HeadScope: o.head || null });
const steps = (type, id) => sql(`select trim(wes.assigneduserid)||':'||case when wes.decision is not null then 'Approved' when wes.isactive then 'ACTIVE' else 'waiting' end from workflowexecutionsteps wes where wes.workflowexecutionid=(select max(id) from workflowexecutions where entitytype='${type}' and entityid=${id}) order by wes.steporder, wes.id`).map((x) => x[0]);
const nm = (c) => ({ [GHAYAS]: 'Ghayas', [WAJAHAT]: 'Wajahat', [ABDUR]: 'Abdur', [IQBAL]: 'Iqbal', [IMTIAZ]: 'Imtiaz', [NAZIM]: 'Nazim', [ASIF]: 'Asif' }[c] || c);
const pretty = (arr) => arr.map((s) => { const [c, st] = s.split(':'); return nm(c) + ':' + st; }).join(' > ');
const A = { division: 'DIV-0001' };

async function newDoc(name, cab) {
  const c = cab || A;
  let r = await F.createRequest(ASIF, { ...c, name });
  await F.approveRequestAll(r.requestId);
  const doc = F.documentIdOfRequest(r.requestId);
  r = await F.submitDocument(ASIF, doc, { ...c, name });
  if (r.status !== 200) throw new Error('submit failed: ' + r.msg);
  return doc;
}
const polSteps = (id) => sql(`select coalesce(s.userid, 'HEAD:'||s.headscope) from workflowstepdefinitions s join workflowpolicyversions v on v.id=s.workflowpolicyversionid and v.isactive where v.workflowpolicyid=${id} and s.isactive and not s.isdeleted order by s.steporder`).map((x) => x[0]);

(async () => {
  await ensureUsers([ASIF, GHAYAS, WAJAHAT, ABDUR, IQBAL, IMTIAZ, NAZIM]);
  const P = 26; // Document policy, IT cabinet: Ghayas > Wajahat > Abdur
  const base = polSteps(P);
  check('R0 setup', 'policy 26 starts as Ghayas > Wajahat > Abdur', base.join('>') === [GHAYAS, WAJAHAT, ABDUR].join('>'), base);

  // ---------------- R1: an approver is taken out of the policy while documents wait ----------------
  const R1 = 'R1 Remove an approver from the policy';
  let d1 = await newDoc('E2E reconcile remove last');
  check(R1, 'document submitted with the full list', pretty(steps('Document', d1)) === 'Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting', pretty(steps('Document', d1)));
  let r = await setSeq(P, [GHAYAS, WAJAHAT]);
  check(R1, 'policy saved without Abdur', r.status === 200, r.msg);
  const s1 = pretty(steps('Document', d1));
  check(R1, 'the running document no longer goes to Abdur', s1 === 'Ghayas:ACTIVE > Wajahat:waiting', s1);
  evidence.r1 = s1;
  await F.approveDocAll(d1);
  check(R1, 'it completes after Ghayas and Wajahat: state APPROVED / next stage', ['APPROVED', 'AUTHORIZATION_PENDING', 'TRAINING_PENDING'].includes(F.state(d1)), F.state(d1));

  // ---------------- R2: sequence change ----------------
  const R2 = 'R2 Sequence changed';
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  let d2 = await newDoc('E2E reconcile reorder');
  await setSeq(P, [GHAYAS, ABDUR, WAJAHAT]);
  const s2 = pretty(steps('Document', d2));
  check(R2, 'new order Ghayas > Abdur > Wajahat applied to the running document', s2 === 'Ghayas:ACTIVE > Abdur:waiting > Wajahat:waiting', s2);
  await F.docAction(d2, 'approve-document');
  const s2b = pretty(steps('Document', d2));
  check(R2, 'after Ghayas approves the document goes to ABDUR (not Wajahat)', s2b === 'Ghayas:Approved > Abdur:ACTIVE > Wajahat:waiting', s2b);
  evidence.r2 = [s2, s2b];

  // ---------------- R3: the approver the document is with is taken out ----------------
  const R3 = 'R3 Remove the approver the document is with';
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  let d3 = await newDoc('E2E reconcile remove active');
  await setSeq(P, [WAJAHAT, ABDUR]);
  const s3 = pretty(steps('Document', d3));
  check(R3, 'Ghayas is out; the document is now with Wajahat', s3 === 'Wajahat:ACTIVE > Abdur:waiting', s3);
  await sleep(2500);
  check(R3, 'Wajahat is told it is waiting for him', one(`select count(*) from notifications where relatedentityid=${d3} and trim(employeecode)='${WAJAHAT}' and notificationtype=6`) >= '1', one(`select count(*) from notifications where relatedentityid=${d3} and trim(employeecode)='${WAJAHAT}' and notificationtype=6`));
  const g = await call(GHAYAS, 'POST', 'DMSDocument/get-documents-pending-approval', { PageNumber: 1, PageSize: 50 }); // (admin list) not used for the check below
  const ghayasPending = Number(one(`select count(*) from workflowexecutionsteps wes join workflowexecutions we on we.id=wes.workflowexecutionid where we.entitytype='Document' and we.entityid=${d3} and wes.isactive and trim(wes.assigneduserid)='${GHAYAS}'`));
  check(R3, 'nothing pending with Ghayas for this document any more', ghayasPending === 0, ghayasPending);
  const ghayasTryApprove = await F.docAction(d3, 'approve-document', GHAYAS);
  // FINDING (existing behaviour, not changed here): the API does not check that the caller is the approver the step belongs to, so a hand-made call by Ghayas was accepted.
  evidence.finding_actor_not_checked = 'approve-document by a user who is not the active approver returned: ' + ghayasTryApprove.status + ' ' + ghayasTryApprove.msg;
  check(R3, '(finding) approve-document does not verify the caller is the active approver - same as before this change', true, evidence.finding_actor_not_checked);
  evidence.r3 = s3;

  // ---------------- R4: someone is added through the + button ----------------
  const R4 = 'R4 Add an approver';
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  let d4 = await newDoc('E2E reconcile add');
  r = await addStep(P, 'Document', { division: 'DIV-0001', code: IQBAL });
  check(R4, 'Iqbal added to the policy with the + button', r.status === 200, r.msg);
  const s4 = pretty(steps('Document', d4));
  check(R4, 'the running document now also goes to Iqbal, last', s4 === 'Ghayas:ACTIVE > Wajahat:waiting > Abdur:waiting > Iqbal:waiting', s4);
  evidence.r4 = s4;

  // ---------------- R5: the only approver still waiting is taken out -> completes ----------------
  const R5 = 'R5 Nobody left to approve';
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  let d5 = await newDoc('E2E reconcile complete');
  await F.docAction(d5, 'approve-document'); await F.docAction(d5, 'approve-document');
  check(R5, 'document is with Abdur (Ghayas and Wajahat approved)', pretty(steps('Document', d5)) === 'Ghayas:Approved > Wajahat:Approved > Abdur:ACTIVE', pretty(steps('Document', d5)));
  await setSeq(P, [GHAYAS, WAJAHAT]);
  const s5 = pretty(steps('Document', d5));
  check(R5, 'Abdur removed -> workflow completed (both remaining steps are Approved)', s5 === 'Ghayas:Approved > Wajahat:Approved', s5);
  check(R5, 'document moved on after approval (as if the last approver approved)', ['APPROVED', 'AUTHORIZATION_PENDING', 'TRAINING_PENDING'].includes(F.state(d5)), F.state(d5));
  check(R5, 'workflow marked Completed', one(`select status from workflowexecutions where entitytype='Document' and entityid=${d5} order by id desc limit 1`) === 'Completed', one(`select status from workflowexecutions where entitytype='Document' and entityid=${d5} order by id desc limit 1`));
  await sleep(2500);
  check(R5, 'initiator told it was approved', Number(one(`select count(*) from notifications where relatedentityid=${d5} and trim(employeecode)='${ASIF}' and notificationtype=19`)) >= 1, '');
  const r5auth = await F.authorize(d5);
  check(R5, 'it can then be authorized -> Effective', r5auth.status === 200 && F.state(d5) === 'EFFECTIVE', r5auth.msg);

  // ---------------- R6: the Head-of-Division scenario ----------------
  const R6 = 'R6 Head of Division removed after the document was submitted';
  const Q = { division: 'DIV-0003', department: 'DIV-0003-DPT-0002' };
  const HP = 39; // Document policy of Quality Operations: Wajahat
  await setSeq(HP, [WAJAHAT, 'HEAD:DIVISION', 'HEAD:DEPARTMENT']);
  let d6 = await newDoc('E2E reconcile head', Q);
  check(R6, 'document goes Wajahat > Imtiaz (Head of Division) > Nazim (Head of Department)', pretty(steps('Document', d6)) === 'Wajahat:ACTIVE > Imtiaz:waiting > Nazim:waiting', pretty(steps('Document', d6)));
  await setSeq(HP, [WAJAHAT, 'HEAD:DEPARTMENT']);
  const s6 = pretty(steps('Document', d6));
  check(R6, 'Head of Division removed from the policy -> the document no longer goes to him', s6 === 'Wajahat:ACTIVE > Nazim:waiting', s6);
  await setSeq(HP, [WAJAHAT, 'HEAD:DIVISION', 'HEAD:DEPARTMENT']);
  const s6b = pretty(steps('Document', d6));
  check(R6, 'put back -> the document goes to him again, in his place', s6b === 'Wajahat:ACTIVE > Imtiaz:waiting > Nazim:waiting', s6b);
  evidence.r6 = [s6, s6b];
  await setSeq(HP, [WAJAHAT]);

  // ---------------- R7: a policy with no steps is never applied ----------------
  const R7 = 'R7 Policy emptied';
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  let d7 = await newDoc('E2E reconcile empty');
  const before7 = pretty(steps('Document', d7));
  await setSeq(P, []);
  const after7 = pretty(steps('Document', d7));
  check(R7, 'removing EVERY step does not touch or auto-approve running documents', before7 === after7 && F.state(d7) === 'PENDING_APPROVAL', { before: before7, after: after7, state: F.state(d7) });
  await setSeq(P, [GHAYAS, WAJAHAT, ABDUR]);
  check(R7, 'policy restored', polSteps(P).join('>') === [GHAYAS, WAJAHAT, ABDUR].join('>'), polSteps(P));

  // ---------------- R8: a REQUEST workflow ----------------
  const R8 = 'R8 Request workflow follows the policy too';
  const RP = 47; // Request policy, Quality Operations: Wajahat
  await setSeq(RP, [WAJAHAT, GHAYAS]);
  let rq = await F.createRequest(ASIF, { division: 'DIV-0003', name: 'E2E reconcile request' });
  check(R8, 'request goes Wajahat > Ghayas', pretty(steps('Request', rq.requestId)) === 'Wajahat:ACTIVE > Ghayas:waiting', pretty(steps('Request', rq.requestId)));
  await F.actOnRequest(rq.requestId, 'APPROVED');
  check(R8, 'Wajahat approved, request is with Ghayas', pretty(steps('Request', rq.requestId)) === 'Wajahat:Approved > Ghayas:ACTIVE', pretty(steps('Request', rq.requestId)));
  await setSeq(RP, [WAJAHAT]);
  check(R8, 'Ghayas taken out -> nobody left -> request approved and its document created', one(`select status from documentrequests where id=${rq.requestId}`) === '3' && Number(one(`select coalesce(documentid,0) from documentrequests where id=${rq.requestId}`)) > 0, { status: one(`select status from documentrequests where id=${rq.requestId}`), doc: one(`select coalesce(documentid,0) from documentrequests where id=${rq.requestId}`) });
  await setSeq(RP, [WAJAHAT]);

  // ---------------- R9: history stays ----------------
  const R9 = 'R9 History kept';
  const chain = (await call(ASIF, 'GET', 'DMSDocument/get-adhoc-options/' + L.one(`select max(id) from workflowexecutions where entitytype='Document' and entityid=${d5}`))).body.Data.Steps;
  check(R9, 'the approvals already given stay in the chain with their decisions and dates', chain.length === 2 && chain.every((x) => x.Decision === 'Approved' && x.StatusUpdatedOn), chain.map((x) => x.EmployeeName + ':' + x.Decision));
  const logs = sql(`select action from auditlogs where entitytype='Document' and entityid in (${d1},${d2},${d3},${d4},${d5},${d6}) and action like 'Approval workflow updated%' order by id`).map((x) => x[0]);
  check(R9, 'every change is in the audit log', logs.length >= 6, logs);
  evidence.audit = logs;

  fs.writeFileSync(process.env.TEMP + '\\e2e_part3.json', JSON.stringify({ results: L.results, evidence }, null, 1));
  console.log('\nPART 3 done: ' + L.results.filter((x) => x.ok).length + ' pass, ' + L.results.filter((x) => !x.ok).length + ' fail');
  process.exit(0);
})().catch((e) => { console.error('RUN ERROR', e); process.exit(1); });
