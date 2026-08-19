/**
 * Welcome page demo – show confetti when the welcome modal is shown and on button click.
 */
(function () {
	'use strict';

	if (typeof confetti !== 'function') return;

	function fireWelcomeConfetti() {
		confetti({
			particleCount: 100,
			spread: 70,
			origin: { y: 0.6 },
		});
	}

	var modalEl = document.getElementById('modal_welcome_confetti');
	if (modalEl) {
		var modal = KTModal.getInstance(modalEl);
		if (modal) {
			modalEl.addEventListener('shown.kt.modal', fireWelcomeConfetti);
		}
		// Show modal on load (same as welcome-message); fire confetti when shown
		window.addEventListener('load', function () {
			modal = KTModal.getInstance(modalEl) || new KTModal(modalEl);
			modal.show();
			// Fire confetti after modal is visible (fallback if shown.kt.modal is not dispatched)
			setTimeout(fireWelcomeConfetti, 300);
		});
	}

	var btn = document.getElementById('welcome_confetti_btn');
	if (btn) {
		btn.addEventListener('click', fireWelcomeConfetti);
	}
})();
