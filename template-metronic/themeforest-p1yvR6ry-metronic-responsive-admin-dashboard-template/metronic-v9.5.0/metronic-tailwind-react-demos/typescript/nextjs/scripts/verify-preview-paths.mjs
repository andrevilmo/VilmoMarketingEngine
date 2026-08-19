#!/usr/bin/env node
/**
 * Smoke checklist for local/staging (NEXT_PUBLIC_BASE_PATH=/metronic/tailwind/nextjs).
 *
 * Usage:
 *   PREVIEW_BASE_URL=https://keenthemes.com node scripts/verify-preview-paths.mjs
 *   node scripts/verify-preview-paths.mjs http://127.0.0.1:3000   # requires `next start` with port published
 *
 * Default Compose does not publish 3000 on the host; check inside the container instead:
 *   docker exec metronic-tailwind-react-nextjs wget -qO- http://127.0.0.1:3000/metronic/tailwind/nextjs/api/health
 */
const base =
  process.argv[2] ||
  process.env.PREVIEW_BASE_URL ||
  'http://127.0.0.1:3000';
const bp = '/metronic/tailwind/nextjs';
const paths = [
  `${bp}/api/health`,
  `${bp}/demo1/`,
  `${bp}/demo10/network/get-started`,
];

async function main() {
  console.log(`Checking ${base} …`);
  for (const p of paths) {
    const url = new URL(p, base).href;
    try {
      const res = await fetch(url, { redirect: 'manual' });
      console.log(res.status, url);
    } catch (e) {
      console.error('FAIL', url, e.message);
      process.exitCode = 1;
    }
  }
}

main();
