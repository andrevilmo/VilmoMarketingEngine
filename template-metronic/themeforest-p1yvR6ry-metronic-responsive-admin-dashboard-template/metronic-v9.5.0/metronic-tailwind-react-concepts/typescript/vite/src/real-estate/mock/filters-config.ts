import type { FilterConfig } from '../types';
import { Area } from '../layout/components/area';
import { Dates } from '../layout/components/dates';
import { Price } from '../layout/components/price';
import { Condition } from '../layout/components/condition';
import { PropertyType } from '../layout/components/property-type';

export const filtersConfig: FilterConfig[] = [
  { key: "propertyType", label: "Type", component: PropertyType },
  { key: "condition", label: "Condition", component: Condition },
  { key: "area", label: "Area", component: Area },
  { key: "price", label: "Price", component: Price },
  { key: "dates", label: "Dates", component: Dates },
];
