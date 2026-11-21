import fs from 'fs';
import path from 'path';
import vm from 'vm';

function findHtmlFiles(dir) {
  const files = [];
  for (const item of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, item.name);
    if (item.isDirectory()) {
      if (item.name !== 'node_modules' && item.name !== '.git') {
        files.push(...findHtmlFiles(full));
      }
    } else if (item.name.endsWith('.html')) {
      files.push(full);
    }
  }
  return files;
}

const htmlFiles = findHtmlFiles('frontend');
let hasError = false;

for (const f of htmlFiles) {
  const content = fs.readFileSync(f, 'utf8');
  const scripts = [...content.matchAll(/<script(?:\s+type="module")?>([\s\S]*?)<\/script>/g)];
  for (let i = 0; i < scripts.length; i++) {
    const code = scripts[i][1];
    try {
      new vm.SourceTextModule(code);
      console.log(`✅ [OK] ${f} (script #${i + 1})`);
    } catch (err) {
      console.error(`❌ [SYNTAX ERROR] in ${f} (script #${i + 1}):`, err.message);
      hasError = true;
    }
  }
}

if (hasError) {
  process.exit(1);
} else {
  console.log('\n🎉 ALL SCRIPTS IN ALL HTML FILES HAVE VALID SYNTAX!');
}
