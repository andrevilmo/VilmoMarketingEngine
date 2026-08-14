import type { FilterConfig } from '../types';
import { Area } from '@/components/layouts/real-estate/components/area';
import { Dates } from '@/components/layouts/real-estate/components/dates';
import { Price } from '@/components/layouts/real-estate/components/price';
import { Condition } from '@/components/layouts/real-estate/components/condition';
import { PropertyType } from '@/components/layouts/real-estate/components/property-type';

export const filtersConfig: FilterConfig[] = [
  { key: "propertyType", label: "Type", component: PropertyType },
  { key: "condition", label: "Condition", component: Condition },
  { key: "area", label: "Area", component: Area },
  { key: "price", label: "Price", component: Price },
  { key: "dates", label: "Dates", component: Dates },
];
