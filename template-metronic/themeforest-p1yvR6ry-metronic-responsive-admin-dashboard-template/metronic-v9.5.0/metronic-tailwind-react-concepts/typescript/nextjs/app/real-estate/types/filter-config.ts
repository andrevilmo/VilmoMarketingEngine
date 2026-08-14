export type FilterKey = "propertyType" | "condition" | "area" | "price" | "dates";

export interface FilterConfig {
  key: FilterKey;
  label: string;
  component: React.ComponentType;
}
