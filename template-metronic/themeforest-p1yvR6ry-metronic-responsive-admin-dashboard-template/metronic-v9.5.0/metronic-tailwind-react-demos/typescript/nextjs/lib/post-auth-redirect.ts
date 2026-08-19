import { DEMO_SEGMENT_RE, type DemoId } from '@/lib/demo-id';

function withSearch(path: string, search: string): string {
  if (!search) return path;
  const q = search.startsWith('?') ? search.slice(1) : search;
  return q ? `${path}?${q}` : path;
}

/**
 * Builds the post-sign-in path. `usePathname()` may return the browser path
 * (`/demo5/account`) or the rewritten internal path (`/account`); handle both.
 */
export function callbackPathForDemoRoute(
  demo: DemoId,
  pathname: string,
  search: string,
): string {
  const normalized = pathname && pathname !== '' ? pathname : '/';

  const seg = normalized.match(DEMO_SEGMENT_RE);
  if (seg && seg[1] === demo) {
    const rest = seg[2];
    const path =
      rest == null || rest === '' || rest === '/'
        ? `/${demo}/`
        : `/${demo}${rest}`;
    return withSearch(path, search);
  }

  const path =
    normalized === '/' ? `/${demo}/` : `/${demo}${normalized}`;
  return withSearch(path, search);
}

/** Reject open redirects; only same-origin relative paths. */
export function safeCallbackUrl(
  raw: string | null | undefined,
  fallback = '/',
): string {
  if (raw == null || raw === '') return fallback;
  let decoded: string;
  try {
    decoded = decodeURIComponent(raw.trim());
  } catch {
    return fallback;
  }
  if (!decoded.startsWith('/') || decoded.startsWith('//')) {
    return fallback;
  }
  return decoded;
}
