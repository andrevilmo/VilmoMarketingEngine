'use client';

import * as React from 'react';
import {
  ArrowLeft,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  Heart,
  MapPin,
  Phone,
  Share2,
  X,
} from 'lucide-react';
import {
  MapContainer as LeafletMapContainer,
  Marker as LeafletMarker,
  Popup as LeafletPopup,
  TileLayer,
  useMap,
} from 'react-leaflet';
import { cn } from '@/lib/utils';
import 'leaflet/dist/leaflet.css';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { properties } from '@/app/real-estate/mock';

// Typed wrappers to fix react-leaflet v5 type issues
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const MapContainer = LeafletMapContainer;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const Marker = LeafletMarker;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const Popup = LeafletPopup;

// Get Leaflet instance (loaded via react-leaflet)
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const getLeaflet = () => window.L;

// Format price with commas
function formatPrice(price) {
  return price.toLocaleString('en-US');
}

// Simple Image Carousel without external dependencies
function ImageCarousel({ images, alt, onFavoriteClick, isFavorite, onClose }) {
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
      className="relative w-full h-full overflow-hidden rounded-t-lg"
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
      <div className="absolute top-2 right-2 flex gap-1.5">
        <Button
          variant="outline"
          size="sm"
          mode="icon"
          shape="circle"
          onClick={(e) => {
            e.stopPropagation();
          }}
        >
          <Share2 className="size-3.5" />
        </Button>
        <Button
          variant="outline"
          shape="circle"
          size="sm"
          mode="icon"
          className={cn(isFavorite ? 'bg-rose-500 hover:bg-rose-600' : '')}
          onClick={(e) => {
            e.stopPropagation();
            onFavoriteClick?.();
          }}
        >
          <Heart
            className={cn(
              'size-3.5 transition-colors',
              isFavorite ? 'text-white fill-white' : '',
            )}
          />
        </Button>
        <Button
          variant="outline"
          size="sm"
          mode="icon"
          shape="circle"
          onClick={(e) => {
            e.stopPropagation();
            onClose?.();
          }}
        >
          <X className="size-3.5" />
        </Button>
      </div>

      {/* Image Count */}
      <div className="absolute bottom-2 right-2 bg-black/60 text-white text-[10px] font-medium px-1.5 py-0.5 rounded backdrop-blur-sm">
        {current + 1} / {images.length}
      </div>

      {/* Navigation Arrows */}
      <div
        className={cn(
          'absolute inset-x-2 top-1/2 -translate-y-1/2 flex justify-between pointer-events-none transition-opacity duration-200',
          isHovered ? 'opacity-100' : 'opacity-0',
        )}
      >
        <Button
          className="pointer-events-auto"
          shape="circle"
          variant="outline"
          size="sm"
          mode="icon"
          onClick={scrollPrev}
        >
          <ChevronLeft className="size-3.5" />
        </Button>
        <Button
          className="pointer-events-auto"
          shape="circle"
          variant="outline"
          size="sm"
          mode="icon"
          onClick={scrollNext}
        >
          <ChevronRight className="size-3.5" />
        </Button>
      </div>

      {/* Dots Indicator */}
      <div className="absolute bottom-2 left-1/2 -translate-x-1/2 flex gap-1">
        {images.map((_, index) => (
          <button
            key={index}
            onClick={(e) => {
              e.stopPropagation();
              setCurrent(index);
            }}
            className={cn(
              'size-1.5 rounded-full transition-all duration-200 shadow-sm',
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

// Create custom price marker icon
function createPriceMarkerIcon(price, isActive = false, isFavorite = false) {
  const formattedPrice = `$ ${formatPrice(price)}`;

  return getLeaflet().divIcon({
    className: 'custom-price-marker',
    html: `
      <div class="${cn(
        'px-2 py-1 rounded-full font-semibold text-xs whitespace-nowrap shadow-lg transition-all duration-200 cursor-pointer border-1',
        isActive
          ? 'bg-primary text-primary-foreground border-primary scale-110 z-50'
          : isFavorite
            ? 'bg-rose-500 text-white border-rose-500 hover:scale-105'
            : 'bg-white text-zinc-900 border-zinc-200 hover:border-primary hover:scale-105',
      )}">
        ${formattedPrice}
      </div>
    `,
    iconSize: [60, 32],
    iconAnchor: [40, 32],
  });
}

// Custom Popup Content Component
function PropertyPopup({ property, onFavoriteClick }) {
  const map = useMap();

  const handleClose = () => {
    map.closePopup();
  };

  return (
    <div className="w-60 -m-3.5">
      {/* Image Carousel */}
      <div className="relative h-40 overflow-hidden rounded-t-lg -mx-2 w-[calc(100%+1.3rem)]">
        <ImageCarousel
          images={property.images}
          alt={property.name}
          isFavorite={property.isFavorite}
          onFavoriteClick={() => onFavoriteClick(property.id)}
          onClose={handleClose}
        />
      </div>

      {/* Content */}
      <div className="p-3 space-y-2.5">
        {/* Price */}
        <div className="flex items-baseline justify-between">
          <span className="text-xl font-semibold leading-none text-foreground">
            ${formatPrice(property.price)}
          </span>
        </div>

        {/* Property Type & Year */}
        <div className="flex items-center gap-2">
          <Badge variant="secondary" appearance="outline" size="sm">
            {property.propertyType}
          </Badge>
          {property.yearBuilt && (
            <span className="text-xs font-normal text-muted-foreground">
              Built {property.yearBuilt}
            </span>
          )}
        </div>

        {/* Address */}
        <div className="flex items-start gap-1.5">
          <MapPin className="size-3.5 text-gray-400" />
          <div className="flex flex-col gap-0.5">
            <span className="text-xs font-semibold leading-none text-foreground">
              {property.address}
            </span>
            <span className="text-xs font-normal text-muted-foreground">
              {property.city}, {property.state} {property.zipCode}
            </span>
          </div>
        </div>

        {/* Action Button */}
        <Button
          className="w-full"
          size="sm"
          variant="mono"
          onClick={(e) => e.stopPropagation()}
        >
          <Phone className="size-3.5" />
          Contact Agent
        </Button>
      </div>
    </div>
  );
}

// Map bounds adjuster component
function MapBoundsAdjuster({ properties }) {
  const map = useMap();

  React.useEffect(() => {
    if (properties.length > 0) {
      const bounds = getLeaflet().latLngBounds(
        properties.map((p) => p.coordinates),
      );
      map.fitBounds(bounds, { padding: [50, 50] });
    }
  }, [map, properties]);

  return null;
}

// Map size invalidator - fixes map not rendering fully when container size changes
function MapSizeInvalidator({ isExpanded }) {
  const map = useMap();

  React.useEffect(() => {
    // Small delay to ensure the container has finished resizing
    const timeoutId = setTimeout(() => {
      map.invalidateSize();
    }, 100);

    return () => clearTimeout(timeoutId);
  }, [map, isExpanded]);

  return null;
}

export function Map({ isExpanded = false, onToggleExpand }) {
  const [activeProperty, setActiveProperty] = React.useState(null);
  const [favorites, setFavorites] = React.useState(
    new Set(properties.filter((p) => p.isFavorite).map((p) => p.id)),
  );

  // NYC center coordinates
  const center = [40.7128, -73.956];

  const handleFavoriteClick = (id) => {
    setFavorites((prev) => {
      const newFavorites = new Set(prev);
      if (newFavorites.has(id)) {
        newFavorites.delete(id);
      } else {
        newFavorites.add(id);
      }
      return newFavorites;
    });
  };

  return (
    <div className="h-full w-full relative">
      {/* Toggle Expand Button */}
      <Button
        variant="outline"
        shape="circle"
        mode="icon"
        className="absolute left-4 top-4 z-1"
        onClick={onToggleExpand}
      >
        {isExpanded ? <ArrowRight /> : <ArrowLeft />}
      </Button>

      <MapContainer
        center={center}
        zoom={12}
        className="h-full w-full z-0"
        zoomControl={false}
        attributionControl={false}
      >
        {/* Map Tiles - Using CartoDB Positron for clean look */}
        <TileLayer url="https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png" />

        {/* Invalidate map size when expanded/collapsed */}
        <MapSizeInvalidator isExpanded={isExpanded} />

        {/* Adjust bounds to fit all properties */}
        <MapBoundsAdjuster properties={properties} />

        {/* Property Markers */}
        {properties.map((property) => (
          <Marker
            key={property.id}
            position={property.coordinates}
            icon={createPriceMarkerIcon(
              property.price,
              activeProperty === property.id,
              favorites.has(property.id),
            )}
            eventHandlers={{
              click: () => setActiveProperty(property.id),
              popupclose: () => setActiveProperty(null),
            }}
          >
            <Popup
              className="property-popup"
              closeButton={false}
              autoPan={true}
              offset={[0, -10]}
            >
              <PropertyPopup
                property={{
                  ...property,
                  isFavorite: favorites.has(property.id),
                }}
                onFavoriteClick={handleFavoriteClick}
              />
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
