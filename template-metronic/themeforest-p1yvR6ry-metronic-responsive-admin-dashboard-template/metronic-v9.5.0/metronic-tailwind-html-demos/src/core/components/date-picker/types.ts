import { Options } from 'vanilla-calendar-pro';

/** Preset value: single date (YYYY-MM-DD) or range [from, to] */
export type DatePickerPresetValue = string | [string, string];

/** Keys for built-in presets; used with data-kt-date-picker-preset-{key} (kebab-case) to enable/disable */
export type DatePickerPresetKey =
	| 'today'
	| 'yesterday'
	| 'thisWeek'
	| 'last7Days'
	| 'last30Days'
	| 'thisMonth'
	| 'lastMonth'
	| 'thisYear'
	| 'lastYear';

export interface DatePickerPreset {
	/** Required for built-in presets; optional for custom preset arrays */
	key?: DatePickerPresetKey;
	label: string;
	getValue: () => DatePickerPresetValue;
}

export interface KTDatePickerConfigInterface extends Partial<Options> {
	// KT-specific options
	lazy?: boolean;
	actionButtons?: boolean;
	/** Override Reset label when actionButtons is true (otherwise derived from locale). */
	actionButtonResetLabel?: string;
	/** Override Apply label when actionButtons is true (otherwise derived from locale). */
	actionButtonApplyLabel?: string;
	/** When true, the input is editable and typing/pasting a date (e.g. YYYY-MM-DD or ISO string) updates the calendar on blur. */
	allowInput?: boolean;
	dateFormat?: string;
	/** Enable built-in presets (true = today + yesterday by default); each preset can be toggled via presetToday, presetYesterday, etc. */
	presets?: boolean | DatePickerPreset[];
	/** Per-preset enable/disable (e.g. presetToday, presetYesterday). Only used when presets === true. */
	presetToday?: boolean;
	presetYesterday?: boolean;
	presetThisWeek?: boolean;
	presetLast7Days?: boolean;
	presetLast30Days?: boolean;
	presetThisMonth?: boolean;
	presetLastMonth?: boolean;
	presetThisYear?: boolean;
	presetLastYear?: boolean;
}

export interface KTDatePickerInterface {
	init(): void;
	dispose(): void;
	show(): void;
	hide(): void;
	update(): void;
	reset(): void;
	apply(): void;
	getCalendar(): unknown;
	getSelectedDates(): string[];
}
