// Test framework against the UAT clone (local ATCO_DMS_UAT). Never touches UAT itself.
const { execFileSync } = require('child_process');
const net = require('net');
const fs = require('fs');

const API = 'http://localhost:5191/api/';
const DB = 'ATCO_DMS_UAT';
const PSQL = 'C:/Program Files/PostgreSQL/18/bin/psql.exe';
const users = {}; // empcode -> { key, empid, name }

function sql(q) {
  const out = execFileSync(PSQL, ['-h', 'localhost', '-U', 'postgres', '-d', DB, '-At', '-F', '\u0001', '-c', q],
    { env: { ...process.env, PGPASSWORD: '12345678' }, encoding: 'utf8' });
  return out.split(/\r?\n/).filter(Boolean).map((l) => l.split('\u0001'));
}
const one = (q) => (sql(q)[0] || [])[0];

async function redisSet(cmds) {
  const resp = (a) => '*' + a.length + '\r\n' + a.map((x) => '$' + Buffer.byteLength(x) + '\r\n' + x + '\r\n').join('');
  return new Promise((resolve) => {
    const s = net.createConnection({ host: '127.0.0.1', port: 6379 }, () => cmds.forEach(([k, v]) => s.write(resp(['SET', k, v, 'EX', '28800']))));
    let b = ''; s.on('data', (d) => (b += d));
    setTimeout(() => { s.end(); resolve((b.match(/\+OK/g) || []).length); }, 600);
  });
}

// Makes a session for each employee code (real people of the UAT clone), so the API can be called AS them.
async function ensureUsers(codes) {
  const need = codes.filter((c) => !users[c]);
  const sqlServer = [];
  const cmds = [];
  for (const code of need) {
    const row = sql(`select empid, trim(coalesce(firstname,'')||' '||coalesce(midname,'')||' '||coalesce(lastname,'')) from tblemployee where trim(empcode)='${code}' and companyid=1 order by active desc, empid desc limit 1`)[0];
    if (!row) throw new Error('employee not found in clone: ' + code);
    const empid = +row[0]; const name = row[1].replace(/\s+/g, ' ');
    const key = 'qaU' + code;
    users[code] = { key, empid, name, code };
    const uid = 'qa.u' + code;
    const login = JSON.stringify([{ CompanyId: '1', CompanyName: 'ATCO Laboratories Limited', Password: 'aaa', UserIdCode: code, UserId: uid, CompetitorAllowed: 'Yes', Culture: 'en-GB', WebErp: 'Yes', Application: 'true' }]);
    const other = JSON.stringify([{ UICulture: 'en-GB', Culture: 'en-GB', UserIdCode: code, UserId: uid, userid: uid, FileName: uid + '.xml', FilePath: '', CompanyId: '1', CompanyName: 'ATCO', CompanyName_UserWise: '', BaseCompanyId: '', EmpId: String(empid), PGid: '', PGIDS: '0', EmpName: name, Logo: '' }]);
    cmds.push([key + 'DecryptLoginDetail', login], [key + 'OtherDataHRIS', other], [key + 'IsLogin', 'true'], [key + 'LoginDetail', login]);
    sqlServer.push({ key, code, empid, uid: uid.toUpperCase(), first: name.split(' ')[0], last: name.split(' ').slice(-1)[0] });
  }
  if (!need.length) return;
  await redisSet(cmds);
  const ps = `
$ErrorActionPreference='Stop'
function Run($db,$q){ $c=New-Object System.Data.SqlClient.SqlConnection("Data Source=.\\SQLEXPRESS;Initial Catalog=$db;Integrated Security=True;Connect Timeout=10"); $c.Open(); $m=$c.CreateCommand(); $m.CommandText=$q; [void]$m.ExecuteNonQuery(); $c.Close() }
${sqlServer.map((u) => `
Run 'QA_Security' "IF NOT EXISTS (SELECT 1 FROM tblUniqueKeyForRedis WHERE UniqueKey='${u.key}') INSERT INTO tblUniqueKeyForRedis(UniqueKey,EntTerminal,EntTerminalIP) VALUES ('${u.key}','${u.key}','127.0.0.1')"
Run 'QA_HCMS' "IF NOT EXISTS (SELECT 1 FROM tblEmployee WHERE EmpId=${u.empid}) INSERT INTO tblEmployee VALUES (${u.empid},'${u.code}','${u.first.replace(/'/g, "''")}',NULL,'${u.last.replace(/'/g, "''")}',1); IF NOT EXISTS (SELECT 1 FROM EXTERNAL_SECURITY_USERMAPPING WHERE UserId='${u.uid}') INSERT INTO EXTERNAL_SECURITY_USERMAPPING VALUES ('${u.uid}',${u.empid},1)"`).join('\n')}
`;
  const f = process.env.TEMP + '\\ensure_users.ps1';
  fs.writeFileSync(f, ps);
  execFileSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', f], { encoding: 'utf8' });
}

async function call(code, method, path, body, form) {
  const u = users[code]; if (!u) throw new Error('no session for ' + code);
  const h = { login: u.key }; let b;
  if (form) b = form; else if (body !== undefined) { h['Content-Type'] = 'application/json'; b = JSON.stringify(body); }
  const r = await fetch(API + path, { method, headers: h, body: b });
  const t = await r.text(); let j; try { j = JSON.parse(t); } catch { j = t; }
  return { status: r.status, body: j, msg: (j && j.Message) || '' };
}

const results = [];
function check(scenario, name, ok, evidence) {
  results.push({ scenario, name, ok: !!ok, evidence: evidence === undefined ? '' : (typeof evidence === 'string' ? evidence : JSON.stringify(evidence)) });
  console.log((ok ? 'PASS' : 'FAIL') + ' | ' + scenario + ' | ' + name + (evidence !== undefined ? ' | ' + (typeof evidence === 'string' ? evidence : JSON.stringify(evidence)) : ''));
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
module.exports = { sql, one, ensureUsers, call, users, check, results, sleep, DB };
