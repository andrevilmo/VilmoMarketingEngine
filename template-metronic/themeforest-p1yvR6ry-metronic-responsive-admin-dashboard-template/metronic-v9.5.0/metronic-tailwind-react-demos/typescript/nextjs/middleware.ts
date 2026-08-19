import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { DEMO_SEGMENT_RE } from '@/lib/demo-id';

const AUTH_PATH =
  /^\/(signin|signup|reset-password|verify-email|change-password)(\/|$)/;

/** Path already starts with /demo1 … /demo10 */
const HAS_DEMO_PREFIX = /^\/(demo(?:10|[1-9]))(\/|$)/;

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (pathname === '/' || pathname === '') {
    const url = request.nextUrl.clone();
    url.pathname = '/demo1/';
    return NextResponse.redirect(url);
  }

  if (AUTH_PATH.test(pathname)) {
    return NextResponse.next();
  }

  const demoMatch = pathname.match(DEMO_SEGMENT_RE);
  if (demoMatch) {
    const demo = demoMatch[1];
    const rest = demoMatch[2] ?? '/';
    const innerPath = rest === '' ? '/' : rest;

    const url = request.nextUrl.clone();
    url.pathname = innerPath;

    const requestHeaders = new Headers(request.headers);
    requestHeaders.set('x-metronic-demo', demo);

    return NextResponse.rewrite(url, {
      request: { headers: requestHeaders },
    });
  }

  if (pathname.startsWith('/demo')) {
    const url = request.nextUrl.clone();
    url.pathname = '/demo1/';
    return NextResponse.redirect(url);
  }

  if (!HAS_DEMO_PREFIX.test(pathname)) {
    const url = request.nextUrl.clone();
    url.pathname = `/demo1${pathname}`;
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api/|_next/static|_next/image|favicon.ico).*)'],
};
