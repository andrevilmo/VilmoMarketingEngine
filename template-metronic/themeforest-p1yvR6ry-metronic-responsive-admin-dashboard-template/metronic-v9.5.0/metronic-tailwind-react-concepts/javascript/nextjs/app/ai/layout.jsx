'use client';

import { useEffect, useState } from 'react';
import { DefaultLayout } from '@/components/layouts/ai';
import { ScreenLoader } from '@/components/screen-loader';

export default function AiLayout({ children }) {
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Simulate short loading time
    const timer = setTimeout(() => {
      setIsLoading(false);
    }, 1000); // 1 second loading time

    return () => clearTimeout(timer);
  }, []);

  if (isLoading) {
    return <ScreenLoader />;
  }

  return <DefaultLayout>{children}</DefaultLayout>;
}
