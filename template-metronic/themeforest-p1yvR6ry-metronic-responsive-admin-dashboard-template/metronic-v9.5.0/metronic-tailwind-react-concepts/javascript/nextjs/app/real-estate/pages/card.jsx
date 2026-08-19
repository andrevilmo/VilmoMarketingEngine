'use client';

import * as React from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Heart,
  MapPin,
  Phone,
  Share2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Toolbar,
  ToolbarActions,
  ToolbarDescription,
  ToolbarHeading,
  ToolbarPageTitle,
} from '@/components/layouts/real-estate/components/toolbar';
import { properties } from '@/app/real-estate/mock';

// Format price with commas
function formatPrice(price) {
  return price.toLocaleString('en-US');
}

// Simple Image Carousel without external dependencies
function ImageCarousel({ images, alt, onFavoriteClick, isFavorite }) {
  const [current, setCurrent] = React.useState(0);
  const [isHovered, setIsHovered] = React.useState(false);

  const scrollPrev = React.useCallback(
    (e) => {
      e.stopPropagation();
      setCurrent((prev) => (prev === 0 ? images.length - 1 : prev - 1));
    },
    [images.length],
  );

  const scrollNext = React.useCallback(
    (e) => {
      e.stopPropagation();
      setCurrent((prev) => (prev === images.length - 1 ? 0 : prev + 1));
    },
    [images.length],
  );

  return (
    <div
      className="relative w-full h-full overflow-hidden"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {/* Images Container */}
      <div
        className="flex h-full transition-transform duration-300 ease-out"
        style={{ transform: `translateX(-${current * 100}%)` }}
      >
        {images.map((image, index) => (
          <div key={index} className="w-full h-full shrink-0">
            <img
              src={image}
              alt={`${alt} - Image ${index + 1}`}
              className="w-full h-full object-cover"
            />
          </div>
        ))}
      </div>

      {/* Top Actions - Favorite & Share */}
      <div className="absolute top-3 right-3 flex gap-2">
        <Button
          variant="outline"
          shape="circle"
          mode="icon"
          onClick={(e) => {
            e.stopPropagation();
          }}
        >
          <Share2 className="size-4" />
        </Button>
        <Button
          variant="outline"
          shape="circle"
          mode="icon"
          className={cn(isFavorite ? 'bg-rose-500 hover:bg-rose-600' : '')}
          onClick={(e) => {
            e.stopPropagation();
            onFavoriteClick?.();
          }}
        >
          <Heart
            className={cn(
              'size-4 transition-colors',
              isFavorite ? 'text-white fill-white' : '',
            )}
          />
        </Button>
      </div>

      {/* Image Count */}
      <div className="absolute bottom-3 right-3 bg-black/60 text-white text-xs font-medium px-2 py-1 rounded-md backdrop-blur-sm">
        {current + 1} / {images.length}
      </div>

      {/* Navigation Arrows */}
      <div
        className={cn(
          'absolute inset-x-3 top-1/2 -translate-y-1/2 flex justify-between pointer-events-none transition-opacity duration-200',
          isHovered ? 'opacity-100' : 'opacity-0',
        )}
      >
        <Button
          className="pointer-events-auto"
          variant="outline"
          shape="circle"
          mode="icon"
          onClick={scrollPrev}
        >
          <ChevronLeft className="size-4" />
        </Button>
        <Button
          className="pointer-events-auto"
          variant="outline"
          shape="circle"
          mode="icon"
          onClick={scrollNext}
        >
          <ChevronRight className="size-4" />
        </Button>
      </div>

      {/* Dots Indicator */}
      <div className="absolute bottom-3 left-1/2 -translate-x-1/2 flex gap-1.5">
        {images.map((_, index) => (
          <button
            key={index}
            onClick={(e) => {
              e.stopPropagation();
              setCurrent(index);
            }}
            className={cn(
              'size-2 rounded-full transition-all duration-200 shadow-sm',
              current === index
                ? 'bg-white scale-110'
                : 'bg-white/60 hover:bg-white/80',
            )}
          />
        ))}
      </div>
    </div>
  );
}

function PropertyCard({ property }) {
  const [isFavorite, setIsFavorite] = React.useState(
    property.isFavorite || false,
  );
  const priceDisplay = `$${formatPrice(property.price)}`;

  return (
    <div className="group cursor-pointer bg-white dark:bg-zinc-950 rounded-2xl overflow-hidden shadow-sm hover:shadow-md transition-all duration-300 flex flex-col">
      {/* Image Carousel */}
      <div className="relative aspect-4/3 overflow-hidden">
        <ImageCarousel
          images={property.images}
          alt={property.name}
          isFavorite={isFavorite}
          onFavoriteClick={() => setIsFavorite(!isFavorite)}
        />
      </div>

      {/* Property Info */}
      <div className="p-4 space-y-3 flex flex-col flex-1">
        {/* Price */}
        <div className="flex items-baseline justify-between">
          <span className="text-2xl font-semibold leading-none text-foreground">
            {priceDisplay}
          </span>
        </div>

        {/* Property Type & Year */}
        <div className="flex items-center gap-2">
          <Badge variant="secondary" appearance="outline">
            {property.propertyType}
          </Badge>
          {property.yearBuilt && (
            <span className="text-sm font-normal text-muted-foreground">
              Built {property.yearBuilt}
            </span>
          )}
        </div>

        {/* Address */}
        <div className="flex items-start gap-2">
          <MapPin className="size-4 text-gray-400" />
          <div className="flex flex-col gap-0.5">
            <span className="text-sm font-semibold leading-none text-foreground">
              {property.address}
            </span>
            <span className="text-sm font-normal text-muted-foreground">
              {property.city}, {property.state} {property.zipCode}
            </span>
          </div>
        </div>

        {/* Contact Button */}
        <Button
          className="mx-auto w-full mt-auto"
          variant="mono"
          onClick={(e) => e.stopPropagation()}
        >
          <Phone />
          Contact Agent
        </Button>
      </div>
    </div>
  );
}

export function Card() {
  const [sortBy, setSortBy] = React.useState('price-asc');

  const sortedProperties = React.useMemo(() => {
    const sorted = [...properties];

    switch (sortBy) {
      case 'price-asc':
        return sorted.sort((a, b) => a.price - b.price);
      case 'price-desc':
        return sorted.sort((a, b) => b.price - a.price);
      case 'date-asc':
        return sorted.sort((a, b) => (a.yearBuilt || 0) - (b.yearBuilt || 0));
      case 'date-desc':
        return sorted.sort((a, b) => (b.yearBuilt || 0) - (a.yearBuilt || 0));
      default:
        return sorted;
    }
  }, [sortBy]);

  return (
    <div className="flex flex-col h-full p-5">
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Properties in New York</ToolbarPageTitle>
          <ToolbarDescription>
            {properties.length} properties found
          </ToolbarDescription>
        </ToolbarHeading>

        <ToolbarActions>
          <Select
            value={sortBy}
            onValueChange={setSortBy}
            indicatorPosition="right"
          >
            <SelectTrigger>
              <SelectValue placeholder="Sort by" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="price-asc">Low to High</SelectItem>
              <SelectItem value="price-desc">High to Low</SelectItem>
              <SelectItem value="date-asc">Old to New</SelectItem>
              <SelectItem value="date-desc">New to Old</SelectItem>
            </SelectContent>
          </Select>
        </ToolbarActions>
      </Toolbar>

      {/* Property Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
        {sortedProperties.map((property) => (
          <PropertyCard key={property.id} property={property} />
        ))}
      </div>
    </div>
  );
}
