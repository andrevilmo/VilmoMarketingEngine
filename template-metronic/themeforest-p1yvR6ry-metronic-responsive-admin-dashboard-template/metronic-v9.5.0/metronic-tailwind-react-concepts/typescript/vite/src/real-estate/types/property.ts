export interface Property {
  id: string;
  propertyType: string;
  address: string;
  city: string;
  state: string;
  zipCode: string;
  name: string;
  price: number;
  yearBuilt?: number;
  images: string[];
  isFavorite?: boolean;
  coordinates?: [number, number]; // [lat, lng] - optional for card view, required for map
}
