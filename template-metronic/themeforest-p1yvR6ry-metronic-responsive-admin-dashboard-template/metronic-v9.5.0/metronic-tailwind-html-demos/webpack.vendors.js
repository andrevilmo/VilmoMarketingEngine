module.exports = {
	output: 'dist/assets',
	entry: {
		keenicons: [
			{
				src: ['src/vendors/keenicons/**/style.css'],
				dist: '/vendors/keenicons/styles.bundle.css',
				bundle: true,
			},
			{
				src: [
					'src/vendors/keenicons/duotone/fonts',
					'src/vendors/keenicons/filled/fonts',
					'src/vendors/keenicons/outline/fonts',
					'src/vendors/keenicons/solid/fonts',
				],
				dist: '/vendors/keenicons/fonts',
			},
		],
		leaflet: [
			{
				src: ['node_modules/leaflet/dist/leaflet.css'],
				dist: '/vendors/leaflet/leaflet.bundle.css',
				bundle: true,
			},
			{
				src: [
					'node_modules/leaflet/dist/leaflet.js',
					'node_modules/esri-leaflet/dist/esri-leaflet.js',
					'node_modules/esri-leaflet-geocoder/dist/esri-leaflet-geocoder.js',
				],
				dist: '/vendors/leaflet/leaflet.bundle.js',
				bundle: true,
			},
			{
				src: ['node_modules/leaflet/dist/images'],
				dist: '/vendors/leaflet/images',
			},
		],
		apexcharts: [
			{
				src: ['node_modules/apexcharts/dist/apexcharts.css'],
				dist: '/vendors/apexcharts/apexcharts.css',
			},
			{
				src: ['node_modules/apexcharts/dist/apexcharts.min.js'],
				dist: '/vendors/apexcharts/apexcharts.min.js',
			},
		],
		prismjs: [
			{
				src: [
					'node_modules/prismjs/prism.js',
					'node_modules/prismjs/components/prism-markup.js',
					'node_modules/prismjs/components/prism-markup-templating.js',
					'node_modules/prismjs/components/prism-bash.js',
					'node_modules/prismjs/components/prism-javascript.js',
					'node_modules/prismjs/components/prism-css.js',
					'node_modules/prismjs/plugins/normalize-whitespace/prism-normalize-whitespace.js',
					'src/vendors/prismjs/prismjs.init.js',
				],
				dist: '/vendors/prismjs/prismjs.min.js',
				bundle: true,
			},
		],
		clipboard: [
			{
				src: ['node_modules/clipboard/dist/clipboard.min.js'],
				dist: '/vendors/clipboard/clipboard.min.js',
			},
		],
		fullcalendar: [
			{
				src: ['node_modules/fullcalendar/index.global.min.js'],
				dist: '/vendors/fullcalendar/index.global.min.js',
			},
		],
		'canvas-confetti': [
			{
				src: ['node_modules/canvas-confetti/dist/confetti.browser.js'],
				dist: '/vendors/canvas-confetti/canvas-confetti.min.js',
			},
		],
		jquery: [
			{
				src: ['node_modules/jquery/dist/jquery.min.js'],
				dist: '/vendors/jquery/jquery.min.js',
			},
		],
		datatablesNet: [
			{
				src: ['node_modules/datatables.net/js/dataTables.min.js'],
				dist: '/vendors/datatables-net/dataTables.min.js',
			},
		],
		dropzone: [
			{
				src: ['node_modules/dropzone/dist/dropzone.css'],
				dist: '/vendors/dropzone/dropzone.css',
			},
			{
				src: ['node_modules/dropzone/dist/dropzone.js'],
				dist: '/vendors/dropzone/dropzone.js',
			},
		],
		ktui: [
			{
				src: ['node_modules/@keenthemes/ktui/dist/ktui.min.js'],
				dist: '/vendors/ktui/ktui.min.js',
			},
		],
	},
};
