// Project planning calendar with real-looking data
document.addEventListener('DOMContentLoaded', function() {
	var calendarEl = document.getElementById('calendar-project-planning');

	if (!calendarEl) return;

		// Modal + form elements for editing tasks
		var modalEl = window.KTDom ? KTDom.getElement('#modal_project_task') : document.getElementById('modal_project_task');
		var modal = (typeof KTModal !== 'undefined' && modalEl) ? (KTModal.getInstance(modalEl) || new KTModal(modalEl)) : null;
		var formEl = modalEl ? modalEl.querySelector('#project_task_form') : null;
		var titleInput = modalEl ? modalEl.querySelector('[name="task_title"]') : null;
		var assigneeInput = modalEl ? modalEl.querySelector('[name="task_assignee"]') : null;
		var startDateInput = modalEl ? modalEl.querySelector('[name="task_start_date"]') : null;
		var endDateInput = modalEl ? modalEl.querySelector('[name="task_end_date"]') : null;
		var currentEvent = null;

		var monthShortNames = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

		function formatDateTimeForPicker(date) {
			if (!date) return '';
			var pad = function(num) { return String(num).padStart(2, '0'); };
			var day = pad(date.getDate());
			var month = monthShortNames[date.getMonth()];
			var year = date.getFullYear();
			var time = pad(date.getHours()) + ':' + pad(date.getMinutes());
			return day + ' ' + month + ' ' + year + ' ' + time;
		}

		function parseDateTimeFromPicker(value) {
			if (!value || typeof value !== 'string') return null;
			var parts = value.trim().split(/\s+/);
			if (parts.length < 3) return null;
			var day = parseInt(parts[0], 10);
			var monthIndex = monthShortNames.indexOf(parts[1]);
			var year = parseInt(parts[2], 10);
			if (monthIndex < 0 || isNaN(day) || isNaN(year)) return null;
			var d = new Date(year, monthIndex, day, 0, 0, 0, 0);
			if (parts.length >= 4) {
				var timeStr = parts[3];
				if (parts.length >= 5 && /AM|PM/i.test(parts[4])) {
					timeStr = parts[3] + ' ' + parts[4];
				}
				var match = timeStr.match(/^(\d{1,2}):(\d{2})\s*(AM|PM)?$/i);
				if (match) {
					var h = parseInt(match[1], 10);
					var m = parseInt(match[2], 10) || 0;
					if (match[3] && match[3].toUpperCase() === 'PM' && h < 12) h += 12;
					if (match[3] && match[3].toUpperCase() === 'AM' && h === 12) h = 0;
					d.setHours(h, m, 0, 0);
				}
			}
			return d;
		}

	// Get Monday of the week to display (if weekend, use next Monday)
	function getWeekMonday(date) {
		var d = new Date(date);
		var day = d.getDay();
		var diff = d.getDate() - day + (day === 0 ? -6 : 1);
		return new Date(d.setDate(diff));
	}

	var today = new Date();
	var weekStart = getWeekMonday(today);
	if (today.getDay() === 0 || today.getDay() === 6) {
		weekStart.setDate(weekStart.getDate() + 7);
	}

	function dateStr(dayOffset) {
		var d = new Date(weekStart);
		d.setDate(d.getDate() + dayOffset);
		return d.toISOString().split('T')[0];
	}

	function timeSlot(dayOffset, startHour, startMin, endHour, endMin) {
		var start = dateStr(dayOffset) + 'T' + String(startHour).padStart(2, '0') + ':' + String(startMin || 0).padStart(2, '0') + ':00';
		var end = dateStr(dayOffset) + 'T' + String(endHour).padStart(2, '0') + ':' + String(endMin || 0).padStart(2, '0') + ':00';
		return { start: start, end: end };
	}

	var assignees = ['Sarah Chen', 'Mike Johnson', 'Alex Rivera', 'Jordan Lee', 'Sam Wilson', 'Emma Davis', 'James Park'];
	var projects = ['Metronic v9', 'Store API', 'Mobile app'];

	// Get CSS variable values
	var getColor = function(varName) {
		return getComputedStyle(document.documentElement).getPropertyValue(varName).trim();
	};

	var projectColors = [
		{ bg: getColor('--primary'), text: getColor('--primary-foreground') },
		{ bg: getColor('--danger'), text: getColor('--danger-foreground') },
		{ bg: getColor('--success'), text: getColor('--success-foreground') }
	];

	var events = [
		{ title: 'Daily standup', start: timeSlot(0, 9, 0, 10, 0).start, end: timeSlot(0, 9, 0, 10, 0).end, extendedProps: { assignee: assignees[0], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'MET-142: Implement user auth flow', start: timeSlot(0, 10, 0, 12, 0).start, end: timeSlot(0, 10, 0, 12, 0).end, extendedProps: { assignee: assignees[1], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Sprint 24 planning', start: timeSlot(0, 14, 0, 16, 0).start, end: timeSlot(0, 14, 0, 16, 0).end, extendedProps: { assignee: assignees[2], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'DES-89: Review checkout UI', start: timeSlot(0, 9, 0, 11, 0).start, end: timeSlot(0, 9, 0, 11, 0).end, extendedProps: { assignee: assignees[3], project: projects[1], sprint: 'Sprint 24' }, backgroundColor: projectColors[1].bg, textColor: projectColors[1].text },
		{ title: 'API: Payment webhooks', start: timeSlot(1, 9, 0, 12, 0).start, end: timeSlot(1, 9, 0, 12, 0).end, extendedProps: { assignee: assignees[1], project: projects[1], sprint: 'Sprint 24' }, backgroundColor: projectColors[1].bg, textColor: projectColors[1].text },
		{ title: 'Daily standup', start: timeSlot(1, 9, 0, 10, 0).start, end: timeSlot(1, 9, 0, 10, 0).end, extendedProps: { assignee: assignees[0], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Frontend: Dashboard filters', start: timeSlot(1, 10, 0, 13, 0).start, end: timeSlot(1, 10, 0, 13, 0).end, extendedProps: { assignee: assignees[4], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'QA: Regression suite v2.1', start: timeSlot(1, 14, 0, 17, 0).start, end: timeSlot(1, 14, 0, 17, 0).end, extendedProps: { assignee: assignees[5], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Daily standup', start: timeSlot(2, 9, 0, 10, 0).start, end: timeSlot(2, 9, 0, 10, 0).end, extendedProps: { assignee: assignees[0], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Design: Onboarding wizard', start: timeSlot(2, 10, 0, 12, 0).start, end: timeSlot(2, 10, 0, 12, 0).end, extendedProps: { assignee: assignees[3], project: projects[2], sprint: 'Sprint 24' }, backgroundColor: projectColors[2].bg, textColor: projectColors[2].text },
		{ title: 'BUG-331: Fix cart total', start: timeSlot(2, 13, 0, 15, 0).start, end: timeSlot(2, 13, 0, 15, 0).end, extendedProps: { assignee: assignees[4], project: projects[1], sprint: 'Sprint 24' }, backgroundColor: projectColors[1].bg, textColor: projectColors[1].text },
		{ title: 'Content: Homepage copy', start: timeSlot(2, 14, 0, 17, 0).start, end: timeSlot(2, 14, 0, 17, 0).end, extendedProps: { assignee: assignees[6], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Daily standup', start: timeSlot(3, 9, 0, 10, 0).start, end: timeSlot(3, 9, 0, 10, 0).end, extendedProps: { assignee: assignees[0], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Deploy: Staging release', start: timeSlot(3, 10, 0, 12, 0).start, end: timeSlot(3, 10, 0, 12, 0).end, extendedProps: { assignee: assignees[2], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Stakeholder demo – Q2 roadmap', start: timeSlot(3, 14, 0, 16, 0).start, end: timeSlot(3, 14, 0, 16, 0).end, extendedProps: { assignee: assignees[1], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Daily standup', start: timeSlot(4, 9, 0, 10, 0).start, end: timeSlot(4, 9, 0, 10, 0).end, extendedProps: { assignee: assignees[0], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Retro: Sprint 24', start: timeSlot(4, 14, 0, 16, 0).start, end: timeSlot(4, 14, 0, 16, 0).end, extendedProps: { assignee: assignees[2], project: projects[0], sprint: 'Sprint 24' }, backgroundColor: projectColors[0].bg, textColor: projectColors[0].text },
		{ title: 'Mobile app: Push notifications', start: timeSlot(4, 10, 0, 12, 0).start, end: timeSlot(4, 10, 0, 12, 0).end, extendedProps: { assignee: assignees[6], project: projects[2], sprint: 'Sprint 24' }, backgroundColor: projectColors[2].bg, textColor: projectColors[2].text },
		{ title: 'Store API: Inventory sync', start: timeSlot(4, 13, 0, 17, 0).start, end: timeSlot(4, 13, 0, 17, 0).end, extendedProps: { assignee: assignees[1], project: projects[1], sprint: 'Sprint 24' }, backgroundColor: projectColors[1].bg, textColor: projectColors[1].text },
	];

	var calendar = new FullCalendar.Calendar(calendarEl, {
		initialView: 'timeGridWeek',
		headerToolbar: {
			left: 'prev,next today',
			center: 'title',
			right: 'dayGridMonth,timeGridWeek,timeGridDay,listWeek'
		},
		editable: true,
		events: events,
			eventClick: function(info) {
				if (!modal || !modalEl) return;

				info.jsEvent.preventDefault();

				currentEvent = info.event;

				if (titleInput) {
					titleInput.value = currentEvent.title || '';
				}
				if (assigneeInput) {
					assigneeInput.value = currentEvent.extendedProps.assignee || '';
				}
				if (startDateInput) {
					startDateInput.value = formatDateTimeForPicker(currentEvent.start);
				}
				if (endDateInput) {
					endDateInput.value = formatDateTimeForPicker(currentEvent.end || currentEvent.start);
				}

				modal.show();
			},
		eventContent: function(arg) {
			var assignee = arg.event.extendedProps.assignee || '';
			return {
				html: '<div class="flex flex-col gap-0.5 p-0.5">' +
					'<span class="fc-event-title truncate">' + (arg.event.title || '') + '</span>' +
					(assignee ? '<span class="text-2xs opacity-90 truncate">' + assignee + '</span>' : '') +
					'</div>'
			};
		},
		slotMinTime: '08:00:00',
		slotMaxTime: '18:00:00',
		views: {
			dayGridMonth: {
				titleFormat: { year: 'numeric', month: 'long' }
			},
			timeGridWeek: {
				titleFormat: { year: 'numeric', month: 'short', day: 'numeric' }
			},
			timeGridDay: {
				titleFormat: { year: 'numeric', month: 'short', day: 'numeric' }
			}
		},
		eventDidMount: function(arg) {
			var assignee = arg.event.extendedProps.assignee || '';
			var project = arg.event.extendedProps.project || '';
			var tooltip = arg.event.title + (assignee ? ' — ' + assignee : '') + (project ? ' (' + project + ')' : '');
			arg.el.setAttribute('title', tooltip);
		}
	});

	calendar.render();

	// Handle form submit to update the clicked event
	if (formEl) {
		formEl.addEventListener('submit', function(e) {
			e.preventDefault();

			if (!currentEvent) return;

			if (titleInput && titleInput.value) {
				currentEvent.setProp('title', titleInput.value);
			}

			if (assigneeInput) {
				currentEvent.setExtendedProp('assignee', assigneeInput.value);
			}

			var newStart = parseDateTimeFromPicker(startDateInput && startDateInput.value);
			if (newStart) {
				currentEvent.setStart(newStart);
			}

			var newEnd = parseDateTimeFromPicker(endDateInput && endDateInput.value);
			if (newEnd) {
				currentEvent.setEnd(newEnd);
			}

			if (modal) {
				modal.hide();
			}
		});
	}
});
