export interface KTSortableConfigInterface {
	animation?: number;
	handle?: string;
	group?: string | { name: string; pull?: boolean | 'clone'; put?: boolean };
	dragClass?: string;
	ghostClass?: string;
	chosenClass?: string;
	disabled?: boolean;
	lazy?: boolean;
}

export interface KTSortableInterface {
	dispose(): void;
	getSortable(): any;
}
