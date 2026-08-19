/**
 * Unit tests for KTDatePicker
 * Covers: init/dispose, scroll/resize repositioning, allowInput, action buttons, getInstance, apply/reset
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { KTDatePicker } from '../date-picker';

// Mock vanilla-calendar-pro so we can test KTDatePicker without the full calendar
let capturedOptions: Record<string, unknown> | null = null;
let mockCalendarInstance: {
	context: {
		mainElement: HTMLElement;
		inputElement: HTMLInputElement;
		selectedDates: string[];
		selectedTime: string;
		cleanupHandlers: Array<() => void>;
	};
	init: () => void;
	show: () => void;
	hide: () => void;
	set: (opts: { selectedDates?: string[] }) => void;
	update: (opts?: unknown) => void;
} | null = null;

vi.mock('vanilla-calendar-pro', () => ({
	Calendar: class MockCalendar {
		context: typeof mockCalendarInstance extends null ? never : typeof mockCalendarInstance.context;
		constructor(
			selector: string,
			options: Record<string, unknown>,
		) {
			capturedOptions = options;
			const mainElement = document.createElement('div');
			mainElement.getBoundingClientRect = () =>
				({ top: 100, left: 50, width: 300, height: 320, bottom: 420, right: 350 } as DOMRect);
			const inputElement = document.querySelector<HTMLInputElement>(selector) ?? document.createElement('input');
			this.context = {
				mainElement,
				inputElement,
				selectedDates: [],
				selectedTime: '',
				cleanupHandlers: [],
			};
			mockCalendarInstance = this as typeof mockCalendarInstance;
		}

		init(): void {
			// KTDatePicker's onInit uses setTimeout(..., 10); run it after a microtask so callbacks are registered
			setTimeout(() => {
				capturedOptions?.onInit?.(this as never);
			}, 0);
		}

		show(): void {
			capturedOptions?.onShow?.(this as never);
		}

		hide(): void {
			capturedOptions?.onHide?.(this as never);
		}

		set(opts: { selectedDates?: string[] }): void {
			if (opts.selectedDates) (this as typeof mockCalendarInstance).context.selectedDates = opts.selectedDates;
		}

		update(_opts?: unknown): void {}
	},
}));

function createInput(attrs: Record<string, string> = {}): { container: HTMLElement; input: HTMLInputElement } {
	const container = document.createElement('div');
	container.className = 'kt-input';
	const input = document.createElement('input');
	input.type = 'text';
	input.setAttribute('data-kt-date-picker', 'true');
	Object.entries(attrs).forEach(([k, v]) => input.setAttribute(k, v));
	container.appendChild(input);
	return { container, input };
}

describe('KTDatePicker', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		capturedOptions = null;
		mockCalendarInstance = null;
	});

	afterEach(() => {
		vi.runOnlyPendingTimers();
		vi.useRealTimers();
		document.body.innerHTML = '';
	});

	describe('initialization', () => {
		it('creates calendar with lazy: false by default and calls init', () => {
			const { container, input } = createInput();
			document.body.appendChild(container);

			new KTDatePicker(input);
			expect(capturedOptions).not.toBeNull();
			expect(mockCalendarInstance).not.toBeNull();
		});

		it('does not init when lazy: true', () => {
			const { container, input } = createInput();
			input.setAttribute('data-kt-date-picker-lazy', 'true');
			document.body.appendChild(container);

			new KTDatePicker(input);
			expect(capturedOptions).toBeNull();
			expect(mockCalendarInstance).toBeNull();
		});

		it('passes inputMode and positionToInput to calendar options when set via data attributes', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-position-to-input': 'right',
			});
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			expect(capturedOptions?.inputMode).toBe(true);
			expect(capturedOptions?.positionToInput).toBe('right');
		});
	});

	describe('scroll and resize repositioning', () => {
		it('registers scroll and resize listeners when calendar is shown with inputMode', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
			});
			document.body.appendChild(container);

			const addSpy = vi.spyOn(window, 'addEventListener');
			const removeSpy = vi.spyOn(window, 'removeEventListener');

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			picker.show();
			// onShow is called by mock when show() is invoked
			mockCalendarInstance?.show();

			expect(addSpy).toHaveBeenCalledWith('scroll', expect.any(Function), true);
			expect(addSpy).toHaveBeenCalledWith('resize', expect.any(Function));

			picker.hide();
			mockCalendarInstance?.hide();
			expect(removeSpy).toHaveBeenCalledWith('scroll', expect.any(Function), true);
			expect(removeSpy).toHaveBeenCalledWith('resize', expect.any(Function));

			addSpy.mockRestore();
			removeSpy.mockRestore();
		});
	});

	describe('allowInput', () => {
		it('removes readonly and syncs parsed date from input to calendar on blur when allowInput is true', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-allow-input': 'true',
			});
			input.readOnly = true;
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);

			expect(input.hasAttribute('readonly')).toBe(false);

			// Use slash format so the value is not split by the range hyphen (e.g. 2025-03-15 would be split incorrectly)
			input.value = '2025/03/15';
			input.dispatchEvent(new Event('blur', { bubbles: false }));

			expect(mockCalendarInstance?.context.selectedDates).toEqual(['2025-03-15']);
		});

		it('parses multiple dates separated by comma', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-allow-input': 'true',
			});
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);

			input.value = '2025/01/01, 2025/01/03';
			input.dispatchEvent(new Event('blur', { bubbles: false }));

			expect(mockCalendarInstance?.context.selectedDates).toEqual(['2025-01-01', '2025-01-03']);
		});

		it('parses ISO date (YYYY-MM-DD) as single date without splitting on hyphens', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-allow-input': 'true',
			});
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);

			input.value = '2025-03-15';
			input.dispatchEvent(new Event('blur', { bubbles: false }));

			expect(mockCalendarInstance?.context.selectedDates).toEqual(['2025-03-15']);
		});

		it('parses input using user-defined dateFormat from config (e.g. dd MMM yyyy)', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-allow-input': 'true',
				'data-kt-date-picker-format': 'dd MMM yyyy',
			});
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);

			input.value = '15 Mar 2025';
			input.dispatchEvent(new Event('blur', { bubbles: false }));

			expect(mockCalendarInstance?.context.selectedDates).toEqual(['2025-03-15']);
		});
	});

	describe('action buttons', () => {
		it('injects action buttons when actionButtons is true', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-action-buttons': 'true',
			});
			document.body.appendChild(container);

			new KTDatePicker(input);
			vi.advanceTimersByTime(20);

			// onInit runs with setTimeout(..., 10), so after 20ms the inject ran
			const mainEl = mockCalendarInstance?.context.mainElement;
			const actions = mainEl?.querySelector('.vc-actions');
			expect(actions).not.toBeNull();
			expect(actions?.querySelector('[data-kt-date-picker-action="apply"]')).not.toBeNull();
			expect(actions?.querySelector('[data-kt-date-picker-action="reset"]')).not.toBeNull();
		});
	});

	describe('dispose', () => {
		it('cleans up scroll/resize and allowInput listeners on dispose', () => {
			const removeSpy = vi.spyOn(window, 'removeEventListener');
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-allow-input': 'true',
			});
			document.body.appendChild(container);

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			picker.show();
			mockCalendarInstance?.show();
			picker.dispose();

			expect(removeSpy).toHaveBeenCalledWith('scroll', expect.any(Function), true);
			expect(removeSpy).toHaveBeenCalledWith('resize', expect.any(Function));
			removeSpy.mockRestore();
		});
	});

	describe('getInstance', () => {
		it('returns the same instance for the same element', () => {
			const { container, input } = createInput();
			document.body.appendChild(container);

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			const instance = KTDatePicker.getInstance(input);
			expect(instance).toBe(picker);
		});

		it('returns null for element without date picker', () => {
			const div = document.createElement('div');
			document.body.appendChild(div);
			expect(KTDatePicker.getInstance(div)).toBeNull();
		});
	});

	describe('apply and reset', () => {
		it('apply updates input and hides calendar', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
				'data-kt-date-picker-action-buttons': 'true',
			});
			document.body.appendChild(container);

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			const calendar = mockCalendarInstance;
			if (calendar) {
				calendar.context.selectedDates = ['2025-06-10'];
				calendar.context.inputElement = input;
			}
			const hideSpy = vi.spyOn(calendar!, 'hide');

			picker.apply();
			expect(input.value).toBe('2025-06-10');
			expect(hideSpy).toHaveBeenCalled();
		});

		it('reset clears input and updates calendar', () => {
			const { container, input } = createInput({
				'data-kt-date-picker-input-mode': 'true',
			});
			document.body.appendChild(container);
			input.value = '2025-01-01';

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			const calendar = mockCalendarInstance;
			if (calendar) {
				calendar.context.selectedDates = ['2025-01-01'];
				calendar.context.inputElement = input;
			}
			const updateSpy = vi.spyOn(calendar!, 'update');

			picker.reset();
			expect(input.value).toBe('');
			expect(updateSpy).toHaveBeenCalledWith({ dates: true });
		});
	});

	describe('getSelectedDates', () => {
		it('returns selected dates from calendar context', () => {
			const { container, input } = createInput();
			document.body.appendChild(container);

			const picker = new KTDatePicker(input);
			vi.advanceTimersByTime(20);
			if (mockCalendarInstance) mockCalendarInstance.context.selectedDates = ['2025-02-14'];
			expect(picker.getSelectedDates()).toEqual(['2025-02-14']);
		});
	});

	describe('_formatDate (MMM token - no corruption)', () => {
		function formatDate(picker: KTDatePicker, dateStr: string, format: string): string {
			return (picker as unknown as { _formatDate(s: string, f: string): string })._formatDate(dateStr, format);
		}

		it('formats May as "May" not "5ay" (DD/MMM/YYYY)', () => {
			const { container, input } = createInput();
			input.setAttribute('data-kt-date-picker-lazy', 'true');
			document.body.appendChild(container);
			const picker = new KTDatePicker(input);
			expect(formatDate(picker, '2025-05-20', 'DD/MMM/YYYY')).toBe('20/May/2025');
		});

		it('formats March as "Mar" not "3ar"', () => {
			const { container, input } = createInput();
			input.setAttribute('data-kt-date-picker-lazy', 'true');
			document.body.appendChild(container);
			const picker = new KTDatePicker(input);
			expect(formatDate(picker, '2025-03-15', 'DD/MMM/YYYY')).toBe('15/Mar/2025');
		});

		it('formats December without D corrupting "Dec"', () => {
			const { container, input } = createInput();
			input.setAttribute('data-kt-date-picker-lazy', 'true');
			document.body.appendChild(container);
			const picker = new KTDatePicker(input);
			expect(formatDate(picker, '2025-12-19', 'DD MMM YYYY')).toBe('19 Dec 2025');
		});

		it('formats dd MMM yyyy for May', () => {
			const { container, input } = createInput();
			input.setAttribute('data-kt-date-picker-lazy', 'true');
			document.body.appendChild(container);
			const picker = new KTDatePicker(input);
			expect(formatDate(picker, '2025-05-20', 'dd MMM yyyy')).toBe('20 May 2025');
		});
	});

	describe('static createInstances', () => {
		it('initializes all elements with data-kt-date-picker', () => {
			const { container: c1, input: i1 } = createInput();
			const { container: c2, input: i2 } = createInput();
			i2.setAttribute('data-kt-date-picker', 'true');
			document.body.appendChild(c1);
			document.body.appendChild(c2);

			KTDatePicker.createInstances();
			vi.advanceTimersByTime(20);

			expect(KTDatePicker.getInstance(i1)).not.toBeNull();
			expect(KTDatePicker.getInstance(i2)).not.toBeNull();
		});
	});
});
