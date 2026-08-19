// Pickr types are handled via any to avoid import issues with UMD module
export interface KTColorPickerConfigInterface {
	// KT-specific options
	lazy?: boolean;
	inputMode?: boolean;

	// Pickr options (partial - expose commonly used ones)
	theme?: 'classic' | 'monolith' | 'nano';
	default?: string;
	swatches?: string[];
	components?: {
		preview?: boolean;
		opacity?: boolean;
		hue?: boolean;
		interaction?: {
			hex?: boolean;
			rgba?: boolean;
			hsla?: boolean;
			hsva?: boolean;
			cmyk?: boolean;
			input?: boolean;
			clear?: boolean;
			save?: boolean;
		};
	};
	comparison?: boolean;
	lockOpacity?: boolean;
	closeOnScroll?: boolean;
	closeWithKey?: string;
	position?: 'top' | 'bottom' | 'left' | 'right';
	adjustableNumbers?: boolean;
}

export interface KTColorPickerInterface {
	init(): void;
	dispose(): void;
	show(): void;
	hide(): void;
	update(): void;
	reset(): void;
	apply(): void;
	getPickr(): any | null;
	getColor(): string | null;
}

