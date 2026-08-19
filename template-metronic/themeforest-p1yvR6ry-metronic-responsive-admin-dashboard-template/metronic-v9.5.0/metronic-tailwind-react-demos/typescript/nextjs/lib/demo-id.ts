/** Matches demo1 … demo10 only (avoids demo1 vs demo10 ambiguity). */
export const DEMO_SEGMENT_RE = /^\/(demo(?:10|[1-9]))(\/.*)?$/;

export type DemoId =
  | 'demo1'
  | 'demo2'
  | 'demo3'
  | 'demo4'
  | 'demo5'
  | 'demo6'
  | 'demo7'
  | 'demo8'
  | 'demo9'
  | 'demo10';

export const DEMO_IDS: DemoId[] = [
  'demo1',
  'demo2',
  'demo3',
  'demo4',
  'demo5',
  'demo6',
  'demo7',
  'demo8',
  'demo9',
  'demo10',
];

export function isDemoId(value: string | null | undefined): value is DemoId {
  return value != null && (DEMO_IDS as string[]).includes(value);
}
