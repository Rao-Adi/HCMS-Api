const fs = require('fs');
const { execFileSync } = require('child_process');
const L = require('./lib2');
const F = require('./flows');
const { call, sql, one, ensureUsers, users, check, sleep } = L;

const ASIF = '002006129', WAJAHAT = '011001011', IMTIAZ = '011000923', NAZIM = '011001245';
const evidence = {};
const flat = (rows) => rows.map((r) => r.join(' | '));

(async () => {
  await ensureUsers([ASIF, WAJAHAT, IMTIAZ, NAZIM]);
  const B = { division: 'DIV-0003', department: 'DIV-0003-DPT-0002' };

  // ---------- test data in the CLONE only: the real heads get an access level on their unit ----------
  check('S0 setup', 'UAT-like data: NO User Access Levels at all (as in UAT)', Number(one('select count(*) from useraccesslevels')) === 0, one('select count(*) from useraccesslevels'));

  // ---------- S7  Head of Division / Department added to the Document policy of the Quality Operations cabinet (policy 39) ----------
  const S7 = 'S7 Head steps show the person (name / designation / role)';
  for (const scope of ['DIVISION', 'DEPARTMENT']) {
    const r = await call(WAJAHAT, 'POST', 'DMSWorkflowStep/create-workflow-step', { WorkflowPolicyId: 39, EntityType: 'Document', StepType: 'Review', documentTypeCode: 'DT-0006', divisionCode: 'DIV-0003', departmentCode: '', subDepartmentCode: '', businessDomainCode: '', designationCodes: [], roles: [], employeeCodes: [], HeadScope: scope });
    check(S7, 'admin adds "Head of ' + scope.toLowerCase() + '" to policy 39', r.status === 200, r.msg);
  }
  const stepRow = (x) => ({ order: x.StepOrder, name: x.EmployeeName, code: x.EmployeeCode, designation: x.Designation, role: x.UserRole, headScope: x.HeadScope || '' });
  let pr = (await call(WAJAHAT, 'POST', 'DMSWorkflowStep/get-workflow-step-by-document-code', { EntityType: 'Document', WorkflowPolicyId: 39, DocumentTypeCode: 'DT-0006', DivisionCode: 'DIV-0003', DepartmentCode: '', SubDepartmentCode: '', BusinessDomainCode: '' })).body.Data || [];
  const policyScreen = pr.map(stepRow);
  const headDiv = pr.find((x) => x.HeadScope === 'DIVISION');
  check(S7, 'POLICY SCREEN: Head of Division shows the head\'s NAME, designation and role (not "Head of Division")', headDiv && /Imtiaz/i.test(headDiv.EmployeeName) && !!headDiv.Designation && /Group Head of Quality Operations/.test(headDiv.UserRole), policyScreen);
  const headDept = pr.find((x) => x.HeadScope === 'DEPARTMENT');
  check(S7, 'POLICY SCREEN: a Division-level policy cannot know the Department -> keeps the label with a note', headDept && headDept.EmployeeName === 'Head of Department', headDept && headDept.UserRole);
  evidence.policyScreen = policyScreen;
  const pv = (await call(ASIF, 'POST', 'DMSWorkflowStep/get-workflow-step-by-document-code', { EntityType: 'Document', WorkflowPolicyId: 0, DocumentTypeCode: 'DT-0006', DivisionCode: 'DIV-0003', DepartmentCode: 'DIV-0003-DPT-0002', SubDepartmentCode: '', BusinessDomainCode: '' })).body.Data || [];
  const formPreview = pv.map(stepRow);
  check(S7, 'REQUEST / DOCUMENT FORM preview (Workflow Authorities): names of Division head AND Department head', pv.some((x) => /Imtiaz/i.test(x.EmployeeName)) && pv.some((x) => /Nazim/i.test(x.EmployeeName)), formPreview);
  evidence.formPreview = formPreview;

  // ---------- create a document in that cabinet: the heads become the approvers ----------
  let r = await F.createRequest(ASIF, { ...B, name: 'E2E Head Approver Policy' });
  check(S7, 'request submitted', r.status === 200, r.msg);
  await F.approveRequestAll(r.requestId);
  const docH = F.documentIdOfRequest(r.requestId);
  r = await F.submitDocument(ASIF, docH, { ...B, name: 'E2E Head Approver Policy' });
  check(S7, 'document submitted: heads resolved from the document\'s cabinet', r.status === 200, r.msg);
  const execH = F.latestExec('Document', docH);
  const stepsH = sql(`select steporder, trim(assigneduserid) from workflowexecutionsteps where workflowexecutionid=${execH} order by steporder`);
  check(S7, 'execution steps: Wajahat > Imtiaz (Head of Division) > Nazim (Head of Department)', stepsH.map((x) => x[1]).join('>') === [WAJAHAT, IMTIAZ, NAZIM].join('>'), stepsH.map((x) => x[1]));
  const modal = (await call(ASIF, 'GET', 'DMSDocument/get-adhoc-options/' + execH)).body.Data.Steps;
  const modalRows = modal.map((s) => ({ no: s.StepOrder, name: s.EmployeeName, designation: s.Designation, status: s.Decision || 'Pending', received: s.ReceivedOn ? 'yes' : '-' }));
  check(S7, 'SIGNING AUTHORITIES modal: real names + designations, all Pending, first one received', modal.length === 3 && modal.every((s) => s.EmployeeName && !/^Head of/.test(s.EmployeeName) && !!s.Designation), modalRows);
  evidence.signingModalPending = modalRows;
  await F.docAction(docH, 'approve-document');   // Wajahat
  const modal2 = (await call(ASIF, 'GET', 'DMSDocument/get-adhoc-options/' + execH)).body.Data.Steps;
  check(S7, 'after Wajahat approves: his row Approved (tick), the heads Pending', modal2[0].Decision === 'Approved' && !modal2[1].Decision && !modal2[2].Decision, modal2.map((s) => s.EmployeeName + ':' + (s.Decision || 'Pending')));
  const wf = (await call(ASIF, 'GET', `DMSDocumentRequest/get-workflow-details?requestId=${docH}&entityType=Document&decision=All`)).body.Data || [];
  check(S7, 'APPROVAL HISTORY data (other screens still use it): real names, not "Head of ..."', wf.length >= 3 && wf.every((x) => x.EmployeeName && !/^Head of/.test(x.EmployeeName)), wf.map((x) => `${x.StepOrder}:${x.EmployeeName}:${x.Decision || 'Pending'}`));
  await F.docAction(docH, 'approve-document');   // Imtiaz
  await F.docAction(docH, 'approve-document');   // Nazim
  r = await F.authorize(docH);
  check(S7, 'document authorized -> Effective', r.status === 200 && F.state(docH) === 'EFFECTIVE', r.msg);

  // ---------- the policy of the screenshot ----------
  {
    const S7b = 'S7b Policy 5 (the screenshot policy), no access levels';
    const rr = await call(WAJAHAT, 'POST', 'DMSWorkflowStep/create-workflow-step', { WorkflowPolicyId: 5, EntityType: 'Request', StepType: 'Review', documentTypeCode: 'DT-0001', divisionCode: 'DIV-0003', departmentCode: 'DIV-0003-DPT-0002', subDepartmentCode: 'DIV-0003-DPT-0002-SCT-0002', businessDomainCode: '', designationCodes: [], roles: [], employeeCodes: [], HeadScope: 'DIVISION' });
    check(S7b, 'Head of Division added to policy 5', rr.status === 200, rr.msg);
    const sc = (await call(WAJAHAT, 'POST', 'DMSWorkflowStep/get-workflow-step-by-document-code', { EntityType: 'Request', WorkflowPolicyId: 5, DocumentTypeCode: 'DT-0001', DivisionCode: 'DIV-0003', DepartmentCode: 'DIV-0003-DPT-0002', SubDepartmentCode: 'DIV-0003-DPT-0002-SCT-0002', BusinessDomainCode: '' })).body.Data || [];
    const h = sc.find((x) => x.HeadScope === 'DIVISION');
    check(S7b, 'row 3 shows the Division head BY NAME with designation and role (not "Head of Division")', !!h && /Imtiaz/i.test(h.EmployeeName) && /Group Head of Quality Operations/.test(h.UserRole) && !!h.Designation, h && { name: h.EmployeeName, role: h.UserRole, designation: h.Designation });
    evidence.policy5 = sc.map(stepRow);
    const keep = await call(WAJAHAT, 'POST', 'DMSWorkflowStep/update-approval-sequence', { workflowpolicyid: 5, steps: sc.map((x, i) => ({ steporder: i + 1, stepgroup: x.StepGroup || 0, steptype: x.StepType || 'Review', roleid: 0, designationid: 0, userid: x.HeadScope ? '' : (x.EmployeeCode || ''), headscope: x.HeadScope || null, requiresallapprovals: false, canaddadhocapprover: false })) });
    const stored = sql("select coalesce(s.userid,'-')||'/'||coalesce(s.headscope,'-') from workflowstepdefinitions s join workflowpolicyversions v on v.id=s.workflowpolicyversionid and v.isactive where v.workflowpolicyid=5 and s.isactive and not s.isdeleted order by s.steporder").map((x) => x[0]);
    check(S7b, 'save / reorder keeps it a Head step (not turned into a fixed employee)', keep.status === 200 && stored[2] === '-/DIVISION', stored);
  }

  // ---------- footer / signature block of the generated document ----------
  const S8 = 'S8 Document footer (signatory block)';
  try {
    const u = users[ASIF];
    const resp = await fetch('http://localhost:5191/api/DMSDocument/download-submitted-document-template/' + docH, { headers: { login: u.key } });
    const buf = Buffer.from(await resp.arrayBuffer());
    const f = process.env.TEMP + '\\e2e_doc_' + docH + '.docx';
    fs.writeFileSync(f, buf);
    check(S8, 'document downloaded (' + resp.status + ', ' + buf.length + ' bytes)', resp.status === 200 && buf.length > 2000, '');
    const ps = `Add-Type -AssemblyName System.IO.Compression.FileSystem; $z=[IO.Compression.ZipFile]::OpenRead('${f}'); foreach($e in $z.Entries){ if($e.FullName -match '^word/(footer|header|document)\\d*\\.xml$'){ $sr=New-Object IO.StreamReader($e.Open()); $x=$sr.ReadToEnd(); $sr.Close(); '### '+$e.FullName; ([regex]::Replace(([regex]::Replace($x,'</w:p>',"\`n")),'<[^>]+>','')) } }; $z.Dispose()`;
    const text = execFileSync('powershell', ['-NoProfile', '-Command', ps], { encoding: 'utf8', maxBuffer: 1 << 26 });
    fs.writeFileSync(process.env.TEMP + '\\e2e_doc_text.txt', text);
    const hasImtiaz = /Imtiaz/i.test(text), hasNazim = /Nazim/i.test(text);
    check(S8, 'footer names the Division head and the Department head (by name)', hasImtiaz && hasNazim, { Imtiaz: hasImtiaz, Nazim: hasNazim });
    check(S8, 'footer does not say "Head of Division / Department" in place of a person', !/Worked out when submitted/.test(text), '');
    const lines = text.split(/\r?\n/).map((x) => x.trim()).filter((x) => /(Written|Reviewed|Approved|Authorized) By|Imtiaz|Nazim|Shahid|Baig|Head of|Manager|Officer|Associate/i.test(x));
    evidence.footerLines = lines.slice(0, 40);
  } catch (e) { check(S8, 'footer check could not run: ' + String(e.message).slice(0, 120), false, ''); }

  // ---------- head cannot be found -> clear message, nothing starts ----------
  const S9 = 'S9 Head cannot be found';
  const C = { division: 'DIV-0003', department: 'DIV-0003-DPT-0001', name: 'E2E No Head Policy' };
  r = await F.createRequest(ASIF, C);
  await F.approveRequestAll(r.requestId);
  const docN = F.documentIdOfRequest(r.requestId);
  sql("update workflowheadrolerules set isactive=false where companyid=1 and headscope='DEPARTMENT'");
  r = await F.submitDocument(ASIF, docN, C);
  sql("update workflowheadrolerules set isactive=true where companyid=1 and headscope='DEPARTMENT'");
  check(S9, 'no role rule matches the Department -> submit refused with a clear message', r.status === 409 && /Department Head/.test(r.msg), r.msg);
  evidence.noHeadMessage = r.msg;
  r = await call(WAJAHAT, 'POST', 'DMSWorkflowStep/create-workflow-step', { WorkflowPolicyId: 39, EntityType: 'Document', StepType: 'Review', documentTypeCode: 'DT-0006', divisionCode: 'DIV-0003', departmentCode: '', subDepartmentCode: '', businessDomainCode: '', designationCodes: [], roles: [], employeeCodes: [], HeadScope: 'SUBDEPARTMENT' });
  r = await F.submitDocument(ASIF, docN, C);
  check(S9, 'a Head of Sub-Department step but the document has no Sub-Department -> refused, says why', r.status === 409 && /not filed under a Sub-Department/.test(r.msg), r.msg);
  evidence.noUnitMessage = r.msg;
  check(S9, 'nothing was started (still draft, no workflow execution)', F.state(docN) === 'DRAFT' && !sql(`select 1 from workflowexecutions where entitytype='Document' and entityid=${docN}`).length, F.state(docN));

  // ---------- data-fix script on the real UAT document 27 (IT-POL-001): before / after ----------
  const S10 = 'S10 Data-fix script on real UAT document IT-POL-001';
  const hist = async () => (await F.history(WAJAHAT, 27)).map((x) => `${x.Version} | ${x.CurrentStatus}`);
  const beforeFix = await hist(); const beforeVer = F.versions(27);
  const fixSql = fs.readFileSync('D:/Projects/ESSAndAPISrc/ESSAndAPISrc/HCMSCoreAPI/HCMSCoreAPI/HCMS-Api/db-fix-2026-11-08-false-reverted-versions.sql', 'utf8');
  const asCommit = fixSql.replace(/^ROLLBACK;.*$/m, 'COMMIT;');
  fs.writeFileSync(process.env.TEMP + '\\fix_commit_clone.sql', asCommit);
  const out = execFileSync('C:/Program Files/PostgreSQL/18/bin/psql.exe', ['-h', 'localhost', '-U', 'postgres', '-d', L.DB, '-f', process.env.TEMP + '\\fix_commit_clone.sql'], { env: { ...process.env, PGPASSWORD: '12345678' }, encoding: 'utf8' });
  const afterFix = await hist(); const afterVer = F.versions(27);
  check(S10, 'BEFORE: 2.0 Reverted + 2.1 Effective (false revert, as on production)', beforeVer.join(',') === '1.0|3|false|Reverted|false,1.1|3|false|Revised|false,2.0|3|false|Reverted|false,2.1|2|true||false', beforeVer);
  check(S10, 'AFTER: 2.1 renumbered 2.0 and the false Reverted row removed; genuine 1.0 Reverted kept', afterVer.join(',') === '1.0|3|false|Reverted|false,1.1|3|false|Revised|false,2.0|3|false|Reverted|true,2.0|2|true||false', afterVer);
  check(S10, 'Revision History after: 2.0 Effective, 1.1 Revised, 1.0 Reverted (genuine)', afterFix.join(',') === '2.0 | Effective,1.1 | Revised,1.0 | Reverted', afterFix);
  evidence.fix27 = { before: beforeFix, after: afterFix, beforeVersions: beforeVer, afterVersions: afterVer };

  fs.writeFileSync(process.env.TEMP + '\\e2e_part2.json', JSON.stringify({ results: L.results, evidence }, null, 1));
  console.log('\nPART 2 done: ' + L.results.filter((x) => x.ok).length + ' pass, ' + L.results.filter((x) => !x.ok).length + ' fail');
  process.exit(0);
})().catch((e) => { console.error('RUN ERROR', e); process.exit(1); });
