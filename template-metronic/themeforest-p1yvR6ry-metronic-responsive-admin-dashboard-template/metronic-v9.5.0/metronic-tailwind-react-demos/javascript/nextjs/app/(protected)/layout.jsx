import { headers } from 'next/headers';
import { isDemoId } from '@/lib/demo-id';
import { ProtectedLayoutClient } from './protected-layout-client';

export default async function ProtectedLayout({ children }) {
  const h = await headers();
  const raw = h.get('x-metronic-demo');
  const demo = isDemoId(raw) ? raw : 'demo1';

  return <ProtectedLayoutClient demo={demo}>{children}</ProtectedLayoutClient>;
}
