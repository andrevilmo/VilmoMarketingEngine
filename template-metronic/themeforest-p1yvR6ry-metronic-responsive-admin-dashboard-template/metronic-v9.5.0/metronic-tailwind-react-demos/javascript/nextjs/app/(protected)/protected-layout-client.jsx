'use client';

import { useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { callbackPathForDemoRoute } from '@/lib/post-auth-redirect';
import { ScreenLoader } from '@/components/common/screen-loader';
import { DemoLayoutById } from '@/app/components/layouts/demo-layout-registry';

export function ProtectedLayoutClient({ demo, children }) {
  const { data: session, status } = useSession();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status === 'unauthenticated') {
      const search =
        typeof window !== 'undefined' ? window.location.search : '';
      const callbackPath = callbackPathForDemoRoute(demo, pathname, search);
      const q = new URLSearchParams({
        callbackUrl: callbackPath,
      });
      router.push(`/signin?${q.toString()}`);
    }
  }, [status, router, demo, pathname]);

  if (status === 'loading') {
    return <ScreenLoader />;
  }

  return session ? (
    <DemoLayoutById demo={demo}>{children}</DemoLayoutById>
  ) : null;
}
