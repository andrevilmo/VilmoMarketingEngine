/**
 * Dropzone File Upload Component
 *
 * This module provides a comprehensive file upload solution using Dropzone.js library.
 *
 * Features:
 * - Drag and drop file uploads
 * - File type detection and icon display
 * - Progress tracking
 * - Form integration with hidden inputs
 * - Submit button state management
 * - File removal and cancellation
 * - Error handling with tooltips
 * - Parallel uploads support
 * - Custom preview templates
 *
 * Data Attributes:
 * - data-kt-dropzone="true" - Enable dropzone on element
 * - data-kt-dropzone-url - Upload endpoint URL (default: "/upload")
 * - data-kt-dropzone-max-files - Maximum number of files
 * - data-kt-dropzone-max-filesize - Maximum file size in MB (default: "2")
 * - data-kt-dropzone-accepted-files - Accepted file extensions (default: ".jpeg,.jpg,.png")
 * - data-kt-dropzone-auto-process - Auto-upload files (default: true)
 * - data-kt-dropzone-file-thumbnail-icon - Default icon class (default: "cloud-upload")
 * - data-kt-dropzone-display-file-thumbnail - Show file type icons (true/false)
 * - data-kt-dropzone-file-thumbnail-image - Alternative attribute for thumbnails (true/false)
 * - data-kt-dropzone-file-thumbnail-image-path - Path to file type icons (default: "assets/media/file-types/")
 * - data-kt-dropzone-parallel-uploads - Number of concurrent uploads (default: 1)
 */

declare global {
	interface Window {
		Dropzone: any;
		KT_DROPZONE_INITIALIZED: boolean;
	}
}

import KTComponent from '../component';
import KTDom from '../../helpers/dom';
import KTData from '../../helpers/data';
import {
	KTDropzoneConfigInterface,
	KTDropzoneInterface,
} from './types';

/**
 * File type to SVG icon mapping
 * Maps file extensions to SVG icon names for visual file type representation
 */
const FILE_TYPE_SVG_MAP: Record<string, string> = {
	// Documents
	doc: 'doc',
	docx: 'word',
	pdf: 'pdf',
	txt: 'txt',
	rtf: 'text',
	// Spreadsheets
	xls: 'xls',
	xlsx: 'excel',
	csv: 'excel',
	// Presentations
	ppt: 'ppt',
	pptx: 'powerpoint',
	// Images
	jpg: 'image',
	jpeg: 'image',
	png: 'image',
	gif: 'image',
	bmp: 'image',
	webp: 'image',
	svg: 'svg',
	ai: 'ai',
	psd: 'psd',
	fig: 'figma',
	// Archives
	zip: 'zip',
	rar: 'zip',
	'7z': 'zip',
	tar: 'zip',
	gz: 'zip',
	iso: 'iso',
	// Audio
	mp3: 'mp3',
	wav: 'music',
	ogg: 'music',
	flac: 'music',
	m4a: 'music',
	// Video
	mp4: 'video',
	avi: 'video-1',
	mkv: 'video-1',
	mov: 'video-1',
	wmv: 'video-1',
	flv: 'video-1',
	// Code
	js: 'js',
	javascript: 'javascript',
	css: 'css',
	php: 'php',
	sql: 'sql',
	html: 'css',
	htm: 'css',
	// Fonts
	ttf: 'ttf',
	otf: 'font',
	woff: 'font',
	woff2: 'font',
	// Vector
	eps: 'vector',
	// Other
	apk: 'apk',
	mail: 'mail',
	eml: 'mail-1',
	disc: 'disc',
	record: 'record',
};

/**
 * Extract file extension from filename
 */
function getFileExtension(filename: string): string {
	if (!filename) return '';
	const parts = filename.split('.');
	const ext = parts.length > 1 ? parts[parts.length - 1].toLowerCase() : '';
	return ext;
}

/**
 * Get SVG icon path for file type
 *
 * Automatically detects the base path from existing asset links on the page to ensure
 * correct path resolution in both local development and production environments.
 *
 * - Local: Detects base from Flask's url_for() generated paths (e.g., /static/assets/...)
 * - Production: Detects base from nginx-served paths (e.g., /metronic/tailwind/docs/assets/...)
 */
function getFileTypeSvg(filename: string, thumbnailPath?: string): string | null {
	if (!filename) return null;

	const ext = getFileExtension(filename);
	if (!ext) return null;

	const svgName = FILE_TYPE_SVG_MAP[ext];
	if (svgName) {
		let path = thumbnailPath || 'assets/media/file-types/';
		// Detect base path from existing asset links on the page
		// This ensures compatibility with both local (Flask url_for) and production (nginx) environments
		if (path && !path.startsWith('http://') && !path.startsWith('https://')) {
			// Try to detect base path from existing asset links (CSS/JS files)
			const baseLink = document.querySelector('link[href*="/assets/"], script[src*="/assets/"]') as HTMLElement;
			if (baseLink) {
				const href = (baseLink as HTMLLinkElement).href || (baseLink as HTMLScriptElement).src;
				if (href) {
					try {
						const url = new URL(href);
						const assetPath = url.pathname;
						// Extract base path before /assets/
						// Examples:
						// - Local: /static/assets/css/styles.css -> base: /static
						// - Production: /metronic/tailwind/docs/assets/css/styles.css -> base: /metronic/tailwind/docs
						const assetsIndex = assetPath.indexOf('/assets/');
						if (assetsIndex > 0) {
							const detectedBase = assetPath.substring(0, assetsIndex);
							// Make path absolute with detected base
							if (!path.startsWith('/')) {
								path = detectedBase + '/' + path;
							} else {
								path = detectedBase + path;
							}
						} else if (!path.startsWith('/')) {
							// No base path detected, make it absolute from root
							path = '/' + path;
						}
					} catch (e) {
						// URL parsing failed, fallback to simple absolute path
						if (!path.startsWith('/')) {
							path = '/' + path;
						}
					}
				} else if (!path.startsWith('/')) {
					path = '/' + path;
				}
			} else if (!path.startsWith('/')) {
				// No asset links found, make it absolute from root
				path = '/' + path;
			}
		}
		// Ensure path ends with /
		const normalizedPath = path && path[path.length - 1] !== '/' ? path + '/' : path;
		const finalPath = normalizedPath + svgName + '.svg';
		return finalPath;
	}

	// Fallback: if no mapping found, return null (will use default icon)
	return null;
}

/**
 * Generate Dropzone preview template HTML
 */
function getPreviewTemplate(
	iconName: string,
	displayFileThumbnail: boolean,
	fileThumbnailPath: string
): string {
	// Default to cloud-upload if no icon specified
	let icon = iconName || 'cloud-upload';
	// Remove 'ki-' prefix if present
	if (icon.indexOf('ki-') === 0) {
		icon = icon.substring(3);
	}

	// Default icon content - will be replaced dynamically for file types
	const iconContent = `<i class="ki-filled ki-${icon}"></i>`;

	return (
		'<div class="dropzone-file-item dz-preview dz-file-preview">' +
		'<div class="dropzone-file-item-icon">' +
		iconContent +
		'</div>' +
		'<div class="dropzone-file-item-content">' +
		'<div class="dropzone-file-item-header">' +
		'<div class="dropzone-file-item-info">' +
		'<div class="dropzone-file-item-name"><div class="dropzone-file-item-wrapper" data-dz-name></div></div>' +
		'<div class="dropzone-file-item-size" data-dz-size></div>' +
		'</div>' +
		'<div class="dropzone-file-item-actions">' +
		'<button type="button" class="kt-btn kt-btn-icon kt-btn-sm dropzone-file-item-remove" data-dz-remove aria-label="Remove file">' +
		'<i class="ki-filled ki-cross-circle"></i>' +
		'</button>' +
		'<div class="dropzone-file-item-complete">' +
		'<i class="ki-filled ki-check-circle"></i>' +
		'</div>' +
		'<div class="dropzone-file-item-error">' +
		'<i class="ki-filled ki-information-1"></i>' +
		'</div>' +
		'</div>' +
		'</div>' +
		'<div class="dropzone-file-item-progress">' +
		'<div class="progress">' +
		'<div class="progress-bar dz-progress-bar" role="progressbar" data-dz-uploadprogress style="width: 0%"></div>' +
		'</div>' +
		'</div>' +
		'</div>' +
		'</div>'
	);
}

export class KTDropzone
	extends KTComponent
	implements KTDropzoneInterface
{
	protected override _name: string = 'ktDropzone';
	protected override _defaultConfig: KTDropzoneConfigInterface = {
		// Essential Dropzone.js defaults
		url: '/upload',
		autoProcessQueue: true,
		maxFilesize: 2 * 1024 * 1024, // 2MB in bytes
		acceptedFiles: '.jpeg,.jpg,.png',
		parallelUploads: 1,
		addRemoveLinks: false,
		autoDiscover: false,
		createImageThumbnails: false,
		uploadMultiple: false,

		// KT-specific defaults
		lazy: false,
		fileThumbnailIcon: 'cloud-upload',
		displayFileThumbnail: true,
		fileThumbnailImage: true,
		fileThumbnailPath: 'assets/media/file-types/',

		// Dictionary defaults
		dictDefaultMessage: '',
		dictFallbackMessage: "Your browser does not support drag'n'drop file uploads.",
		dictFileTooBig: 'File is too big ({{filesize}}MB). Max filesize: {{maxFilesize}}MB.',
		dictInvalidFileType: "You can't upload files of this type.",
		dictResponseError: 'Server responded with {{statusCode}} code.',
		dictCancelUpload: '',
		dictUploadCanceled: 'Upload canceled.',
		dictRemoveFile: '',
		dictMaxFilesExceeded: 'You can not upload any more files.',
	};
	protected override _config: KTDropzoneConfigInterface = this._defaultConfig;
	protected _dropzone: any = null;
	protected _initialized: boolean = false;

	constructor(element: HTMLElement, config?: KTDropzoneConfigInterface) {
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
	 * Extract Dropzone options from merged config
	 *
	 * Copies all options from merged _config (which already has defaults → global → data attributes → constructor)
	 * Filters out KT-specific options and applies special handling for DOM elements and type conversions
	 */
	protected _getOptionsFromDataAttributes(): any {
		if (!this._element) return {};

		// Start with all options from merged config
		// _config already has: defaults → global → data attributes → constructor
		const options: any = { ...this._config };

		// Remove KT-specific options (not part of Dropzone.js)
		delete options.lazy;
		delete options.fileThumbnailIcon;
		delete options.displayFileThumbnail;
		delete options.fileThumbnailImage;
		delete options.fileThumbnailPath;

		// Special handling: DOM element selection
		const fileListContainer =
			this._element.parentElement?.querySelector('.file-upload-list .dropzone-previews') ||
			this._element.querySelector('.file-upload-list .dropzone-previews');
		const previewsContainer =
			fileListContainer ||
			this._element.querySelector('.dropzone-previews') ||
			this._element;
		options.previewsContainer = previewsContainer as HTMLElement;

		const clickableElement =
			this._element.querySelector('.dropzone-browse-btn') ||
			this._element.querySelector('.dropzone-clickable') ||
			this._element;
		options.clickable = clickableElement as HTMLElement;

		// Special handling: maxFilesize conversion (MB to bytes if needed)
		const maxFilesize = this._getOption('maxFilesize');
		if (maxFilesize !== undefined) {
			// If small number or string, assume MB and convert to bytes
			if (typeof maxFilesize === 'string' || (typeof maxFilesize === 'number' && maxFilesize < 1000)) {
				options.maxFilesize = parseFloat(String(maxFilesize)) * 1024 * 1024;
			} else {
				options.maxFilesize = maxFilesize;
			}
		}

		// Special handling: maxFiles type conversion
		const maxFiles = this._getOption('maxFiles');
		if (maxFiles !== undefined) {
			options.maxFiles = typeof maxFiles === 'number' ? maxFiles : parseInt(String(maxFiles), 10);
		}

		// Special handling: parallelUploads normalization
		const parallelUploadsAttr = this._getOption('parallelUploads');
		if (parallelUploadsAttr !== undefined) {
			const parallelUploads = parallelUploadsAttr ? parseInt(String(parallelUploadsAttr), 10) : 1;
			options.parallelUploads = parallelUploads < 1 || isNaN(parallelUploads) ? 1 : parallelUploads;
		}

		// Special handling: previewTemplate (uses fileThumbnailIcon from config)
		const fileThumbnailIcon = (this._getOption('fileThumbnailIcon') as string) || 'cloud-upload';
		const displayFileThumbnail = this._getOption('displayFileThumbnail');
		const fileThumbnailImage = this._getOption('fileThumbnailImage');
		const showFileThumbnails = displayFileThumbnail !== false && fileThumbnailImage !== false;
		const fileThumbnailPath = (this._getOption('fileThumbnailImagePath') as string) || 'assets/media/file-types/';
		options.previewTemplate = getPreviewTemplate(fileThumbnailIcon, showFileThumbnails, fileThumbnailPath);

		// Special handling: transformResponse (keep existing logic if not overridden)
		if (!options.transformResponse) {
			options.transformResponse = (text: string) => {
				try {
					return JSON.parse(text);
				} catch (e) {
					return text;
				}
			};
		}

		// Special handling: dictFileTooBig - use maxFilesize from config if available
		if (options.dictFileTooBig && options.dictFileTooBig.includes('{{maxFilesize}}')) {
			const maxFilesizeMB = options.maxFilesize ? (options.maxFilesize / (1024 * 1024)).toFixed(0) : '2';
			options.dictFileTooBig = options.dictFileTooBig.replace('{{maxFilesize}}', maxFilesizeMB);
		}

		// All other Dropzone.js options pass through automatically from merged config
		return options;
	}

	/**
	 * Initialize Dropzone on an element
	 */
	public init(): void {
		if (!this._element) return;

		// Check if Dropzone is available
		if (typeof window.Dropzone === 'undefined') {
			console.warn('KTDropzone: Dropzone.js library is not loaded. Please ensure dropzone.min.js is included.');
			return;
		}

		// Check if already initialized
		if (this._initialized) {
			return;
		}

		// Check if Dropzone.js has already attached to this element
		if ((this._element as any).dropzone) {
			this._dropzone = (this._element as any).dropzone;
			this._setupEventHandlers();
			this._initialized = true;
			return;
		}

		// Check if Dropzone.js has already attached to this element
		if ((this._element as any).dropzone) {
			this._dropzone = (this._element as any).dropzone;
			this._setupEventHandlers();
			this._initialized = true;
			return;
		}

		// Get options from merged config
		// _getOptionsFromDataAttributes() already uses merged _config which has:
		// defaults → global → data attributes → constructor config
		const options = this._getOptionsFromDataAttributes();

		// Store thumbnail settings for access in event handlers
		// Use already parsed config values
		// File type icons are enabled by default
		// Can be disabled by setting data-kt-dropzone-display-file-thumbnail="false" or data-kt-dropzone-file-thumbnail-image="false"
		const displayFileThumbnail = this._getOption('displayFileThumbnail');
		const fileThumbnailImage = this._getOption('fileThumbnailImage');
		// Default to true unless explicitly set to false
		// Enabled by default, disabled only if either attribute is explicitly set to false
		const showFileThumbnails = displayFileThumbnail !== false && fileThumbnailImage !== false;
		// Default path (relative - will be converted to absolute by getFileTypeSvg based on detected base path)
		let fileThumbnailPath = (this._getOption('fileThumbnailImagePath') as string) || 'assets/media/file-types/';

		// Initialize Dropzone
		try {
			this._dropzone = new window.Dropzone(this._element, options);
		} catch (error: any) {
			// If Dropzone is already attached, get the existing instance
			if (error.message && error.message.includes('already attached')) {
				this._dropzone = (this._element as any).dropzone;
				this._setupEventHandlers();
				this._initialized = true;
				return;
			}
			throw error;
		}

		// Store settings on dropzone instance for access in event handlers
		(this._dropzone as any).showFileThumbnails = showFileThumbnails;
		(this._dropzone as any).fileThumbnailPath = fileThumbnailPath;
		(this._dropzone as any).element = this._element;

		// Set up event handlers
		this._setupEventHandlers();

		this._initialized = true;
		this._fireEvent('init', { dropzone: this._dropzone });
	}

	/**
	 * Set up Dropzone event handlers
	 */
	protected _setupEventHandlers(): void {
		if (!this._dropzone || !this._element) return;

		const dropzone = this._dropzone;
		const element = this._element;
		const self = this;

		// File added
		dropzone.on('addedfile', (file: any) => {
			this._updateFileList();

			// Hide dropzone message when files are added
			const message = element.querySelector('.dropzone-message');
			if (message && dropzone.files.length > 0) {
				element.classList.add('dz-started');
			}

			// Update file icon based on file type if enabled
			if ((dropzone as any).showFileThumbnails && file.previewElement) {
				const iconElement = file.previewElement.querySelector('.dropzone-file-item-icon');
				if (iconElement) {
					const fileThumbnailPath = (dropzone as any).fileThumbnailPath || 'assets/media/file-types/';
					const svgPath = getFileTypeSvg(file.name, fileThumbnailPath);
					if (svgPath) {
						// Replace icon with SVG image
						const iconTag = iconElement.querySelector('i');
						if (iconTag) {
							iconTag.remove();
						}
						// Remove any existing image
						const existingImg = iconElement.querySelector('img.dropzone-file-type-icon');
						if (existingImg) {
							existingImg.remove();
						}
						const imgTag = document.createElement('img');
						imgTag.src = svgPath;
						imgTag.alt = file.name;
						imgTag.className = 'dropzone-file-type-icon';
						iconElement.appendChild(imgTag);
					}
				}
			}

			// Create hidden input for tracking file status
			this._createFileHiddenInput(file);

			// Disable submit button when file is added
			const form = element.closest('form');
			if (form) {
				this._updateSubmitButtonState(form);
			}

			this._fireEvent('addedfile', { file, dropzone });
		});

		// Upload started
		dropzone.on('sending', (file: any) => {
			if (file.previewElement) {
				file.previewElement.classList.add('dz-processing');
			}

			// Send file ID to server for matching
			if (file.uploadId) {
				file.xhr.setRequestHeader('X-File-Id', file.uploadId);
			}

			this._fireEvent('sending', { file, dropzone });
		});

		// Upload progress
		dropzone.on('uploadprogress', (file: any, progress: number) => {
			const previewElement = file.previewElement;
			if (previewElement) {
				previewElement.classList.add('dz-processing');
			}

			this._fireEvent('uploadprogress', { file, progress, dropzone });
		});

		// Upload successful
		dropzone.on('success', (file: any, response: any) => {
			if (file.previewElement) {
				file.previewElement.classList.remove('dz-processing');
				file.previewElement.classList.add('dz-complete');
			}

			// Parse response if it's a string
			let responseData = response;
			if (typeof response === 'string') {
				try {
					responseData = JSON.parse(response);
				} catch (e) {
					console.error('Failed to parse response:', e);
					responseData = {};
				}
			}

			// Update or create filename input with server response
			const form = element.closest('form');
			if (form && file.uploadId) {
				const hiddenInputsContainer = form.querySelector('.dropzone-hidden-inputs');
				if (hiddenInputsContainer && responseData) {
					const filename = responseData.filename || '';

					// Update or create filename input
					if (file.filenameInput) {
						file.filenameInput.value = filename;
					} else if (filename) {
						const filenameInput = document.createElement('input');
						filenameInput.type = 'hidden';
						filenameInput.name = `file_filename[${file.uploadId}]`;
						filenameInput.value = filename;
						filenameInput.setAttribute('data-file-id', file.uploadId);
						hiddenInputsContainer.appendChild(filenameInput);
						file.filenameInput = filenameInput;
					}
				}
			}

			this._updateFileList();

			// Update submit button state after successful upload
			if (form) {
				this._updateSubmitButtonState(form);
			}

			this._fireEvent('success', { file, response: responseData, dropzone });
		});

		// Upload error
		dropzone.on('error', (file: any, message: string | Error) => {
			if (file.previewElement) {
				file.previewElement.classList.remove('dz-processing');
				file.previewElement.classList.add('dz-error');
			}

			this._updateFileList();

			// Update submit button state
			const form = element.closest('form');
			if (form) {
				this._updateSubmitButtonState(form);
			}

			this._fireEvent('error', { file, message, dropzone });
		});

		// File removed
		dropzone.on('removedfile', (file: any) => {
			this._updateFileList();

			// Mark file as canceled in hidden input
			this._markFileAsCanceled(file);

			// Show dropzone message if no files left
			if (dropzone.files.length === 0) {
				element.classList.remove('dz-started');
			}

			// Update submit button state
			const form = element.closest('form');
			if (form) {
				this._updateSubmitButtonState(form);
			}

			this._fireEvent('removedfile', { file, dropzone });
		});

		// Upload canceled
		dropzone.on('canceled', (file: any) => {
			if (file.previewElement) {
				file.previewElement.classList.remove('dz-processing');
				file.previewElement.classList.remove('dz-complete');
			}

			// Mark file as canceled in hidden input
			this._markFileAsCanceled(file);
			this._updateFileList();

			// Update submit button state
			const form = element.closest('form');
			if (form) {
				this._updateSubmitButtonState(form);
			}

			this._fireEvent('canceled', { file, dropzone });
		});

		// Set up form handlers
		this._setupFormHandlers();
	}

	/**
	 * Set up form submission and reset handlers
	 */
	protected _setupFormHandlers(): void {
		if (!this._element) return;

		const form = this._element.closest('form');
		if (!form || form.hasAttribute('data-kt-dropzone-form-handled')) {
			return;
		}

		form.setAttribute('data-kt-dropzone-form-handled', '1');

		// Handle reset button
		const resetButton = form.querySelector('button[type="reset"]');
		if (resetButton) {
			resetButton.addEventListener('click', (e) => {
				e.preventDefault();

				// Clear all dropzones in the form
				const formDropzones = form.querySelectorAll('[data-kt-dropzone="true"]');
				formDropzones.forEach((dzElement) => {
					const instance = KTDropzone.getInstance(dzElement as HTMLElement);
					if (instance) {
						instance.removeAllFiles();
					}
				});

				// Hide file list
				const fileListContainer = form.querySelector('.file-upload-list');
				if (fileListContainer) {
					fileListContainer.classList.add('hidden');
				}

				// Clear hidden inputs
				const hiddenInputsContainer = form.querySelector('.dropzone-hidden-inputs');
				if (hiddenInputsContainer) {
					hiddenInputsContainer.innerHTML = '';
				}

				// Reset dropzone state
				const dropzoneElement = form.querySelector('.dropzone');
				if (dropzoneElement) {
					dropzoneElement.classList.remove('dz-started');
				}

				// Update button states
				this._updateSubmitButtonState(form);
			});
		}

		// Handle form submission
		form.addEventListener('submit', (e) => {
			e.preventDefault();

			// Get all dropzones in this form
			const formDropzones = form.querySelectorAll('[data-kt-dropzone="true"]');

			// Process queue for all dropzones if autoProcessQueue is false
			formDropzones.forEach((dzElement) => {
				const instance = KTDropzone.getInstance(dzElement as HTMLElement);
				if (instance && this._dropzone) {
					const dz = instance.getDropzone();
					if (dz && !dz.options.autoProcessQueue && dz.getQueuedFiles().length > 0) {
						dz.processQueue();
					}
				}
			});

			// Wait for all uploads to complete before submitting
			const allDropzones = Array.from(formDropzones)
				.map((dzElement) => {
					const instance = KTDropzone.getInstance(dzElement as HTMLElement);
					return instance ? instance.getDropzone() : null;
				})
				.filter((dz) => dz !== null);

			const pendingUploads = allDropzones.filter((dz: any) => {
				return dz.getUploadingFiles().length > 0 || dz.getQueuedFiles().length > 0;
			});

			if (pendingUploads.length > 0) {
				// Wait for uploads to complete
				const checkUploads = () => {
					const stillPending = allDropzones.filter((dz: any) => {
						return dz.getUploadingFiles().length > 0 || dz.getQueuedFiles().length > 0;
					});
					if (stillPending.length === 0) {
						// All uploads complete, now submit
						this._submitFormData(form);
					} else {
						// Check again in 100ms
						setTimeout(checkUploads, 100);
					}
				};
				checkUploads();
			} else {
				// All uploads already complete
				this._submitFormData(form);
			}
		});
	}

	/**
	 * Create hidden inputs for file tracking
	 */
	protected _createFileHiddenInput(file: any): void {
		if (!this._element) return;

		const form = this._element.closest('form');
		if (!form) {
			return;
		}

		// Generate unique ID for the file
		if (!file.uploadId) {
			file.uploadId = `file_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
		}

		// Create hidden input container if it doesn't exist
		let hiddenInputsContainer = form.querySelector('.dropzone-hidden-inputs');
		if (!hiddenInputsContainer) {
			hiddenInputsContainer = document.createElement('div');
			hiddenInputsContainer.className = 'dropzone-hidden-inputs';
			hiddenInputsContainer.style.display = 'none';
			form.appendChild(hiddenInputsContainer);
		}

		// Create hidden inputs for file tracking
		const fileIdInput = document.createElement('input');
		fileIdInput.type = 'hidden';
		fileIdInput.name = 'uploaded_files[]';
		fileIdInput.value = file.uploadId;
		fileIdInput.setAttribute('data-file-id', file.uploadId);
		fileIdInput.setAttribute('data-file-name', file.name);
		hiddenInputsContainer.appendChild(fileIdInput);

		// Create status input (active by default)
		const statusInput = document.createElement('input');
		statusInput.type = 'hidden';
		statusInput.name = `file_status[${file.uploadId}]`;
		statusInput.value = 'active';
		statusInput.setAttribute('data-file-id', file.uploadId);
		hiddenInputsContainer.appendChild(statusInput);

		// Store reference to status input in file object
		file.statusInput = statusInput;
		file.fileIdInput = fileIdInput;
	}

	/**
	 * Mark file as canceled in hidden input
	 */
	protected _markFileAsCanceled(file: any): void {
		if (file.statusInput) {
			file.statusInput.value = 'canceled';
		}
	}

	/**
	 * Update file list container visibility
	 */
	protected _updateFileList(): void {
		if (!this._dropzone || !this._element) return;

		const files = this._dropzone.files;
		const fileListContainer =
			this._element.parentElement?.querySelector('.file-upload-list') ||
			this._element.querySelector('.file-upload-list');
		const previewsContainer = fileListContainer?.querySelector('.dropzone-previews');

		if (!fileListContainer || !previewsContainer) {
			return;
		}

		// Show/hide file list container
		if (files.length > 0) {
			fileListContainer.classList.remove('hidden');
		} else {
			fileListContainer.classList.add('hidden');
		}
	}

	/**
	 * Update submit and reset button states
	 */
	protected _updateSubmitButtonState(form: HTMLFormElement): void {
		if (!form) return;

		const submitButton = form.querySelector('button[type="submit"]');
		const resetButton = form.querySelector('button[type="reset"]');

		// Get all dropzones in this form
		const formDropzones = form.querySelectorAll('[data-kt-dropzone="true"]');

		let hasSuccessfulUploads = false;
		let hasFiles = false;
		let isManualUploadMode = false;

		formDropzones.forEach((dzElement) => {
			const instance = KTDropzone.getInstance(dzElement as HTMLElement);
			if (instance) {
				const dz = instance.getDropzone();
				if (dz && dz.files.length > 0) {
					hasFiles = true;
				}
				// Check if manual upload mode (autoProcessQueue is false)
				if (dz && !dz.options.autoProcessQueue) {
					isManualUploadMode = true;
				}
				// Check if there are any files that have completed upload
				if (dz) {
					dz.files.forEach((file: any) => {
						if (file.status === 'success' && file.filenameInput && file.filenameInput.value) {
							hasSuccessfulUploads = true;
						}
					});
				}
			}
		});

		// Enable/disable submit button:
		// - In manual upload mode: enable when files are added (queued)
		// - In auto upload mode: enable only after successful uploads
		if (submitButton) {
			if (isManualUploadMode) {
				// Manual upload: enable when files are queued
				(submitButton as HTMLButtonElement).disabled = !hasFiles;
			} else {
				// Auto upload: enable only after successful uploads
				(submitButton as HTMLButtonElement).disabled = !hasSuccessfulUploads;
			}
		}

		// Enable/disable reset button based on whether there are any files
		if (resetButton) {
			(resetButton as HTMLButtonElement).disabled = !hasFiles;
		}
	}

	/**
	 * Submit form data to server via AJAX
	 */
	protected _submitFormData(form: HTMLFormElement): void {
		// Get form action or use default
		const formAction = form.getAttribute('action') || 'upload/submit.php';
		const formMethod = form.getAttribute('method') || 'POST';

		// Create FormData from form
		const formData = new FormData(form);

		// Show loading state and disable both buttons
		const submitButton = form.querySelector('button[type="submit"]') as HTMLButtonElement;
		const resetButton = form.querySelector('button[type="reset"]') as HTMLButtonElement;
		const originalButtonText = submitButton ? submitButton.innerHTML : '';
		if (submitButton) {
			submitButton.disabled = true;
			submitButton.innerHTML = 'Processing...';
		}
		if (resetButton) {
			resetButton.disabled = true;
		}

		// Submit via fetch
		fetch(formAction, {
			method: formMethod,
			body: formData,
		})
			.then((response) => response.json())
			.then((data) => {
				if (data.success) {
					// Success - clear all dropzones in the form
					const formDropzones = form.querySelectorAll('[data-kt-dropzone="true"]');
					formDropzones.forEach((dzElement) => {
						const instance = KTDropzone.getInstance(dzElement as HTMLElement);
						if (instance) {
							instance.removeAllFiles();
						}
					});

					// Hide file list
					const fileListContainer = form.querySelector('.file-upload-list');
					if (fileListContainer) {
						fileListContainer.classList.add('hidden');
					}

					// Clear hidden inputs
					const hiddenInputsContainer = form.querySelector('.dropzone-hidden-inputs');
					if (hiddenInputsContainer) {
						hiddenInputsContainer.innerHTML = '';
					}

					// Reset dropzone state
					const dropzoneElement = form.querySelector('.dropzone');
					if (dropzoneElement) {
						dropzoneElement.classList.remove('dz-started');
					}

					// Keep both buttons disabled after successful submission
					if (submitButton) {
						submitButton.disabled = true;
						submitButton.innerHTML = originalButtonText;
					}
					if (resetButton) {
						resetButton.disabled = true;
					}
				} else {
					// Handle errors - restore button text and update state
					if (submitButton) {
						submitButton.innerHTML = originalButtonText;
					}
					this._updateSubmitButtonState(form);
				}
			})
			.catch((error) => {
				console.error('Form submission error:', error);
				// Restore button text and update state
				if (submitButton) {
					submitButton.innerHTML = originalButtonText;
				}
				this._updateSubmitButtonState(form);
			});
	}

	/**
	 * Dispose the dropzone instance
	 */
	public dispose(): void {
		if (this._dropzone) {
			this._dropzone.destroy();
			this._dropzone = null;
		}
		this._initialized = false;
		super.dispose();
	}

	/**
	 * Process the queue of files
	 */
	public processQueue(): void {
		if (this._dropzone) {
			this._dropzone.processQueue();
		}
	}

	/**
	 * Remove all files
	 */
	public removeAllFiles(deleteFiles: boolean = false): void {
		if (this._dropzone) {
			this._dropzone.removeAllFiles(deleteFiles);
		}
	}

	/**
	 * Get the Dropzone instance
	 */
	public getDropzone(): any {
		return this._dropzone;
	}

	/**
	 * Get all files
	 */
	public getFiles(): any[] {
		return this._dropzone ? this._dropzone.files : [];
	}

	/**
	 * Get queued files
	 */
	public getQueuedFiles(): any[] {
		return this._dropzone ? this._dropzone.getQueuedFiles() : [];
	}

	/**
	 * Get uploading files
	 */
	public getUploadingFiles(): any[] {
		return this._dropzone ? this._dropzone.getUploadingFiles() : [];
	}

	/**
	 * Get instance by element
	 */
	public static getInstance(element: HTMLElement | string): KTDropzone | null {
		const targetElement = KTDom.getElement(element);
		if (!targetElement) return null;
		return KTData.get(targetElement, 'ktDropzone') as KTDropzone | null;
	}

	/**
	 * Create instances for all dropzones following Arena pattern
	 * Finds forms with data-kt-dropzone="true" and initializes .dropzone elements inside
	 */
	public static createInstances(): void {
		// Check if Dropzone.js is available, retry if not (in case scripts are still loading)
		if (typeof window.Dropzone === 'undefined') {
			// Retry after a short delay to allow scripts to load
			setTimeout(() => {
				KTDropzone.createInstances();
			}, 100);
			return;
		}

		// Disable Dropzone.js auto-discovery to prevent duplicate initialization
		if (window.Dropzone && (window.Dropzone as any).autoDiscover !== false) {
			(window.Dropzone as any).autoDiscover = false;
		}

		// Find all forms with data-kt-dropzone="true" (following Arena pattern)
		const forms = document.querySelectorAll(
			'[data-kt-dropzone="true"]:not([data-kt-dropzone=false])'
		);

		forms.forEach((form) => {
			// Find dropzone elements within this form
			const formDropzones = (form as HTMLElement).querySelectorAll('.dropzone');

			if (formDropzones.length === 0) {
				return;
			}

			// Disable submit and reset buttons initially
			const submitButton = (form as HTMLElement).querySelector('button[type="submit"]');
			const resetButton = (form as HTMLElement).querySelector('button[type="reset"]');
			if (submitButton) {
				(submitButton as HTMLButtonElement).disabled = true;
			}
			if (resetButton) {
				(resetButton as HTMLButtonElement).disabled = true;
			}

			// Initialize each dropzone in the form
			formDropzones.forEach((dropzone) => {
				// Skip if already has Dropzone.js instance attached
				if ((dropzone as any).dropzone) {
					return;
				}
				new KTDropzone(dropzone as HTMLElement);
			});
		});
	}

	/**
	 * Initialize all dropzones
	 */
	public static init(): void {
		KTDropzone.createInstances();

		if (window.KT_DROPZONE_INITIALIZED !== true) {
			window.KT_DROPZONE_INITIALIZED = true;
		}
	}
}
