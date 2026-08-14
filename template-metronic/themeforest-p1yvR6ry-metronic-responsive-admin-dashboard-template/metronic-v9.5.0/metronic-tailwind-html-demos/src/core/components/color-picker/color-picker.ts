import Pickr from '@simonwep/pickr';
import KTComponent from '../component';
import KTDom from '../../helpers/dom';
import KTData from '../../helpers/data';
import {
	KTColorPickerConfigInterface,
	KTColorPickerInterface,
} from './types';

declare global {
	interface Window {
		KT_COLOR_PICKER_INITIALIZED: boolean;
	}
}

export class KTColorPicker
	extends KTComponent
	implements KTColorPickerInterface
{
	// Use 'ktColorPicker' to match data-kt-color-picker-* attributes
	protected override _name: string = 'ktColorPicker';
	protected override _defaultConfig: KTColorPickerConfigInterface = {
		lazy: false,
		theme: 'monolith',
	};
	protected override _config: KTColorPickerConfigInterface =
		this._defaultConfig;
	protected _pickr: any | null = null;
	protected _initialized: boolean = false;
	protected _pendingColor: string | null = null;

	/**
	 * Get color string in the currently selected format
	 */
	protected _getColorString(color: any): string {
		if (!color || !this._pickr) return '';

		try {
			const format = this._pickr.getColorRepresentation();
			switch (format) {
				case 'HEXA':
					return color.toHEXA().toString();
				case 'RGBA':
					const rgba = color.toRGBA();
					// Round RGB values to integers, keep alpha as is
					return `rgba(${Math.round(rgba[0])}, ${Math.round(rgba[1])}, ${Math.round(rgba[2])}, ${rgba[3]})`;
				case 'HSLA':
					const hsla = color.toHSLA();
					// Round H, S, L values to integers, keep alpha as is
					return `hsla(${Math.round(hsla[0])}, ${Math.round(hsla[1])}%, ${Math.round(hsla[2])}%, ${hsla[3]})`;
				case 'HSVA':
					return color.toHSVA().toString();
				case 'CMYK':
					return color.toCMYK().toString();
				default:
					return color.toHEXA().toString();
			}
		} catch (e) {
			// Fallback to HEX if format conversion fails
			return color.toHEXA().toString();
		}
	}

	constructor(element: HTMLElement, config?: KTColorPickerConfigInterface) {
		super();

		if (KTData.has(element as HTMLElement, this._name)) return;

		this._init(element);
		this._buildConfig(config);

		// Check if lazy initialization is enabled
		const lazy = this._getOption('lazy') as boolean;

		if (!lazy) {
			this.init();
		}
	}

	protected _buildPickrOptions(): any {
		const options: any = {};

		// Check if input mode is enabled
		const inputMode = this._getOption('inputMode') === true;

		// For input mode, use the input as a button (don't replace it)
		if (inputMode && this._element && this._element.tagName === 'INPUT') {
			options.useAsButton = true;
		}

		// Theme
		const theme = this._getOption('theme') as string || 'monolith';
		options.theme = theme;

		// Default color
		const defaultColor = this._getOption('default') as string;
		if (defaultColor) options.default = defaultColor;

		// Swatches - handle both array and comma-separated string
		const swatches = this._getOption('swatches');
		if (swatches) {
			if (Array.isArray(swatches)) {
				options.swatches = swatches;
			} else if (typeof swatches === 'string') {
				// Parse comma-separated string to array
				options.swatches = swatches.split(',').map(s => s.trim()).filter(s => s.length > 0);
			}
		} else {
			// Enable swatches by default
			options.swatches = [
				'#000000', '#FFFFFF', '#FF0000', '#00FF00', '#0000FF',
				'#FFFF00', '#FF00FF', '#00FFFF', '#808080', '#800000',
				'#008000', '#000080', '#808000', '#800080', '#008080'
			];
		}

		// Components
		const components = this._getOption('components') as any;

		// Default components - disable format buttons (hex, rgba, hsla) by default
		const defaultComponents = {
			preview: true,
			opacity: true,
			hue: true,
			interaction: {
				hex: false,
				rgba: false,
				hsla: false,
				input: true,
				clear: true,
				save: true,
			},
		};

		if (components) {
			// Merge user components with defaults
			options.components = {
				...defaultComponents,
				...components,
				interaction: {
					...defaultComponents.interaction,
					...(components.interaction || {}),
				},
			};
		} else {
			options.components = defaultComponents;
		}

		// Comparison
		const comparison = this._getOption('comparison') as boolean;
		if (comparison !== undefined) options.comparison = comparison;

		// Lock opacity
		const lockOpacity = this._getOption('lockOpacity') as boolean;
		if (lockOpacity !== undefined) options.lockOpacity = lockOpacity;

		// Close on scroll
		const closeOnScroll = this._getOption('closeOnScroll') as boolean;
		if (closeOnScroll !== undefined) options.closeOnScroll = closeOnScroll;

		// Close with key
		const closeWithKey = this._getOption('closeWithKey') as string;
		if (closeWithKey) options.closeWithKey = closeWithKey;

		// Position - default to 'bottom' if not specified
		const position = this._getOption('position') as string;
		options.position = position || 'bottom';

		// Adjustable numbers
		const adjustableNumbers = this._getOption('adjustableNumbers') as boolean;
		if (adjustableNumbers !== undefined) options.adjustableNumbers = adjustableNumbers;

		// Disable autoReposition - we'll handle it manually to support scrollable parent containers
		options.autoReposition = false;

		return options;
	}

	protected _setupPickrEvents(): void {
		if (!this._pickr) return;

		const self = this;
		const inputMode = this._getOption('inputMode') === true;

		// Init event - create color preview box for input mode
		this._pickr.on('init', () => {
			// Create color preview box for input mode
			if (inputMode && this._element && this._element.tagName === 'INPUT') {
				const inputElement = this._element as HTMLInputElement;
				const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;

				if (ktInputContainer && !ktInputContainer.querySelector('.kt-color-picker-preview')) {
					// Create color preview box
					const colorPreview = document.createElement('div');
					colorPreview.className = 'kt-color-picker-preview';
					colorPreview.setAttribute('data-kt-color-picker-preview', 'true');

					// Set initial color if available
					const currentColor = this._pickr!.getColor();
					if (currentColor) {
						colorPreview.style.backgroundColor = currentColor.toHEXA().toString();
					} else {
						colorPreview.style.backgroundColor = '#3F51B5'; // Default color
					}

					// Insert preview box at the beginning of kt-input container
					ktInputContainer.insertBefore(colorPreview, inputElement);

					// Add padding to input to make room for preview box (0.25rem left + 1.75rem box + 0.25rem gap = 1.5rem)
					inputElement.style.paddingLeft = '1.5rem';
				}
			}
		});

		// Change event
		this._pickr.on('change', (color: any) => {
			if (!color) return;

			// Get color in currently selected format
			const colorString = self._getColorString(color);
			// Store HEX for internal use (preview box, etc.)
			const hexColorString = color.toHEXA().toString();
			self._pendingColor = hexColorString;

			// Update input and color preview box
			if (inputMode) {
				const inputElement = self._element as HTMLInputElement;
				if (inputElement && inputElement.tagName === 'INPUT') {
					// Update input value (show in selected format)
					inputElement.value = colorString;

					// Update color preview box (always use HEX for background color)
					const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;
					if (ktInputContainer) {
						const colorPreview = ktInputContainer.querySelector('.kt-color-picker-preview') as HTMLElement;
						if (colorPreview) {
							colorPreview.style.backgroundColor = hexColorString;
						}
					}
				}
			}

			// Fire change event
			self._fireEvent('change', {
				color: colorString,
				colorObject: color,
				element: self._element,
			});
			self._dispatchEvent('kt.color-picker.change', {
				color: colorString,
				colorObject: color,
				element: self._element,
			});
		});

		// Save event - handle Pickr's built-in save button
		this._pickr.on('save', (color: any) => {
			if (!color) return;

			const colorString = self._getColorString(color);
			const hexColorString = color.toHEXA().toString();

			// Update input and color preview box
			if (inputMode) {
				const inputElement = self._element as HTMLInputElement;
				if (inputElement && inputElement.tagName === 'INPUT') {
					inputElement.value = colorString;
					inputElement.dispatchEvent(new Event('input', { bubbles: true }));
					inputElement.dispatchEvent(new Event('change', { bubbles: true }));

					// Update color preview box
					const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;
					if (ktInputContainer) {
						const colorPreview = ktInputContainer.querySelector('.kt-color-picker-preview') as HTMLElement;
						if (colorPreview) {
							colorPreview.style.backgroundColor = hexColorString;
						}
					}
				}
			}

			// Fire save event
			self._fireEvent('save', {
				color: colorString,
				colorObject: color,
				element: self._element,
			});
			self._dispatchEvent('kt.color-picker.save', {
				color: colorString,
				colorObject: color,
				element: self._element,
			});

			// Close the picker when Save is clicked
			if (self._pickr && self._pickr.isOpen()) {
				self._pickr.hide();
			}
		});

		// Clear event - handle Pickr's built-in clear button
		this._pickr.on('clear', () => {
			// Update input and color preview box
			if (inputMode) {
				const inputElement = self._element as HTMLInputElement;
				if (inputElement && inputElement.tagName === 'INPUT') {
					inputElement.value = '';
					inputElement.dispatchEvent(new Event('input', { bubbles: true }));
					inputElement.dispatchEvent(new Event('change', { bubbles: true }));

					// Reset color preview box
					const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;
					if (ktInputContainer) {
						const colorPreview = ktInputContainer.querySelector('.kt-color-picker-preview') as HTMLElement;
						if (colorPreview) {
							colorPreview.style.backgroundColor = '#3F51B5'; // Default color
						}
					}
				}
			}

			// Fire clear event
			self._fireEvent('clear', {
				element: self._element,
			});
			self._dispatchEvent('kt.color-picker.clear', {
				element: self._element,
			});

			// Close the picker when Clear is clicked
			if (self._pickr && self._pickr.isOpen()) {
				self._pickr.hide();
			}
		});

		// Show event - attach focus handler for input mode
		if (inputMode) {
			// Show picker when input is focused
			if (this._element && this._element.tagName === 'INPUT') {
				this._element.addEventListener('focus', () => {
					if (this._pickr && !this._pickr.isOpen()) {
						this._pickr.show();
					}
				});
			}
		}

		// Helper method to position the picker correctly
		const positionPicker = (pickerApp: HTMLElement, instanceId: string): void => {
			if (!pickerApp || !self._pickr) return;

			// Ensure the picker app has position: fixed for correct viewport-relative positioning
			const computedStyle = window.getComputedStyle(pickerApp);
			if (computedStyle.position !== 'fixed') {
				pickerApp.style.position = 'fixed';
			}

			// Get nanopop and button references
			const nanopop = (self._pickr as any)._nanopop;
			const root = (self._pickr as any)._root;
			const button = root?.button;
			const options = (self._pickr as any).options;

			// Try to use nanopop.update() first, but if it fails, manually calculate position
			if (nanopop && typeof nanopop.update === 'function' && button && pickerApp && options) {
				// Try nanopop.update() first
				const updateSuccess = nanopop.update({
					container: document.body.getBoundingClientRect(),
					position: options.position || 'bottom-middle'
				});

				// If nanopop.update() failed, manually calculate and set position
				if (!updateSuccess) {
					// Manually calculate position based on button and position option
					const position = (options.position || 'bottom-middle').split('-');
					const vertical = position[0]; // top or bottom
					const horizontal = position[1] || 'middle'; // start, middle, or end

					// Get fresh button rect
					const freshButtonRect = button.getBoundingClientRect();
					const appHeight = pickerApp.offsetHeight;
					const appWidth = pickerApp.offsetWidth;
					const buttonWidth = freshButtonRect.width;
					const padding = options.padding || 8;

					let top = 0;
					let left = 0;

					// Calculate vertical position
					if (vertical === 'top') {
						top = freshButtonRect.top - appHeight - padding;
					} else { // bottom
						top = freshButtonRect.bottom + padding;
					}

					// Calculate horizontal position
					if (horizontal === 'start') {
						left = freshButtonRect.left;
					} else if (horizontal === 'end') {
						left = freshButtonRect.right - appWidth;
					} else { // middle
						left = freshButtonRect.left + (buttonWidth / 2) - (appWidth / 2);
					}

					// Ensure picker stays within viewport
					const viewportWidth = window.innerWidth;
					const viewportHeight = window.innerHeight;

					// Adjust horizontal position if it goes outside viewport
					if (left < 0) {
						left = padding;
					} else if (left + appWidth > viewportWidth) {
						left = viewportWidth - appWidth - padding;
					}

					// Adjust vertical position if it goes outside viewport
					if (top < 0) {
						// If top positioning would go off-screen, switch to bottom
						top = freshButtonRect.bottom + padding;
					} else if (top + appHeight > viewportHeight) {
						// If bottom positioning would go off-screen, switch to top
						top = freshButtonRect.top - appHeight - padding;
					}

					// Apply the calculated position
					pickerApp.style.top = `${top}px`;
					pickerApp.style.left = `${left}px`;
				}
			} else {
				// Fallback to _rePositioningPicker if nanopop isn't available
				if (typeof (self._pickr as any)._rePositioningPicker === 'function') {
					(self._pickr as any)._rePositioningPicker();
				}
			}
		};

		// Prevent picker from closing when clicking inside it
		// Add click handler on picker root to stop propagation (doesn't affect dragging)
		this._pickr.on('show', () => {
			// Close other open Pickr instances to avoid positioning conflicts
			const allPickers = document.querySelectorAll('[data-kt-color-picker]:not([data-kt-color-picker=false])');
			allPickers.forEach((pickerEl) => {
				if (pickerEl !== self._element) {
					const otherPicker = KTColorPicker.getInstance(pickerEl as HTMLElement);
					if (otherPicker && otherPicker.getPickr() && otherPicker.getPickr().isOpen()) {
						otherPicker.getPickr().hide();
					}
				}
			});

			// Position the picker immediately on show to prevent visible jump
			// Use requestAnimationFrame to ensure DOM is ready but still synchronous enough to prevent jump
			requestAnimationFrame(() => {
				const root = self._pickr!.getRoot();
				const rootElement = ((root as any).app || root) as HTMLElement;
				if (rootElement) {
					positionPicker(rootElement, self._uid);
				}
			});

			setTimeout(() => {
				const root = self._pickr!.getRoot();
				const rootElement = ((root as any).app || root) as HTMLElement;
				if (!rootElement) {
					return;
				}

				// Force position recalculation by triggering a resize event
				// This ensures the picker positions correctly relative to its trigger element
				if (typeof window !== 'undefined') {
					window.dispatchEvent(new Event('resize'));
				}

				// Also try to manually recalculate position if Pickr has a reposition method
				if (self._pickr && typeof (self._pickr as any).reposition === 'function') {
					(self._pickr as any).reposition();
				}

				// Only stop click propagation, not mousedown (so dragging still works)
				// But allow Pickr's built-in buttons and interaction elements to work
				const stopClickPropagation = (e: MouseEvent) => {
					const target = e.target as HTMLElement;
					// Allow clicks on Pickr's built-in buttons and interaction elements to work normally
					if (target && (
						target.closest('.pcr-button') ||
						target.closest('.pcr-clear') ||
						target.closest('.pcr-save') ||
						target.closest('.pcr-cancel') ||
						target.closest('.pcr-interaction') ||
						target.closest('button') ||
						target.closest('input') ||
						target.classList.contains('pcr-button') ||
						target.classList.contains('pcr-clear') ||
						target.classList.contains('pcr-save') ||
						target.classList.contains('pcr-cancel') ||
						target.tagName === 'BUTTON' ||
						target.tagName === 'INPUT'
					)) {
						// Let Pickr's built-in buttons and interaction elements work normally
						return;
					}
					// Stop propagation for other clicks inside the picker
					// This prevents Pickr's document click handler from closing it
					e.stopPropagation();
				};

				// Add click listener (not mousedown, so dragging works)
				rootElement.addEventListener('click', stopClickPropagation, true);

				// Store handler for cleanup
				(rootElement as any)._ktColorPickerClickHandler = stopClickPropagation;

				// Store reference to this picker's app element to verify it belongs to this instance
				const pickerRoot = self._pickr!.getRoot();
				const pickerApp = ((pickerRoot as any).app || pickerRoot) as HTMLElement;
				const pickerInstanceId = self._uid;

				// Mark this picker app with instance ID for verification
				if (pickerApp) {
					(pickerApp as any)._ktColorPickerInstanceId = pickerInstanceId;
				}

				// Add scroll handler to reposition picker using Pickr's internal method
				// Use requestAnimationFrame to throttle updates
				let repositionFrameId: number | null = null;
				const scrollHandler = () => {
					if (repositionFrameId !== null) {
						return; // Already scheduled
					}

					repositionFrameId = requestAnimationFrame(() => {
						repositionFrameId = null;

						// Only reposition if this picker is open and belongs to this instance
						if (self._pickr && self._pickr.isOpen()) {
							// Verify this is the correct picker instance
							const currentRoot = self._pickr.getRoot();
							const currentApp = ((currentRoot as any).app || currentRoot) as HTMLElement;
							const currentInstanceId = (currentApp as any)?._ktColorPickerInstanceId;
							const instanceIdMatches = currentApp && currentInstanceId === pickerInstanceId;

							// Only reposition if this picker app belongs to this instance
							if (instanceIdMatches && currentApp) {
								// Use the shared positioning function
								positionPicker(currentApp, pickerInstanceId);
							}
						}
					});
				};

				// Listen to scroll events on window and document
				window.addEventListener('scroll', scrollHandler, true);
				document.addEventListener('scroll', scrollHandler, true);

				// Also listen to scrollable parent containers
				const scrollableParents: HTMLElement[] = [];
				let parent: HTMLElement | null = self._element.parentElement;
				while (parent && parent !== document.body) {
					const style = window.getComputedStyle(parent);
					const overflow = style.overflow;
					const overflowY = style.overflowY;
					if (overflow === 'auto' || overflow === 'scroll' || overflowY === 'auto' || overflowY === 'scroll') {
						parent.addEventListener('scroll', scrollHandler, true);
						scrollableParents.push(parent);
					}
					parent = parent.parentElement;
				}

				// Also listen to resize events
				window.addEventListener('resize', scrollHandler);

				// Store for cleanup - use getter to access closure variable
				(self._element as any)._ktColorPickerScrollHandler = scrollHandler;
				(self._element as any)._ktColorPickerScrollableParents = scrollableParents;
				(self._element as any)._ktColorPickerGetFrameId = () => repositionFrameId;
			}, 10);
		});

		// Clean up click handler and scroll listener on hide
		this._pickr.on('hide', () => {

			const root = self._pickr!.getRoot();
			const rootElement = ((root as any).app || root) as HTMLElement;
			if (rootElement && (rootElement as any)._ktColorPickerClickHandler) {
				rootElement.removeEventListener('click', (rootElement as any)._ktColorPickerClickHandler, true);
				delete (rootElement as any)._ktColorPickerClickHandler;
			}

			// Remove instance ID from picker app
			if (rootElement && (rootElement as any)._ktColorPickerInstanceId === self._uid) {
				delete (rootElement as any)._ktColorPickerInstanceId;
			}

			// Remove scroll handler
			const scrollHandler = (self._element as any)?._ktColorPickerScrollHandler;
			if (scrollHandler) {
				window.removeEventListener('scroll', scrollHandler, true);
				document.removeEventListener('scroll', scrollHandler, true);
				window.removeEventListener('resize', scrollHandler);

				// Remove from scrollable parent containers
				const scrollableParents = (self._element as any)?._ktColorPickerScrollableParents || [];
				scrollableParents.forEach((parent: HTMLElement) => {
					parent.removeEventListener('scroll', scrollHandler, true);
				});

				// Cancel any pending animation frame
				const getFrameId = (self._element as any)?._ktColorPickerGetFrameId;
				if (getFrameId && typeof getFrameId === 'function') {
					const frameId = getFrameId();
					if (frameId !== null && frameId !== undefined && typeof frameId === 'number') {
						cancelAnimationFrame(frameId);
					}
				}

				// Clean up stored references
				delete (self._element as any)._ktColorPickerScrollHandler;
				delete (self._element as any)._ktColorPickerScrollableParents;
				delete (self._element as any)._ktColorPickerGetFrameId;
			}
		});
	}


	protected _applySelection(): void {
		if (!this._pickr) return;
		// Use Pickr's built-in applyColor method
		this._pickr.applyColor();
	}

	protected _resetSelection(): void {
		if (!this._pickr) return;
		// Use Pickr's built-in setColor(null) to clear
		this._pickr.setColor(null);
		this._pendingColor = null;

		// Update input and preview if in input mode
		const inputMode = this._getOption('inputMode') === true;
		if (inputMode) {
			const inputElement = this._element as HTMLInputElement;
			if (inputElement && inputElement.tagName === 'INPUT') {
				inputElement.value = '';

				// Reset color preview box
				const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;
				if (ktInputContainer) {
					const colorPreview = ktInputContainer.querySelector('.kt-color-picker-preview') as HTMLElement;
					if (colorPreview) {
						colorPreview.style.backgroundColor = '#3F51B5'; // Default color
					}
				}
			}
		}

		this._fireEvent('reset', {
			element: this._element,
		});
		this._dispatchEvent('kt.color-picker.reset', {
			element: this._element,
		});
	}

	public init(): void {
		if (this._initialized || !this._element) return;

		// Only add ID if element doesn't have one
		if (!this._element.id) {
			this._element.id = `kt-color-picker-${this._uid}`;
		}

		const options = this._buildPickrOptions();

		// Resolve Pickr - handle webpack UMD bundle
		// @ts-ignore
		const PickrClass = Pickr || (typeof require !== 'undefined' ? require('@simonwep/pickr') : null);
		const PickrInstance = PickrClass?.default || PickrClass;

		if (!PickrInstance || !PickrInstance.create) {
			console.error('Pickr is not available. Pickr:', Pickr, 'PickrClass:', PickrClass);
			return;
		}

		// Pass element directly instead of selector to ensure proper instance isolation
		// This ensures each picker instance is correctly associated with its trigger element
		this._pickr = PickrInstance.create({
			el: this._element,
			...options,
		});

		// Add click handler to trigger element to prevent clicks from closing picker
		if (this._element) {
			const triggerClickHandler = (e: MouseEvent) => {
				// Stop propagation when picker is open to prevent document click handler from closing it
				if (this._pickr && this._pickr.isOpen()) {
					e.stopPropagation();
				}
			};
			this._element.addEventListener('click', triggerClickHandler, true);
			(this._element as any)._ktColorPickerTriggerClickHandler = triggerClickHandler;
		}

		// Setup event handlers
		this._setupPickrEvents();

		this._initialized = true;

		this._fireEvent('init', { element: this._element });
		this._dispatchEvent('kt.color-picker.init', { element: this._element });
	}

	public dispose(): void {
		if (this._element && (this._element as any)._ktColorPickerTriggerClickHandler) {
			this._element.removeEventListener('click', (this._element as any)._ktColorPickerTriggerClickHandler, true);
			delete (this._element as any)._ktColorPickerTriggerClickHandler;
		}
		if (this._pickr) {
			this._pickr.destroy();
			this._pickr = null;
		}
		this._initialized = false;
		this._pendingColor = null;
		super.dispose();
	}

	public show(): void {
		if (this._pickr && this._initialized) {
			this._pickr.show();
		}
	}

	public hide(): void {
		if (this._pickr && this._initialized) {
			this._pickr.hide();
		}
	}

	public update(): void {
		if (!this._element) return;
		this._buildConfig();
		if (this._initialized) {
			this.dispose();
			this.init();
		}
	}

	public reset(): void {
		this._resetSelection();
	}

	public apply(): void {
		this._applySelection();
	}

	public getPickr(): any | null {
		return this._pickr;
	}

	public getColor(): string | null {
		if (!this._pickr) return null;
		const color = this._pickr.getColor();
		return color ? color.toHEXA().toString() : null;
	}

	public static getInstance(
		element: HTMLElement | string
	): KTColorPicker | null {
		const targetElement = KTDom.getElement(element);
		if (!targetElement) return null;
		return KTData.get(targetElement, 'ktColorPicker') as KTColorPicker | null;
	}

	public static createInstances(): void {
		const elements = document.querySelectorAll(
			'[data-kt-color-picker]:not([data-kt-color-picker=false])'
		);
		elements.forEach((element) => {
			new KTColorPicker(element as HTMLElement);
		});
	}

	public static init(): void {
		KTColorPicker.createInstances();

		if (window.KT_COLOR_PICKER_INITIALIZED !== true) {
			window.KT_COLOR_PICKER_INITIALIZED = true;
		}
	}
}

