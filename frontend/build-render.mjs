import fs from 'node:fs';
import path from 'node:path';

const rawHost = (process.env.API_PUBLIC_HOST || '').trim();
const rawUrl = (process.env.API_PUBLIC_URL || '').trim();

let apiBase = '';
if (rawUrl) {
  apiBase = `${rawUrl.replace(/\/+$/, '')}/api`;
} else if (rawHost) {
  const withProto = /^https?:\/\//i.test(rawHost) ? rawHost : `https://${rawHost}`;
  apiBase = `${withProto.replace(/\/+$/, '')}/api`;
}

const outPath = path.resolve('frontend/assets/js/runtime-config.js');
fs.writeFileSync(outPath, `export const RUNTIME_API_BASE_URL = ${JSON.stringify(apiBase)};\n`, 'utf8');
console.log('Generated runtime-config.js ->', apiBase || '(empty - localhost fallback)');
