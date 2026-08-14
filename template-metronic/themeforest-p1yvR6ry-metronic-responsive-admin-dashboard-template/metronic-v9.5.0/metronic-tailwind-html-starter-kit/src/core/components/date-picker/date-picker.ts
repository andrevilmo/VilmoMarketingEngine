import { Calendar, type Options } from 'vanilla-calendar-pro';
import KTComponent from '../component';
import KTDom from '../../helpers/dom';
import KTData from '../../helpers/data';
import {
	KTDatePickerConfigInterface,
	KTDatePickerInterface,
	type DatePickerPreset,
	type DatePickerPresetKey,
	type DatePickerPresetValue,
} from './types';

declare global {
	interface Window {
		KT_DATE_PICKER_INITIALIZED: boolean;
	}
}

/** Format a Date to YYYY-MM-DD */
function toYYYYMMDD(d: Date): string {
	const y = d.getFullYear();
	const m = String(d.getMonth() + 1).padStart(2, '0');
	const day = String(d.getDate()).padStart(2, '0');
	return `${y}-${m}-${day}`;
}

/** Month names for format-based parsing (must match _formatDate) */
const MONTH_NAMES_LONG = ['January', 'February', 'March', 'April', 'May', 'June',
	'July', 'August', 'September', 'October', 'November', 'December'];
const MONTH_NAMES_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
	'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/**
 * Parse a date string using the same tokens as _formatDate.
 * Returns YYYY-MM-DD or null if parsing fails.
 * Tokens: YYYY, YY, MMMM, MMM, MM, M, DD, D (dddd/ddd matched but ignored).
 */
function parseDateWithFormat(inputStr: string, format: string): string | null {
	const trimmed = inputStr.trim();
	if (!trimmed) return null;
	// Try native parse first for ISO and common formats
	const tryNative = new Date(trimmed);
	if (!Number.isNaN(tryNative.getTime())) {
		const y = tryNative.getFullYear();
		const m = tryNative.getMonth();
		const d = tryNative.getDate();
		if (y >= 1970 && y <= 9999 && m >= 0 && m <= 11 && d >= 1 && d <= 31) {
			return toYYYYMMDD(tryNative);
		}
	}
	// Token definitions: longest first so "dd" matches before "d", "MMM" before "M", etc.
	const tokenSpecs: { token: string; pattern: string; type: 'year' | 'month' | 'day' | null }[] = [
		{ token: 'YYYY', pattern: '(\\d{4})', type: 'year' },
		{ token: 'yyyy', pattern: '(\\d{4})', type: 'year' },
		{ token: 'MMMM', pattern: `(${MONTH_NAMES_LONG.join('|')})`, type: 'month' },
		{ token: 'dddd', pattern: '(\\w+)', type: null },
		{ token: 'MMM', pattern: `(${MONTH_NAMES_SHORT.join('|')})`, type: 'month' },
		{ token: 'YY', pattern: '(\\d{2})', type: 'year' },
		{ token: 'yy', pattern: '(\\d{2})', type: 'year' },
		{ token: 'ddd', pattern: '(\\w+)', type: null },
		{ token: 'MM', pattern: '(\\d{1,2})', type: 'month' },
		{ token: 'DD', pattern: '(\\d{1,2})', type: 'day' },
		{ token: 'dd', pattern: '(\\d{1,2})', type: 'day' },
		{ token: 'M', pattern: '(\\d{1,2})', type: 'month' },
		{ token: 'D', pattern: '(\\d{1,2})', type: 'day' },
		{ token: 'd', pattern: '(\\d{1,2})', type: 'day' },
	];
	const order: { type: 'year' | 'month' | 'day'; pattern: string }[] = [];
	let regexStr = '';
	let pos = 0;
	while (pos < format.length) {
		let matched = false;
		for (const { token, pattern, type } of tokenSpecs) {
			if (format.substring(pos, pos + token.length) === token) {
				regexStr += pattern;
				if (type) order.push({ type, pattern });
				pos += token.length;
				matched = true;
				break;
			}
		}
		if (!matched) {
			regexStr += format[pos].replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
			pos += 1;
		}
	}
	const regex = new RegExp(`^\\s*${regexStr}\\s*$`, 'i');
	const match = trimmed.match(regex);
	if (!match) return null;
	let year: number | null = null;
	let month: number | null = null;
	let day: number | null = null;
	order.forEach(({ type }, i) => {
		const val = match[i + 1];
		if (type === 'year') {
			year = parseInt(val, 10);
			if (val.length === 2) year = year >= 0 && year <= 99 ? 2000 + year : year;
		} else if (type === 'month') {
			const lower = val.toLowerCase();
			const longIdx = MONTH_NAMES_LONG.findIndex((m) => m.toLowerCase() === lower);
			const shortIdx = MONTH_NAMES_SHORT.findIndex((m) => m.toLowerCase() === lower);
			month = longIdx >= 0 ? longIdx + 1 : shortIdx >= 0 ? shortIdx + 1 : parseInt(val, 10);
		} else if (type === 'day') {
			day = parseInt(val, 10);
		}
	});
	if (year == null || month == null || day == null) return null;
	if (month < 1 || month > 12 || day < 1 || day > 31) return null;
	const date = new Date(year, month - 1, day);
	if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null;
	return toYYYYMMDD(date);
}

/** Preset keys in display order */
const PRESET_KEYS_ORDER: DatePickerPresetKey[] = [
	'today',
	'yesterday',
	'thisWeek',
	'last7Days',
	'last30Days',
	'thisMonth',
	'lastMonth',
	'thisYear',
	'lastYear',
];

/** Presets enabled by default when presets=true (today, yesterday) */
const DEFAULT_ENABLED_PRESET_KEYS: DatePickerPresetKey[] = ['today', 'yesterday'];

/** Return built-in preset definitions (today, yesterday, this week, etc.) */
function getDefaultPresets(): DatePickerPreset[] {
	const now = new Date();
	const today = toYYYYMMDD(now);
	const yesterday = toYYYYMMDD(new Date(now.getTime() - 86400000));
	const dayOfWeek = now.getDay();
	const startOfWeek = new Date(now);
	startOfWeek.setDate(now.getDate() - dayOfWeek);
	const endOfWeek = new Date(startOfWeek);
	endOfWeek.setDate(startOfWeek.getDate() + 6);
	const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
	const endOfMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0);
	const startOfYear = new Date(now.getFullYear(), 0, 1);
	const endOfYear = new Date(now.getFullYear(), 11, 31);
	const lastMonthStart = new Date(now.getFullYear(), now.getMonth() - 1, 1);
	const lastMonthEnd = new Date(now.getFullYear(), now.getMonth(), 0);
	const lastYearStart = new Date(now.getFullYear() - 1, 0, 1);
	const lastYearEnd = new Date(now.getFullYear() - 1, 11, 31);
	const last7End = new Date(now);
	const last7Start = new Date(now.getTime() - 6 * 86400000);
	const last30End = new Date(now);
	const last30Start = new Date(now.getTime() - 29 * 86400000);

	return [
		{ key: 'today', label: 'Today', getValue: () => today },
		{ key: 'yesterday', label: 'Yesterday', getValue: () => yesterday },
		{ key: 'thisWeek', label: 'This week', getValue: () => [toYYYYMMDD(startOfWeek), toYYYYMMDD(endOfWeek)] },
		{ key: 'last7Days', label: 'Last 7 days', getValue: () => [toYYYYMMDD(last7Start), toYYYYMMDD(last7End)] },
		{ key: 'last30Days', label: 'Last 30 days', getValue: () => [toYYYYMMDD(last30Start), toYYYYMMDD(last30End)] },
		{ key: 'thisMonth', label: 'This month', getValue: () => [toYYYYMMDD(startOfMonth), today] },
		{ key: 'lastMonth', label: 'Last month', getValue: () => [toYYYYMMDD(lastMonthStart), toYYYYMMDD(lastMonthEnd)] },
		{ key: 'thisYear', label: 'This year', getValue: () => [toYYYYMMDD(startOfYear), today] },
		{ key: 'lastYear', label: 'Last year', getValue: () => [toYYYYMMDD(lastYearStart), toYYYYMMDD(lastYearEnd)] },
	];
}

/**
 * Default Reset/Apply labels for KT-injected footer buttons (Vanilla Calendar has none).
 * Indexed by primary language subtag only — `de-AT`, `de-CH`, etc. all resolve via `de`.
 * Unknown languages fall back to English; use actionButtonResetLabel / actionButtonApplyLabel to override.
 */
const DEFAULT_ACTION_BUTTON_I18N: Record<string, readonly [string, string]> = {
	en: ['Reset', 'Apply'],
	de: ['Zurücksetzen', 'Übernehmen'],
	fr: ['Réinitialiser', 'Appliquer'],
	es: ['Restablecer', 'Aplicar'],
};

function defaultActionButtonLabelsForLocaleTag(localeTag: string): { reset: string; apply: string } {
	const lang = localeTag.trim().split('-')[0]?.toLowerCase() || 'en';
	const pair = DEFAULT_ACTION_BUTTON_I18N[lang] ?? DEFAULT_ACTION_BUTTON_I18N.en;
	return { reset: pair[0], apply: pair[1] };
}

export class KTDatePicker
	extends KTComponent
	implements KTDatePickerInterface
{
	// Timeout delay constants for action button re-injection
	private static readonly REINJECTION_DELAY_MS = 10;
	private static readonly TITLE_CLICK_DELAY_MS = 50;

	// Use 'ktDatePicker' to match data-kt-date-picker-* attributes
	protected override _name: string = 'ktDatePicker';
	protected override _defaultConfig: KTDatePickerConfigInterface = {
		lazy: false,
		allowInput: true,
	};
	protected override _config: KTDatePickerConfigInterface =
		this._defaultConfig;
	protected _calendar: Calendar | null = null;
	protected _initialized: boolean = false;
	protected _pendingDates: string[] = [];
	protected _actionButtonsObserver: MutationObserver | null = null;
	protected _injectionTimeouts: Set<ReturnType<typeof setTimeout>> = new Set();
	/** Cleanup for scroll/resize listeners so the dropdown stays aligned when the page scrolls or resizes */
	protected _scrollResizeCleanup: (() => void) | null = null;
	/** Base top captured when dropdown opens; used to prevent cumulative top drift on repeated reposition calls */
	protected _repositionBaseTop: number | null = null;
	/** Cleanup for allowInput blur listener */
	protected _allowInputCleanup: (() => void) | null = null;

	constructor(element: HTMLElement, config?: KTDatePickerConfigInterface) {
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

	/**
	 * BCP 47 tag for footer labels: data-kt-date-picker-locale (runtime), calendar.locale, then config.
	 */
	protected _resolveActionButtonLocaleTag(calendar?: Calendar | null): string {
		const el = this._element;
		if (el) {
			const attr = el.getAttribute('data-kt-date-picker-locale');
			if (attr !== null && attr.trim() !== '') return attr.trim();
		}
		if (calendar) {
			const loc = (calendar as Calendar).locale;
			if (typeof loc === 'string' && loc.trim() !== '') return loc.trim();
		}
		const fromConfig = this._getOption('locale');
		if (typeof fromConfig === 'string' && fromConfig.trim() !== '') return fromConfig.trim();
		return 'en-US';
	}

	/**
	 * Labels for injected Reset / Apply footer; optional per-button overrides from config / data attrs.
	 */
	protected _getActionButtonLabels(calendar?: Calendar | null): { reset: string; apply: string } {
		const base = defaultActionButtonLabelsForLocaleTag(this._resolveActionButtonLocaleTag(calendar));
		const resetOverride = this._getOption('actionButtonResetLabel') as string | undefined;
		const applyOverride = this._getOption('actionButtonApplyLabel') as string | undefined;
		return {
			reset: typeof resetOverride === 'string' && resetOverride.length > 0 ? resetOverride : base.reset,
			apply: typeof applyOverride === 'string' && applyOverride.length > 0 ? applyOverride : base.apply,
		};
	}

	/**
	 * Inject action buttons into the calendar if they don't already exist
	 * @param calendar The calendar instance
	 * @returns true if buttons were injected or already exist, false otherwise
	 */
	protected _injectActionButtons(calendar: Calendar): boolean {
		if (!calendar?.context?.mainElement) return false;

		const { reset, apply } = this._getActionButtonLabels(calendar);
		const main = calendar.context.mainElement;

		const existingActions = main.querySelector('.vc-actions');
		if (existingActions) {
			const resetBtn = existingActions.querySelector(
				'[data-kt-date-picker-action="reset"]'
			);
			const applyBtn = existingActions.querySelector(
				'[data-kt-date-picker-action="apply"]'
			);
			if (resetBtn) resetBtn.textContent = reset;
			if (applyBtn) applyBtn.textContent = apply;
			return true;
		}

		const wrap = document.createElement('div');
		wrap.className = 'vc-actions';

		const resetBtn = document.createElement('button');
		resetBtn.type = 'button';
		resetBtn.className = 'kt-btn kt-btn-sm kt-btn-outline';
		resetBtn.setAttribute('data-kt-date-picker-action', 'reset');
		resetBtn.textContent = reset;

		const applyBtn = document.createElement('button');
		applyBtn.type = 'button';
		applyBtn.className = 'kt-btn kt-btn-sm kt-btn-primary';
		applyBtn.setAttribute('data-kt-date-picker-action', 'apply');
		applyBtn.textContent = apply;

		wrap.appendChild(resetBtn);
		wrap.appendChild(applyBtn);
		main.appendChild(wrap);
		return true;
	}

	/**
	 * Get config key for a preset key (e.g. today -> presetToday)
	 */
	protected _getPresetConfigKey(key: DatePickerPresetKey): string {
		return 'preset' + key.charAt(0).toUpperCase() + key.slice(1);
	}

	/**
	 * Get the list of presets (filtered by enabled config when presets=true)
	 */
	protected _getPresetsList(): DatePickerPreset[] {
		const presetsOpt = this._getOption('presets');
		if (!presetsOpt) return [];
		if (Array.isArray(presetsOpt)) return presetsOpt;

		// presets === true: use default enabled (today, yesterday) and apply per-preset overrides
		const enabled = new Set<DatePickerPresetKey>(DEFAULT_ENABLED_PRESET_KEYS);
		for (const key of PRESET_KEYS_ORDER) {
			const configKey = this._getPresetConfigKey(key);
			const value = this._getOption(configKey);
			if (value !== undefined && value !== null) {
				if (value) {
					enabled.add(key);
				} else {
					enabled.delete(key);
				}
			}
		}
		return getDefaultPresets().filter((p) => p.key !== undefined && enabled.has(p.key as DatePickerPresetKey));
	}

	/**
	 * Inject presets panel into the calendar if presets are enabled
	 */
	protected _injectPresetsPanel(calendar: Calendar): boolean {
		if (!calendar?.context?.mainElement) return false;

		const existing = calendar.context.mainElement.querySelector('.kt-date-picker-presets');
		if (existing) return true;

		const list = this._getPresetsList();
		if (list.length === 0) return false;

		const wrap = document.createElement('div');
		wrap.className = 'kt-date-picker-presets';
		list.forEach((preset, index) => {
			const btn = document.createElement('button');
			btn.type = 'button';
			btn.className = 'kt-date-picker-preset-btn kt-btn kt-btn-sm kt-btn-ghost';
			btn.setAttribute('data-kt-date-picker-preset', String(index));
			btn.textContent = preset.label;
			wrap.appendChild(btn);
		});
		// Insert after .vc-actions when present, otherwise append at end
		const vcActions = calendar.context.mainElement.querySelector('.vc-actions');
		if (vcActions && vcActions.nextSibling) {
			calendar.context.mainElement.insertBefore(wrap, vcActions.nextSibling);
		} else {
			calendar.context.mainElement.appendChild(wrap);
		}
		return true;
	}

	/**
	 * Apply a preset by index: set calendar selection and update input
	 */
	protected _applyPreset(calendar: Calendar, presetIndex: number): void {
		const list = this._getPresetsList();
		const preset = list[presetIndex];
		if (!preset) return;

		const value = preset.getValue();
		const selectedDates: string[] = Array.isArray(value) ? value : [value];
		const selectionMode = this._getOption('selectionDatesMode');
		const isRange = selectionMode === 'multiple-ranged';

		this._calendar?.set({ selectedDates });

		const inputElement = calendar.context.inputElement;
		if (inputElement) {
			const dateFormat = this._getOption('dateFormat') as string | undefined;
			const selectionTimeMode = this._getOption('selectionTimeMode');
			const timeMode = this._getTimeMode(selectionTimeMode);
			const selectedTime = calendar.context.selectedTime;

			let formatted: string;
			if (dateFormat) {
				const formattedDates = selectedDates.map(d =>
					this._formatDate(d, dateFormat, selectedTime, timeMode)
				);
				formatted = isRange && formattedDates.length >= 2
					? formattedDates[0] + ' - ' + formattedDates[formattedDates.length - 1]
					: formattedDates.join(', ');
			} else {
				formatted = isRange && selectedDates.length >= 2
					? selectedDates[0] + ' - ' + selectedDates[selectedDates.length - 1]
					: selectedDates.join(', ');
			}
			inputElement.value = formatted;
		}

		this._fireEvent('change', { dates: selectedDates, element: inputElement ?? undefined });
		this._dispatchEvent('kt.date-picker.change', {
			dates: selectedDates,
			element: inputElement ?? undefined,
		});

		const actionButtons = this._getOption('actionButtons') === true;
		if (!actionButtons && calendar.context.inputElement) {
			calendar.hide();
		}
	}

	/**
	 * Parse input value and sync to calendar selection (for allowInput).
	 * Uses dateFormat from config when set so user-defined formats (e.g. dd MMM yyyy) are parsed correctly.
	 * Splits by comma for multiple dates; each segment can be "from - to" for range.
	 */
	protected _syncInputToCalendar(): void {
		const inputEl = this._calendar?.context?.inputElement;
		if (!inputEl) return;
		const raw = inputEl.value.trim();
		if (!raw) return;
		const dateFormat = this._getOption('dateFormat') as string | undefined;
		const segments = raw.split(/\s*,\s*/).map((s) => s.trim()).filter(Boolean);
		const dates: string[] = [];
		for (const segment of segments) {
			const rangeParts = segment.split(/\s+-\s+/);
			for (const part of rangeParts) {
				const partTrimmed = part.trim();
				let yyyymmdd: string | null = null;
				if (dateFormat) {
					yyyymmdd = parseDateWithFormat(partTrimmed, dateFormat);
				}
				if (yyyymmdd === null) {
					const d = new Date(partTrimmed);
					if (!Number.isNaN(d.getTime())) yyyymmdd = toYYYYMMDD(d);
				}
				if (yyyymmdd) dates.push(yyyymmdd);
			}
		}
		if (dates.length > 0) {
			this._calendar?.set({ selectedDates: dates });
			this._calendar?.update();
			this._fireEvent('change', { dates, element: inputEl });
			this._dispatchEvent('kt.date-picker.change', { dates, element: inputEl });
		}
	}

	protected _buildCalendarOptions(): Options {
		const options: Options = {};
		const self = this;

		// Calendar type options
		const type = this._getOption('type');
		if (type) options.type = type as Options['type'];

		const displayMonthsCount = this._getOption('displayMonthsCount');
		if (displayMonthsCount) options.displayMonthsCount = displayMonthsCount as Options['displayMonthsCount'];

		const monthsToSwitch = this._getOption('monthsToSwitch');
		if (monthsToSwitch) options.monthsToSwitch = monthsToSwitch as Options['monthsToSwitch'];

		// Selection mode
		const selectionDatesMode = this._getOption('selectionDatesMode');
		if (selectionDatesMode !== undefined) options.selectionDatesMode = selectionDatesMode as Options['selectionDatesMode'];

		// Check if action buttons are enabled
		const actionButtons = this._getOption('actionButtons') === true;
		const isRangeMode = selectionDatesMode === 'multiple-ranged';

		// Input mode options
		const inputMode = this._getOption('inputMode') === true;
		const dateFormat = this._getOption('dateFormat') as string | undefined;

		if (inputMode) {
			options.inputMode = true;
			options.positionToInput = (this._getOption('positionToInput') as Options['positionToInput']) || 'left';

			// For action buttons mode, don't auto-close - let buttons handle it
			if (actionButtons) {
				options.onChangeToInput = function(calendar) {
					// Store selected dates but don't close or update input
					self._pendingDates = [...calendar.context.selectedDates];
					self._fireEvent('select', {
						dates: calendar.context.selectedDates,
						element: calendar.context.inputElement,
					});
				};
			} else if (isRangeMode) {
				// For range mode without action buttons - close when range is complete (2 dates)
				options.onChangeToInput = function(calendar) {
					if (!calendar.context.inputElement) return;
					const dates = calendar.context.selectedDates;
					const selectionTimeMode = self._getOption('selectionTimeMode');
					const selectedTime = calendar.context.selectedTime;
					const timeMode = self._getTimeMode(selectionTimeMode);

					if (dates.length >= 2) {
						// Range complete - update input
						let formattedDates: string[];
						if (dateFormat) {
							formattedDates = dates.map(d => self._formatDate(d, dateFormat, selectedTime, timeMode));
						} else if (selectedTime && timeMode) {
							const timeStr = self._formatTime(selectedTime, timeMode);
							formattedDates = dates.map(d => d + ' ' + timeStr);
						} else {
							formattedDates = dates;
						}
						calendar.context.inputElement.value = formattedDates.join(' - ');

						// Fire change events
						self._fireEvent('change', {
							dates: dates,
							element: calendar.context.inputElement,
						});
						self._dispatchEvent('kt.date-picker.change', {
							dates: dates,
							element: calendar.context.inputElement,
						});

						// Only auto-close if time mode is NOT enabled
						// When time mode is enabled, keep calendar open so user can select time
						if (!timeMode) {
							calendar.hide();
						}
					} else if (dates.length === 1) {
						// First date selected - update input but keep open
						let formattedDate: string;
						if (dateFormat) {
							formattedDate = self._formatDate(dates[0], dateFormat, selectedTime, timeMode);
						} else if (selectedTime && timeMode) {
							const timeStr = self._formatTime(selectedTime, timeMode);
							formattedDate = dates[0] + ' ' + timeStr;
						} else {
							formattedDate = dates[0];
						}
						calendar.context.inputElement.value = formattedDate + ' - ...';
					} else {
						calendar.context.inputElement.value = '';
					}
				};
			} else {
				// Standard single date mode
				options.onChangeToInput = function(calendar) {
					if (!calendar.context.inputElement) return;
					if (calendar.context.selectedDates[0]) {
						const selectionTimeMode = self._getOption('selectionTimeMode');
						const selectedTime = calendar.context.selectedTime;
						const timeMode = self._getTimeMode(selectionTimeMode);

						if (dateFormat) {
							const formattedDates = calendar.context.selectedDates.map(d => {
								return self._formatDate(d, dateFormat, selectedTime, timeMode);
							});
							calendar.context.inputElement.value = formattedDates.join(', ');
						} else if (selectedTime && timeMode) {
							// If time mode is enabled but no custom format, append time to date
							const timeStr = self._formatTime(selectedTime, timeMode);
							calendar.context.inputElement.value = calendar.context.selectedDates.join(', ') + ' ' + timeStr;
						} else {
							calendar.context.inputElement.value = calendar.context.selectedDates.join(', ');
						}

						// Only auto-close if time mode is NOT enabled
						// When time mode is enabled, keep calendar open so user can select time
						if (!timeMode) {
							calendar.hide();
						}
					} else {
						calendar.context.inputElement.value = '';
					}
					self._fireEvent('change', {
						dates: calendar.context.selectedDates,
						element: calendar.context.inputElement,
					});
					self._dispatchEvent('kt.date-picker.change', {
						dates: calendar.context.selectedDates,
						element: calendar.context.inputElement,
					});
				};
			}
		}

		// Add action buttons and time change listeners via onInit callback
		const existingOnInit = options.onInit;
		options.onInit = (calendar) => {
			// Call existing onInit if it exists
			if (existingOnInit) {
				existingOnInit(calendar);
			}

			// Defer injection so vanilla-calendar has finished building DOM (fixes first-load visibility)
			const presetsEnabled = self._getOption('presets');
			const injectPanels = () => {
				if (actionButtons) self._injectActionButtons(calendar);
				if (presetsEnabled) self._injectPresetsPanel(calendar);
			};
			setTimeout(injectPanels, KTDatePicker.REINJECTION_DELAY_MS);
			// Also run on next frame in case calendar re-renders after timeout
			requestAnimationFrame(() => {
				setTimeout(injectPanels, 0);
			});

			// Set up MutationObserver to watch for action buttons / presets removal when either is enabled
			if ((actionButtons || presetsEnabled) && calendar.context.mainElement) {
					// Clean up existing observer if any
					if (self._actionButtonsObserver) {
						self._actionButtonsObserver.disconnect();
					}

					self._actionButtonsObserver = new MutationObserver((mutations) => {
						// Add null check to prevent errors if calendar context becomes invalid
						if (!calendar?.context?.mainElement) return;

						for (const mutation of mutations) {
							// Check if action buttons were removed
							if (mutation.type === 'childList' && mutation.removedNodes.length > 0) {
								const hasActionsRemoved = Array.from(mutation.removedNodes).some((node) => {
									if (node.nodeType === Node.ELEMENT_NODE) {
										const element = node as HTMLElement;
										return element.classList.contains('vc-actions') ||
										       element.querySelector('.vc-actions') !== null;
									}
									return false;
								});
								const hasPresetsRemoved = Array.from(mutation.removedNodes).some((node) => {
									if (node.nodeType === Node.ELEMENT_NODE) {
										const element = node as HTMLElement;
										return element.classList.contains('kt-date-picker-presets') ||
										       element.querySelector('.kt-date-picker-presets') !== null;
									}
									return false;
								});

								// Check if action buttons / presets still exist in the main element
								const actionsExist = calendar.context.mainElement.querySelector('.vc-actions');
								const presetsExist = calendar.context.mainElement.querySelector('.kt-date-picker-presets');
								const needReinject = (hasActionsRemoved && !actionsExist) || (hasPresetsRemoved && !presetsExist && self._getOption('presets'));

								if (needReinject) {
									// Re-inject buttons and/or presets after a short delay to allow calendar to finish re-rendering
									const timeoutId = setTimeout(() => {
										self._injectionTimeouts.delete(timeoutId);
										// Verify calendar is still valid before injecting
										if (calendar?.context?.mainElement) {
											self._injectActionButtons(calendar);
											if (self._getOption('presets')) {
												self._injectPresetsPanel(calendar);
											}
										}
									}, KTDatePicker.REINJECTION_DELAY_MS);
									self._injectionTimeouts.add(timeoutId);
								}
							}
						}
					});

					// Start observing for child removals
					self._actionButtonsObserver.observe(calendar.context.mainElement, {
						childList: true,
						subtree: false
					});
				}

			// Setup click handlers for action buttons and presets
			const handleClick = (e: MouseEvent) => {
				const target = e.target as HTMLElement;
				if (target.closest('[data-kt-date-picker-action="apply"]')) {
					self._applySelection();
				} else if (target.closest('[data-kt-date-picker-action="reset"]')) {
					self._resetSelection();
				} else {
					const presetBtn = target.closest('[data-kt-date-picker-preset]') as HTMLElement | null;
					if (presetBtn) {
						const index = parseInt(presetBtn.getAttribute('data-kt-date-picker-preset') ?? '-1', 10);
						if (index >= 0) self._applyPreset(calendar, index);
					}
				}
			};

			// Function to update input with current time
			const updateInputWithTime = () => {
				if (!calendar.context.inputElement || !calendar.context.selectedDates[0]) return;

				const selectionTimeMode = self._getOption('selectionTimeMode');
				const selectedTime = calendar.context.selectedTime;
				const timeMode = self._getTimeMode(selectionTimeMode);
				const dateFormat = self._getOption('dateFormat') as string | undefined;

				if (dateFormat) {
					const formattedDates = calendar.context.selectedDates.map(d => {
						return self._formatDate(d, dateFormat, selectedTime, timeMode);
					});
					calendar.context.inputElement.value = formattedDates.join(', ');
				} else if (selectedTime && timeMode) {
					const timeStr = self._formatTime(selectedTime, timeMode);
					calendar.context.inputElement.value = calendar.context.selectedDates.join(', ') + ' ' + timeStr;
				} else {
					calendar.context.inputElement.value = calendar.context.selectedDates.join(', ');
				}
			};

			// Listen for time changes
			const handleTimeInteraction = (e: Event) => {
				const target = e.target as HTMLElement;
				if (target.closest('.vc-time') || target.closest('[data-vc-time]')) {
					setTimeout(() => {
						updateInputWithTime();
					}, 10);
				}
			};

			calendar.context.mainElement.addEventListener('click', handleClick);
			calendar.context.mainElement.addEventListener('click', handleTimeInteraction);
			calendar.context.mainElement.addEventListener('input', handleTimeInteraction);

			// Allow manual date entry (type or paste) when allowInput is true
			const allowInput = self._getOption('allowInput') === true;
			if (allowInput && inputMode && calendar.context.inputElement) {
				const inputEl = calendar.context.inputElement;
				inputEl.removeAttribute('readonly');
				const syncInputToCalendar = () => self._syncInputToCalendar();
				inputEl.addEventListener('blur', syncInputToCalendar);
				inputEl.addEventListener('change', syncInputToCalendar);
				self._allowInputCleanup = () => {
					inputEl.removeEventListener('blur', syncInputToCalendar);
					inputEl.removeEventListener('change', syncInputToCalendar);
					self._allowInputCleanup = null;
				};
				calendar.context.cleanupHandlers?.push(self._allowInputCleanup);
			}
		};

		// Reposition calendar and ensure action buttons / presets on show (fixes first-load visibility)
		const existingOnShow = options.onShow;
		const existingOnHide = options.onHide;
		const ensurePanelsOnShow = (calendar: Calendar) => {
			if (existingOnShow) existingOnShow(calendar);
			// When allowInput is on, sync input value to calendar when opening so the dropdown shows the typed date
			if (self._getOption('allowInput') === true && calendar.context.inputElement?.value.trim()) {
				self._syncInputToCalendar();
			}
			if (inputMode) {
				// Reset base top for each open cycle so repositioning remains idempotent while visible.
				self._repositionBaseTop = null;
				self._repositionToParentContainer(calendar);
				// Keep calendar aligned to input on scroll/resize (fixes dropdown drifting when part of it is off-screen)
				self._scrollResizeCleanup?.();
				const reposition = () => self._repositionToParentContainer(calendar);
				window.addEventListener('scroll', reposition, true);
				window.addEventListener('resize', reposition);
				self._scrollResizeCleanup = () => {
					window.removeEventListener('scroll', reposition, true);
					window.removeEventListener('resize', reposition);
					self._scrollResizeCleanup = null;
				};
			}
			// Re-inject so buttons are always present when dropdown opens
			if (actionButtons) self._injectActionButtons(calendar);
			if (self._getOption('presets')) self._injectPresetsPanel(calendar);
		};
		const cleanupOnHide = (calendar: Calendar) => {
			self._scrollResizeCleanup?.();
			self._repositionBaseTop = null;
			if (existingOnHide) existingOnHide(calendar);
		};
		if (inputMode || actionButtons || !!this._getOption('presets')) {
			options.onShow = ensurePanelsOnShow;
		}
		if (inputMode) {
			options.onHide = cleanupOnHide;
		}

		// Re-inject action buttons and/or presets when calendar view changes (e.g., clicking title to select year)
		const presetsEnabledForReinject = !!this._getOption('presets');
		if (actionButtons || presetsEnabledForReinject) {
			const existingOnClickTitle = options.onClickTitle;
			options.onClickTitle = (calendar, event) => {
				// Call existing onClickTitle if it exists
				if (existingOnClickTitle) {
					existingOnClickTitle(calendar, event);
				}

				// Re-inject action buttons and presets after view change (with delay to allow calendar to re-render)
				const timeoutId = setTimeout(() => {
					self._injectionTimeouts.delete(timeoutId);
					// Verify calendar is still valid before injecting
					if (calendar?.context?.mainElement) {
						if (actionButtons) self._injectActionButtons(calendar);
						if (self._getOption('presets')) self._injectPresetsPanel(calendar);
					}
				}, KTDatePicker.TITLE_CLICK_DELAY_MS);
				self._injectionTimeouts.add(timeoutId);
			};
		}

		// Keep Reset/Apply labels in sync with locale after calendar.update() / set({ locale })
		if (actionButtons) {
			const existingOnUpdate = options.onUpdate;
			options.onUpdate = (calendar) => {
				if (existingOnUpdate) existingOnUpdate(calendar);
				self._injectActionButtons(calendar);
			};
		}

		// Date constraints
		const dateMin = this._getOption('dateMin');
		if (dateMin) options.dateMin = dateMin as Options['dateMin'];

		const dateMax = this._getOption('dateMax');
		if (dateMax) options.dateMax = dateMax as Options['dateMax'];

		const displayDateMin = this._getOption('displayDateMin');
		if (displayDateMin) options.displayDateMin = displayDateMin as Options['displayDateMin'];

		const displayDateMax = this._getOption('displayDateMax');
		if (displayDateMax) options.displayDateMax = displayDateMax as Options['displayDateMax'];

		const displayDisabledDates = this._getOption('displayDisabledDates');
		if (displayDisabledDates !== undefined) options.displayDisabledDates = displayDisabledDates as boolean;

		const displayDatesOutside = this._getOption('displayDatesOutside');
		if (displayDatesOutside !== undefined) options.displayDatesOutside = displayDatesOutside as boolean;

		// Disable options
		const disableDates = this._getOption('disableDates');
		if (disableDates) options.disableDates = disableDates as Options['disableDates'];

		const disableAllDates = this._getOption('disableAllDates');
		if (disableAllDates !== undefined) options.disableAllDates = disableAllDates as boolean;

		const disableDatesPast = this._getOption('disableDatesPast');
		if (disableDatesPast !== undefined) options.disableDatesPast = disableDatesPast as boolean;

		const disableDatesGaps = this._getOption('disableDatesGaps');
		if (disableDatesGaps !== undefined) options.disableDatesGaps = disableDatesGaps as boolean;

		const disableWeekdays = this._getOption('disableWeekdays');
		if (disableWeekdays) options.disableWeekdays = disableWeekdays as Options['disableWeekdays'];

		const disableToday = this._getOption('disableToday');
		if (disableToday !== undefined) options.disableToday = disableToday as boolean;

		// Enable options
		const enableDates = this._getOption('enableDates');
		if (enableDates) options.enableDates = enableDates as Options['enableDates'];

		const enableWeekNumbers = this._getOption('enableWeekNumbers');
		if (enableWeekNumbers !== undefined) options.enableWeekNumbers = enableWeekNumbers as boolean;

		const enableDateToggle = this._getOption('enableDateToggle');
		if (enableDateToggle !== undefined) options.enableDateToggle = enableDateToggle as Options['enableDateToggle'];

		const enableMonthChangeOnDayClick = this._getOption('enableMonthChangeOnDayClick');
		if (enableMonthChangeOnDayClick !== undefined) options.enableMonthChangeOnDayClick = enableMonthChangeOnDayClick as boolean;

		const enableJumpToSelectedDate = this._getOption('enableJumpToSelectedDate');
		if (enableJumpToSelectedDate !== undefined) options.enableJumpToSelectedDate = enableJumpToSelectedDate as boolean;

		// Selection options (already handled above for selectionDatesMode)
		const selectionMonthsMode = this._getOption('selectionMonthsMode');
		if (selectionMonthsMode !== undefined) options.selectionMonthsMode = selectionMonthsMode as Options['selectionMonthsMode'];

		const selectionYearsMode = this._getOption('selectionYearsMode');
		if (selectionYearsMode !== undefined) options.selectionYearsMode = selectionYearsMode as Options['selectionYearsMode'];

		const selectionTimeMode = this._getOption('selectionTimeMode');
		if (selectionTimeMode !== undefined) options.selectionTimeMode = selectionTimeMode as Options['selectionTimeMode'];

		// Selected values
		const selectedDates = this._getOption('selectedDates');
		if (selectedDates) options.selectedDates = selectedDates as Options['selectedDates'];

		const selectedMonth = this._getOption('selectedMonth');
		if (selectedMonth !== undefined) options.selectedMonth = selectedMonth as Options['selectedMonth'];

		const selectedYear = this._getOption('selectedYear');
		if (selectedYear !== undefined) options.selectedYear = selectedYear as Options['selectedYear'];

		const selectedHolidays = this._getOption('selectedHolidays');
		if (selectedHolidays) options.selectedHolidays = selectedHolidays as Options['selectedHolidays'];

		const selectedWeekends = this._getOption('selectedWeekends');
		if (selectedWeekends) options.selectedWeekends = selectedWeekends as Options['selectedWeekends'];

		const selectedTime = this._getOption('selectedTime');
		if (selectedTime) options.selectedTime = selectedTime as Options['selectedTime'];

		const selectedTheme = this._getOption('selectedTheme');
		options.selectedTheme = (selectedTheme as Options['selectedTheme']) || 'light';

		// Time options
		const timeMinHour = this._getOption('timeMinHour');
		if (timeMinHour !== undefined) options.timeMinHour = timeMinHour as Options['timeMinHour'];

		const timeMaxHour = this._getOption('timeMaxHour');
		if (timeMaxHour !== undefined) options.timeMaxHour = timeMaxHour as Options['timeMaxHour'];

		const timeMinMinute = this._getOption('timeMinMinute');
		if (timeMinMinute !== undefined) options.timeMinMinute = timeMinMinute as Options['timeMinMinute'];

		const timeMaxMinute = this._getOption('timeMaxMinute');
		if (timeMaxMinute !== undefined) options.timeMaxMinute = timeMaxMinute as Options['timeMaxMinute'];

		const timeControls = this._getOption('timeControls');
		if (timeControls) options.timeControls = timeControls as Options['timeControls'];

		const timeStepHour = this._getOption('timeStepHour');
		if (timeStepHour !== undefined) options.timeStepHour = timeStepHour as Options['timeStepHour'];

		const timeStepMinute = this._getOption('timeStepMinute');
		if (timeStepMinute !== undefined) options.timeStepMinute = timeStepMinute as Options['timeStepMinute'];

		// Locale and weekday options
		const locale = this._getOption('locale');
		if (locale) options.locale = locale as Options['locale'];

		const firstWeekday = this._getOption('firstWeekday');
		if (firstWeekday !== undefined) options.firstWeekday = firstWeekday as Options['firstWeekday'];

		// Theme
		const themeAttrDetect = this._getOption('themeAttrDetect');
		if (themeAttrDetect) options.themeAttrDetect = themeAttrDetect as Options['themeAttrDetect'];

		return options;
	}

	/**
	 * Safely convert time mode option to string | number | undefined
	 */
	protected _getTimeMode(timeMode: any): string | number | undefined {
		if (timeMode === undefined || timeMode === null) return undefined;
		if (typeof timeMode === 'string' || typeof timeMode === 'number') return timeMode;
		return undefined;
	}

	/**
	 * Format time value from calendar context
	 * Handles both object format {hours, minutes} and string format "HH:mm" or "HH:mm AM/PM"
	 */
	protected _formatTime(time: any, timeMode?: string | number): string {
		if (!time) return '';

		let hours: number;
		let minutes: number;

		// Handle different time formats from vanilla-calendar-pro
		if (typeof time === 'string') {
			// Check if string already contains AM/PM (12-hour format from vanilla-calendar-pro)
			const hasPeriod = /AM|PM/i.test(time);
			if (hasPeriod) {
				// String is already in 12-hour format like "04:29 PM" or "12:00 AM"
				// Just return it as-is since it's already formatted correctly
				return time;
			}

			// Otherwise parse as 24-hour format "HH:mm"
			const parts = time.split(':');
			hours = parseInt(parts[0], 10);
			minutes = parseInt(parts[1] || '0', 10);
		} else if (typeof time === 'object' && time.hours !== undefined) {
			hours = time.hours;
			minutes = time.minutes || 0;
		} else {
			return '';
		}

		if (isNaN(hours) || isNaN(minutes)) return '';

		const timeModeNum = timeMode ? (typeof timeMode === 'string' ? parseInt(timeMode, 10) : timeMode) : 24;

		if (timeModeNum === 12) {
			// 12-hour format with AM/PM
			const period = hours >= 12 ? 'PM' : 'AM';
			const displayHours = hours === 0 ? 12 : hours > 12 ? hours - 12 : hours;
			return `${displayHours}:${String(minutes).padStart(2, '0')} ${period}`;
		} else {
			// 24-hour format
			return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
		}
	}

	/**
	 * Format a date string (YYYY-MM-DD) to a custom format
	 * Supported tokens: YYYY, YY, MM, M, DD, D, dddd, ddd, MMMM, MMM (and lowercase dd, d, yyyy, yy)
	 * Optionally includes time if provided.
	 * Tokens are replaced by scanning the format string position-by-position (longest match first)
	 * so that substituted month/day names (e.g. May, Dec) are not corrupted by M/D replacement.
	 */
	protected _formatDate(dateStr: string, format: string, time?: any, timeMode?: string | number): string {
		const date = new Date(dateStr);
		if (isNaN(date.getTime())) return dateStr;

		const year = date.getFullYear();
		const month = date.getMonth();
		const day = date.getDate();
		const weekday = date.getDay();

		const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
			'July', 'August', 'September', 'October', 'November', 'December'];
		const monthNamesShort = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
			'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
		const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
		const dayNamesShort = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

		const tokens: { [key: string]: string } = {
			'YYYY': String(year),
			'yyyy': String(year),
			'YY': String(year).slice(-2),
			'yy': String(year).slice(-2),
			'MMMM': monthNames[month],
			'MMM': monthNamesShort[month],
			'MM': String(month + 1).padStart(2, '0'),
			'M': String(month + 1),
			'dddd': dayNames[weekday],
			'ddd': dayNamesShort[weekday],
			'DD': String(day).padStart(2, '0'),
			'dd': String(day).padStart(2, '0'),
			'D': String(day),
			'd': String(day),
		};

		// Longest-first order so MMM matches before M, DD before D, etc.
		const tokenOrder = ['MMMM', 'MMM', 'MM', 'M', 'dddd', 'ddd', 'DD', 'dd', 'D', 'd', 'YYYY', 'yyyy', 'YY', 'yy'];
		let result = '';
		let pos = 0;
		while (pos < format.length) {
			let matched = false;
			for (const token of tokenOrder) {
				if (token.length <= format.length - pos && format.substring(pos, pos + token.length) === token) {
					result += tokens[token] ?? token;
					pos += token.length;
					matched = true;
					break;
				}
			}
			if (!matched) {
				result += format[pos];
				pos += 1;
			}
		}

		// Append time if provided
		if (time) {
			const formattedTime = this._formatTime(time, timeMode);
			if (formattedTime) {
				result = result + ' ' + formattedTime;
			}
		}

		return result;
	}

	protected _repositionToParentContainer(calendar: Calendar): void {
		// Check if input is inside a .kt-input container
		const inputElement = calendar.context.inputElement;
		if (!inputElement) return;

		const ktInputContainer = inputElement.closest('.kt-input') as HTMLElement;
		if (!ktInputContainer) return;

		const mainElement = calendar.context.mainElement;
		if (!mainElement) return;

		// Get positions
		const containerRect = ktInputContainer.getBoundingClientRect();
		const inputRect = inputElement.getBoundingClientRect();
		const calendarRect = mainElement.getBoundingClientRect();

		// Get the position setting
		const positionToInput = this._getOption('positionToInput') as string || 'left';

		// Calculate the vertical offset - position below the container instead of the input
		const topOffset = containerRect.bottom - inputRect.bottom;
		const currentTop = parseFloat(mainElement.style.top) || 0;
		if (this._repositionBaseTop === null) {
			this._repositionBaseTop = currentTop;
		}
		mainElement.style.top = `${this._repositionBaseTop + topOffset}px`;

		// Calculate horizontal position based on setting
		let newLeft: number;

		switch (positionToInput) {
			case 'left':
				// Align calendar's left edge with container's left edge
				newLeft = containerRect.left;
				break;
			case 'right':
				// Align calendar's right edge with container's right edge
				newLeft = containerRect.right - calendarRect.width;
				break;
			case 'center':
				// Center calendar relative to container
				newLeft = containerRect.left + (containerRect.width - calendarRect.width) / 2;
				break;
			case 'auto':
			default:
				// For auto, keep the original position but adjust for container
				const leftOffset = inputRect.left - containerRect.left;
				const currentLeft = parseFloat(mainElement.style.left) || 0;
				newLeft = currentLeft - leftOffset + containerRect.left;
				break;
		}

		mainElement.style.left = `${newLeft}px`;
	}

	protected _applySelection(): void {
		if (!this._calendar) return;

		const inputElement = this._calendar.context.inputElement;
		const dates = this._calendar.context.selectedDates;
		const selectedTime = this._calendar.context.selectedTime;
		const selectionMode = this._getOption('selectionDatesMode');
		const dateFormat = this._getOption('dateFormat') as string | undefined;
		const selectionTimeMode = this._getOption('selectionTimeMode');
		const timeMode = this._getTimeMode(selectionTimeMode);

		if (inputElement) {
			let formattedDates: string[];

			if (dateFormat) {
				formattedDates = dates.map(d => this._formatDate(d, dateFormat, selectedTime, timeMode));
			} else if (selectedTime && timeMode) {
				// If time mode is enabled but no custom format, append time to date
				const timeStr = this._formatTime(selectedTime, timeMode);
				formattedDates = dates.map(d => d + ' ' + timeStr);
			} else {
				formattedDates = dates;
			}

			if (selectionMode === 'multiple-ranged' && dates.length >= 2) {
				inputElement.value = formattedDates[0] + ' - ' + formattedDates[formattedDates.length - 1];
			} else {
				inputElement.value = formattedDates.join(', ');
			}
		}

		this._calendar.hide();

		this._fireEvent('apply', {
			dates: dates,
			element: inputElement,
		});
		this._dispatchEvent('kt.date-picker.apply', {
			dates: dates,
			element: inputElement,
		});
		this._fireEvent('change', {
			dates: dates,
			element: inputElement,
		});
		this._dispatchEvent('kt.date-picker.change', {
			dates: dates,
			element: inputElement,
		});
	}

	protected _resetSelection(): void {
		if (!this._calendar) return;

		const inputElement = this._calendar.context.inputElement;

		// Reset the calendar using update with reset options
		this._calendar.update({ dates: true });
		this._pendingDates = [];

		if (inputElement) {
			inputElement.value = '';
		}

		this._fireEvent('reset', {
			element: inputElement,
		});
		this._dispatchEvent('kt.date-picker.reset', {
			element: inputElement,
		});
	}

	public init(): void {
		if (this._initialized || !this._element) return;

		// Only add ID if element doesn't have one
		if (!this._element.id) {
			this._element.id = `kt-date-picker-${this._uid}`;
		}

		const selector = `#${this._element.id}`;
		const options = this._buildCalendarOptions();

		this._calendar = new Calendar(selector, options);
		this._calendar.init();

		this._initialized = true;

		this._fireEvent('init', { element: this._element });
		this._dispatchEvent('kt.date-picker.init', { element: this._element });
	}

	public dispose(): void {
		this._scrollResizeCleanup?.();
		this._scrollResizeCleanup = null;
		this._repositionBaseTop = null;
		this._allowInputCleanup?.();
		this._allowInputCleanup = null;
		// Clean up pending timeouts to prevent memory leaks
		this._injectionTimeouts.forEach(timeoutId => window.clearTimeout(timeoutId));
		this._injectionTimeouts.clear();

		// Clean up MutationObserver
		if (this._actionButtonsObserver) {
			this._actionButtonsObserver.disconnect();
			this._actionButtonsObserver = null;
		}

		if (this._calendar) {
			this._calendar = null;
		}
		this._initialized = false;
		this._pendingDates = [];
		super.dispose();
	}

	public show(): void {
		if (this._calendar && this._initialized) {
			this._calendar.show();
		}
	}

	public hide(): void {
		if (this._calendar && this._initialized) {
			this._calendar.hide();
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

	public getCalendar(): Calendar | null {
		return this._calendar;
	}

	public getSelectedDates(): string[] {
		return this._calendar?.context.selectedDates || [];
	}

	public static getInstance(
		element: HTMLElement | string
	): KTDatePicker | null {
		const targetElement = KTDom.getElement(element);
		if (!targetElement) return null;
		return KTData.get(targetElement, 'ktDatePicker') as KTDatePicker | null;
	}

	public static createInstances(): void {
		const elements = document.querySelectorAll(
			'[data-kt-date-picker]:not([data-kt-date-picker=false])'
		);
		elements.forEach((element) => {
			new KTDatePicker(element as HTMLElement);
		});
	}

	public static init(): void {
		KTDatePicker.createInstances();

		if (window.KT_DATE_PICKER_INITIALIZED !== true) {
			window.KT_DATE_PICKER_INITIALIZED = true;
		}
	}
}
