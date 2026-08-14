import Sortable from 'sortablejs';
import KTComponent from '../component';
import KTData from '../../helpers/data';
import KTDom from '../../helpers/dom';
import {
	KTSortableConfigInterface,
	KTSortableInterface,
} from './types';

declare global {
	interface Window {
		KT_SORTABLE_INITIALIZED?: boolean;
	}
}

export class KTSortable extends KTComponent implements KTSortableInterface {
	protected override _name: string = 'ktSortable';
	protected override _defaultConfig: KTSortableConfigInterface = {
		animation: 150,
		dragClass: 'rounded-none!',
		lazy: false,
	};
	protected override _config: KTSortableConfigInterface = this._defaultConfig;
	protected _sortable: any = null;

	constructor(element: HTMLElement, config?: KTSortableConfigInterface) {
		super();

		if (KTData.has(element as HTMLElement, this._name)) return;

		this._init(element);
		this._buildConfig(config);

		const lazy = this._getOption('lazy') as boolean;
		if (!lazy) {
			this._createSortable();
		}
	}

	protected _createSortable(): void {
		if (!this._element) return;

		const handle = this._getOption('handle') as string | undefined;
		const group = this._getOption('group') as string | object | undefined;
		const animation = (this._getOption('animation') as number) ?? 150;
		const dragClass = (this._getOption('dragClass') as string) ?? 'rounded-none!';
		const ghostClass = this._getOption('ghostClass') as string | undefined;
		const chosenClass = this._getOption('chosenClass') as string | undefined;

		const options: any = {
			animation: Number(animation) || 150,
			dragClass: dragClass || 'rounded-none!',
			onStart: (evt: any) => {
				this._dispatchEvent('kt.sortable.start', {
					item: evt.item,
					from: evt.from,
					oldIndex: evt.oldIndex,
				});
			},
			onEnd: (evt: any) => {
				this._dispatchEvent('kt.sortable.end', {
					item: evt.item,
					from: evt.from,
					to: evt.to,
					oldIndex: evt.oldIndex,
					newIndex: evt.newIndex,
				});
			},
			onUpdate: (evt: any) => {
				this._dispatchEvent('kt.sortable.update', {
					item: evt.item,
					from: evt.from,
					to: evt.to,
					oldIndex: evt.oldIndex,
					newIndex: evt.newIndex,
				});
			},
		};

		if (handle) options.handle = handle;
		if (group !== undefined) options.group = group;
		if (ghostClass) options.ghostClass = ghostClass;
		if (chosenClass) options.chosenClass = chosenClass;

		this._sortable = new Sortable(this._element, options);

		if (this._element && !this._element.id) {
			this._element.id = `kt-sortable-${this._uid}`;
		}
		this._dispatchEvent('kt.sortable.init', { element: this._element });
	}

	public getSortable(): any {
		return this._sortable;
	}

	public override dispose(): void {
		if (this._sortable) {
			this._sortable.destroy();
			this._sortable = null;
		}
		super.dispose();
	}

	public static getInstance(
		element: HTMLElement | string
	): KTSortable | null {
		const targetElement = KTDom.getElement(element);
		if (!targetElement) return null;
		return KTData.get(targetElement, 'ktSortable') as KTSortable | null;
	}

	public static createInstances(): void {
		const elements = document.querySelectorAll<HTMLElement>(
			'[data-kt-sortable]:not([data-kt-sortable=false])'
		);
		elements.forEach((element) => {
			new KTSortable(element);
		});
	}

	public static init(): void {
		KTSortable.createInstances();
		if (typeof window !== 'undefined') {
			window.KT_SORTABLE_INITIALIZED = true;
		}
	}
}
