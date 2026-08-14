import { NextResponse } from 'next/server';

export async function GET() {
  return NextResponse.json(
    {
      status: 'ok',
      timestamp: new Date().toISOString(),
      service: 'metronic-react-concepts-nextjs',
    },
    { status: 200 },
  );
}
