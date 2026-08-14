'use client';

import * as React from 'react';
import { Separator } from '@/components/ui/separator';
import { Card } from '@/app/real-estate/pages/card';
import { Map } from '@/app/real-estate/pages/map';

export default function RealEstatePage() {
  const [isMapExpanded, setIsMapExpanded] = React.useState(false);

  return (
    <div className="flex h-[calc(100vh-var(--header-height))]">
      {!isMapExpanded && (
        <>
          <div className="w-1/2 overflow-auto">
            <Card />
          </div>
          <Separator orientation="vertical" />
        </>
      )}
      <div className={isMapExpanded ? 'w-full' : 'w-1/2'}>
        <Map
          isExpanded={isMapExpanded}
          onToggleExpand={() => setIsMapExpanded(!isMapExpanded)}
        />
      </div>
    </div>
  );
}
