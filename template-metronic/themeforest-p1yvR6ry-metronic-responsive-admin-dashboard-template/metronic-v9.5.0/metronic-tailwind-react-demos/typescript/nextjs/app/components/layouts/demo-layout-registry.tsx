'use client';

import type { ReactNode } from 'react';
import { Demo1Layout } from '@/app/components/layouts/demo1/layout';
import { Demo2Layout } from '@/app/components/layouts/demo2/layout';
import { Demo3Layout } from '@/app/components/layouts/demo3/layout';
import { Demo4Layout } from '@/app/components/layouts/demo4/layout';
import { Demo5Layout } from '@/app/components/layouts/demo5/layout';
import { Demo6Layout } from '@/app/components/layouts/demo6/layout';
import { Demo7Layout } from '@/app/components/layouts/demo7/layout';
import { Demo8Layout } from '@/app/components/layouts/demo8/layout';
import { Demo9Layout } from '@/app/components/layouts/demo9/layout';
import { Demo10Layout } from '@/app/components/layouts/demo10/layout';
import type { DemoId } from '@/lib/demo-id';

const REGISTRY: Record<
  DemoId,
  React.ComponentType<{ children: ReactNode }>
> = {
  demo1: Demo1Layout,
  demo2: Demo2Layout,
  demo3: Demo3Layout,
  demo4: Demo4Layout,
  demo5: Demo5Layout,
  demo6: Demo6Layout,
  demo7: Demo7Layout,
  demo8: Demo8Layout,
  demo9: Demo9Layout,
  demo10: Demo10Layout,
};

export function DemoLayoutById({
  demo,
  children,
}: {
  demo: DemoId;
  children: ReactNode;
}) {
  const Layout = REGISTRY[demo];
  return <Layout>{children}</Layout>;
}
