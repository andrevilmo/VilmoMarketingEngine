/**
 * KT-specific options that are not part of Dropzone.js
 */
export interface KTDropzoneSpecificOptions {
	lazy?: boolean;
	fileThumbnailIcon?: string;
	displayFileThumbnail?: boolean;
	fileThumbnailImage?: boolean;
	fileThumbnailPath?: string;
}

/**
 * Dropzone Configuration Interface
 *
 * Extends Record<string, any> to allow ANY Dropzone.js option to pass through
 * Users can pass ALL built-in Dropzone config options without restrictions
 */
export interface KTDropzoneConfigInterface
	extends Record<string, any>,
		KTDropzoneSpecificOptions {}

/**
 * Dropzone Interface
 *
 * Defines the public API for the KTDropzone component
 */
export interface KTDropzoneInterface {
	init(): void;
	dispose(): void;
	processQueue(): void;
	removeAllFiles(deleteFiles?: boolean): void;
	getDropzone(): any; // Dropzone instance
	getFiles(): any[]; // Array of file objects
	getQueuedFiles(): any[];
	getUploadingFiles(): any[];
}
