/**
 * TinyMCE form integration for account settings (Bio / About field).
 * Initializes the editor on #settings-bio-editor when present.
 */
document.addEventListener("DOMContentLoaded", function () {
	if (typeof tinymce === "undefined") {
		return;
	}
	var editorEl = document.getElementById("settings-bio-editor");
	if (!editorEl) {
		return;
	}
	tinymce.init({
		target: editorEl,
		license_key: "gpl",
		height: 200,
		menubar: false,
		plugins: ["autolink", "lists", "link"],
		toolbar:
			"undo redo | bold italic forecolor | alignleft aligncenter alignright alignjustify | bullist numlist | link",
		toolbar_mode: "sliding",
		content_style:
			"@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');" +
			" body { font-family: 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 0.875rem; line-height: 1.5; -webkit-font-smoothing: antialiased; -moz-osx-font-smoothing: grayscale; }",
		setup: function (editor) {
			editor.on("change", function () {
				editor.save();
			});
		},
	});
});
