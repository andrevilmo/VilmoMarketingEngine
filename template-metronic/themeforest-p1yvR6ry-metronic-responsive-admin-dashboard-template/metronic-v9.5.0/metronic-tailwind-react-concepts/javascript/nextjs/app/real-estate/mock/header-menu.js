import { Building2, Heart, Home, Key, MapPin, TrendingUp } from 'lucide-react';

export const MENU_HEADER = [
  {
    title: 'Buy',
    path: '#',
    icon: Home,
    children: [
      {
        title: 'Houses',
        path: '#',
        desc: 'Find your dream home',
      },
      {
        title: 'Apartments',
        path: '#',
        desc: 'Modern city living',
      },
      {
        title: 'Condos',
        path: '#',
        desc: 'Low-maintenance lifestyle',
      },
      {
        title: 'Land',
        path: '#',
        desc: 'Build your vision',
      },
    ],
  },
  {
    title: 'Rent',
    path: '#',
    icon: Key,
    children: [
      {
        title: 'Long-term Rentals',
        path: '#',
        desc: 'Annual or monthly leases',
      },
      {
        title: 'Short-term Rentals',
        path: '#',
        desc: 'Vacation & temporary stays',
      },
      {
        title: 'Room Shares',
        path: '#',
        desc: 'Shared living spaces',
      },
    ],
  },
  {
    title: 'Sell',
    path: '#',
    icon: TrendingUp,
  },
  {
    title: 'Commercial',
    path: '#',
    icon: Building2,
  },
  {
    title: 'Saved',
    path: '#',
    icon: Heart,
  },
  {
    title: 'Map View',
    path: '#',
    icon: MapPin,
  },
];
