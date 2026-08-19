export const plugins = {
	'postcss-import': {},
	'postcss-nesting': {},
	'postcss-preset-env': {
		features: { 'nesting-rules': false },
	},
	'@tailwindcss/postcss': {},
	autoprefixer: {},
};
