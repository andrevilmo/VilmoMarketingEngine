/**
 * Canvas Confetti demo - single trigger button
 */
(function () {
	'use strict';

	var btn = document.getElementById('confetti_demo_btn');
	if (!btn || typeof confetti !== 'function') return;

	btn.addEventListener('click', function () {
		confetti({
			particleCount: 100,
			spread: 70,
			origin: { y: 0.6 },
		});
	});
})();
