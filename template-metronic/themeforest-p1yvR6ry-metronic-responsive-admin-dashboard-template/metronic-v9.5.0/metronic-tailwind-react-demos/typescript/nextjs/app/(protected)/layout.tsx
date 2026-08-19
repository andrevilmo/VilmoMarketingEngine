import { headers } from 'next/headers';
import { ProtectedLayoutClient } from './protected-layout-client';
import { isDemoId, type DemoId } from '@/lib/demo-id';

export default async function ProtectedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const h = await headers();
  const raw = h.get('x-metronic-demo');
  const demo: DemoId = isDemoId(raw) ? raw : 'demo1';

  return <ProtectedLayoutClient demo={demo}>{children}</ProtectedLayoutClient>;
}
