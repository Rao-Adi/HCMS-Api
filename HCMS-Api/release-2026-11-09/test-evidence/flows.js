const { call, sql, one, ensureUsers, users, check, sleep } = require('./lib2');

const DIST = [{ CompanyId: 1, DivisionCode: '', RoleId: 1 }];

// ---- requests ----
async function createRequest(creator, o) {
  const f = new FormData();
  f.append('DocumentRequestTypeCode', o.type || 'DRT-0001');
  f.append('DocumentTypeCode', o.docType || 'DT-0006');
  f.append('DocumentName', o.name);
  f.append('Justification', o.justification || 'e2e');
  f.append('ProposedContent', '<p>Content ' + Date.now() + '</p>');
  f.append('DivisionCode', o.division);
  if (o.department) f.append('DepartmentCode', o.department);
  if (o.sub) f.append('SubDepartmentCode', o.sub);
  if (o.parent) f.append('ParentDocumentId', String(o.parent));
  f.append('DistributionList[0].CompanyId', '1');
  f.append('DistributionList[0].DivisionCode', o.division);
  f.append('DistributionList[0].RoleId', '1');
  const path = o.parent ? 'DMSDocumentRequest/create-and-submit-revision-document-request' : 'DMSDocumentRequest/create-and-submit-document-request';
  const r = await call(creator, 'POST', path, undefined, f);
  return { ...r, requestId: r.body && r.body.Data };
}

function activeStep(entityType, entityId) {
  const row = sql(`select wes.id, trim(wes.assigneduserid), we.id from workflowexecutionsteps wes join workflowexecutions we on we.id=wes.workflowexecutionid
    where we.entitytype='${entityType}' and we.entityid=${entityId} and wes.isactive order by we.id desc, wes.id limit 1`)[0];
  return row ? { stepId: +row[0], code: row[1], execId: +row[2] } : null;
}
function latestExec(entityType, entityId) {
  return +one(`select id from workflowexecutions where entitytype='${entityType}' and entityid=${entityId} order by id desc limit 1`);
}

async function actOnRequest(requestId, action, who) {
  const s = activeStep('Request', requestId); if (!s) return null;
  const code = who || s.code; await ensureUsers([code]);
  return call(code, 'POST', 'DMSDocumentRequest/take-workflow-action', { EmpId: users[code].empid, StepId: s.stepId, Action: action, Observation: 'e2e ' + action });
}
async function approveRequestAll(requestId) {
  const done = [];
  for (let i = 0; i < 8; i++) { const s = activeStep('Request', requestId); if (!s) break; const r = await actOnRequest(requestId, 'APPROVED'); done.push(s.code + ':' + r.status); }
  return done;
}
function documentIdOfRequest(requestId) { return +one(`select documentid from documentrequests where id=${requestId}`); }

// ---- documents ----
async function submitDocument(creator, docId, o) {
  const f = new FormData();
  f.append('DocumentId', String(docId));
  f.append('ProposedContent', '<p>Content ' + Date.now() + '</p>');
  f.append('DocumentTypeCode', o.docType || 'DT-0006');
  f.append('DocumentName', o.name);
  f.append('DivisionCode', o.division);
  if (o.department) f.append('DepartmentCode', o.department);
  if (o.sub) f.append('SubDepartmentCode', o.sub);
  f.append('Justification', 'e2e');
  f.append('ActivityTypeCode', o.act || 'DRT-0001');
  if (o.parent) f.append('ParentDocumentId', String(o.parent));
  f.append('ReplaceDistributions', 'true');
  f.append('distributionlist', JSON.stringify([{ CompanyId: 1, DivisionCode: o.division, RoleId: 1 }]));
  return call(creator, 'POST', 'DMSDocument/submit-document', undefined, f);
}
async function docAction(docId, endpoint, who) {
  const e = latestExec('Document', docId);
  const s = sql(`select trim(assigneduserid) from workflowexecutionsteps where workflowexecutionid=${e} and isactive limit 1`)[0];
  const code = who || (s && s[0]); if (!code) return null;
  await ensureUsers([code]);
  return call(code, 'POST', 'DMSDocument/' + endpoint, { EmpId: users[code].empid, DocumentId: docId, ExecutionId: e, Observation: 'e2e ' + endpoint });
}
async function approveDocAll(docId) {
  const done = [];
  for (let i = 0; i < 8; i++) {
    const e = latestExec('Document', docId);
    const s = sql(`select trim(assigneduserid) from workflowexecutionsteps where workflowexecutionid=${e} and isactive limit 1`)[0];
    if (!s) break; const r = await docAction(docId, 'approve-document'); done.push(s[0] + ':' + r.status);
  }
  return done;
}
async function authorize(docId, who, action) {
  const code = who || '011001011'; await ensureUsers([code]);
  return call(code, 'POST', 'DMSDocument/authorize-document-post-training', { DocumentId: docId, Observation: 'e2e', Action: action || 'APPROVED' });
}

const versions = (docId) => sql(`select version||'|'||versiontype||'|'||isactive||'|'||coalesce(archivereason,'')||'|'||coalesce(isdeleted::text,'f') from documentversions where documentid=${docId} order by id`).map((r) => r[0]);
const state = (docId) => one(`select s.code from documentstatehistory h join documentstates s on s.id=h.tostateid where h.documentid=${docId} order by h.changedat desc, h.id desc limit 1`);
const notifs = (docId, types) => sql(`select notificationtype, trim(employeecode) from notifications where relatedentityid=${docId} and notificationtype in (${types}) order by id`).map((r) => r[0] + ':' + r[1]);
const history = async (creator, docId) => (await call(creator, 'GET', 'DMSDocumentRequest/get-document-revision-history?documentId=' + docId)).body.Data || [];

module.exports = { createRequest, activeStep, latestExec, actOnRequest, approveRequestAll, documentIdOfRequest, submitDocument, docAction, approveDocAll, authorize, versions, state, notifs, history, DIST };
