import type { LucideIcon } from "lucide-react";

export interface MenuItem {
  title: string;
  path: string;
  icon?: LucideIcon;
  children?: {
    title: string;
    path: string;
    desc?: string;
  }[];
}
