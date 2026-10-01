// Compares two outputs of db-schema-fingerprint.sql and reports what the target
// environment is missing. Read-only: it prints findings, it changes nothing.
//
//   node db-compare-schema.js schema-dev.txt schema-client.txt
//
// Exit code 1 if the target is missing anything, so it can gate a deployment.
const fs = require('fs');

const [refFile, targetFile] = process.argv.slice(2);
if (!refFile || !targetFile) {
  console.error('usage: node db-compare-schema.js <reference.txt> <target.txt>');
  process.exit(2);
}

const load = f => new Set(
  fs.readFileSync(f, 'utf8').split(/\r?\n/).map(l => l.trim()).filter(Boolean)
);

const ref = load(refFile);
const target = load(targetFile);

const missing = [...ref].filter(l => !target.has(l)).sort();
const extra = [...target].filter(l => !ref.has(l)).sort();

const group = lines => lines.reduce((m, l) => {
  const kind = l.split('|')[0];
  (m[kind] = m[kind] || []).push(l);
  return m;
}, {});

function report(title, lines) {
  console.log('\n' + title);
  console.log('='.repeat(title.length));
  if (!lines.length) { console.log('  (none)'); return; }
  const g = group(lines);
  for (const kind of Object.keys(g).sort()) {
    console.log(`\n  ${kind} (${g[kind].length})`);
    for (const l of g[kind]) {
      const [, a, b, c] = l.split('|');
      console.log(`    ${a}${b ? '.' + b : ''}${c ? '  :: ' + c : ''}`);
    }
  }
}

console.log(`reference : ${refFile}  (${ref.size} objects)`);
console.log(`target    : ${targetFile}  (${target.size} objects)`);

report('MISSING ON TARGET -- must be added before deploying', missing);
report('PRESENT ON TARGET ONLY -- check these are intentional', extra);

console.log('\n---------------------------------------------------');
if (missing.length === 0) {
  console.log('RESULT: the target has every object the reference has. Safe to deploy.');
} else {
  console.log(`RESULT: ${missing.length} object(s) missing. DO NOT deploy until these are added.`);
}
console.log('---------------------------------------------------');
process.exit(missing.length ? 1 : 0);
