document.addEventListener('DOMContentLoaded', function() {
	var table = document.getElementById('team-members-datatable');
	if (table && typeof window.$ !== 'undefined' && !$.fn.DataTable.isDataTable(table)) {
		var dt = $(table).DataTable({
			pageLength: 10,
			lengthMenu: [[10, 25, 50], [10, 25, 50]],
			language: { search: '', searchPlaceholder: 'Search' },
			layout: { topStart: [], topEnd: 'search', bottomStart: ['pageLength', 'info'], bottomEnd: 'paging' },
			columnDefs: [
				{ orderable: false, targets: [0, 6] },
				{ width: '220px', targets: 2 }
			]
		});

		// Select all checkbox in header (current page rows only)
		var selectAll = document.getElementById('team-members-datatable-select-all');
		if (selectAll) {
			function getCurrentPageRows() {
				return dt.rows({ page: 'current' }).nodes();
			}
			function updateSelectAllState() {
				var rows = getCurrentPageRows();
				var checkboxes = $(rows).find('td:first-child input[type="checkbox"]');
				var checkedCount = checkboxes.filter(':checked').length;
				var total = checkboxes.length;
				selectAll.checked = total > 0 && checkedCount === total;
				selectAll.indeterminate = checkedCount > 0 && checkedCount < total;
			}
			selectAll.addEventListener('change', function() {
				var checked = this.checked;
				$(getCurrentPageRows()).each(function() {
					var cb = this.querySelector('td:first-child input[type="checkbox"]');
					if (cb) cb.checked = checked;
				});
			});
			$(table).on('draw.dt', updateSelectAllState);
			$(table).on('change', 'tbody td:first-child input[type="checkbox"]', updateSelectAllState);
		}
	}
});
