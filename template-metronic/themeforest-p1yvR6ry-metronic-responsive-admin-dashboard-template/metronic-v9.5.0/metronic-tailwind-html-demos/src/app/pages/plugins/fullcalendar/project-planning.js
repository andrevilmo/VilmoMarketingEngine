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

	// Get CSS variable values with sensible fallback
	var getColor = function(varName, fallback) {
		var value = getComputedStyle(document.documentElement).getPropertyValue(varName).trim();
		return value || fallback;
	};

	var projectColors = [
		{ bg: getColor('--primary', '#3b82f6'), text: getColor('--primary-foreground', '#ffffff') },
		{ bg: getColor('--danger', '#ef4444'), text: getColor('--danger-foreground', '#ffffff') },
		{ bg: getColor('--success', '#22c55e'), text: getColor('--success-foreground', '#ffffff') },
		{ bg: getColor('--warning', '#f59e0b'), text: getColor('--warning-foreground', '#111827') },
		{ bg: getColor('--info', '#06b6d4'), text: getColor('--info-foreground', '#ffffff') }
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

	function toLocalDateTimeString(date) {
		var pad = function(num) { return String(num).padStart(2, '0'); };
		return date.getFullYear() + '-' +
			pad(date.getMonth() + 1) + '-' +
			pad(date.getDate()) + 'T' +
			pad(date.getHours()) + ':' +
			pad(date.getMinutes()) + ':' +
			pad(date.getSeconds());
	}

	function scatterEventsAcrossMonth(items) {
		var daysInMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0).getDate();
		var spreadPattern = [2, 5, 8, 11, 14, 17, 20, 23, 26, 28];
		var randomColor = function() {
			return projectColors[Math.floor(Math.random() * projectColors.length)];
		};

		return items.map(function(event, index) {
			var start = new Date(event.start);
			var end = new Date(event.end);
			var baseDay = spreadPattern[index % spreadPattern.length];
			var waveOffset = Math.floor(index / spreadPattern.length);
			var targetDay = ((baseDay - 1 + (waveOffset * 3)) % daysInMonth) + 1;
			var color = randomColor();

			start.setFullYear(today.getFullYear(), today.getMonth(), targetDay);
			end.setFullYear(today.getFullYear(), today.getMonth(), targetDay);

			return Object.assign({}, event, {
				start: toLocalDateTimeString(start),
				end: toLocalDateTimeString(end),
				display: 'block',
				color: color.bg,
				backgroundColor: color.bg,
				textColor: color.text
			});
		});
	}

	events = scatterEventsAcrossMonth(events);

	function getRandomInt(min, max) {
		return Math.floor(Math.random() * (max - min + 1)) + min;
	}

	function addRandomEvents(items, count) {
		var taskPrefixes = ['API', 'UI', 'QA', 'BUG', 'OPS', 'DOC', 'DEV'];
		var taskLabels = [
			'Review backlog',
			'Implement feature flag',
			'Fix validation issue',
			'Refine onboarding flow',
			'Write test coverage',
			'Prepare release notes',
			'Run performance check',
			'Database cleanup',
			'Accessibility pass',
			'Update project board'
		];
		var daysInMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0).getDate();

		for (var i = 0; i < count; i++) {
			var day = getRandomInt(1, daysInMonth);
			var startHour = getRandomInt(8, 16);
			var durationHours = getRandomInt(1, 3);
			var endHour = Math.min(18, startHour + durationHours);
			var startMinute = [0, 30][getRandomInt(0, 1)];
			var color = projectColors[getRandomInt(0, projectColors.length - 1)];
			var assignee = assignees[getRandomInt(0, assignees.length - 1)];
			var project = projects[getRandomInt(0, projects.length - 1)];
			var prefix = taskPrefixes[getRandomInt(0, taskPrefixes.length - 1)];
			var ticket = getRandomInt(100, 999);
			var label = taskLabels[getRandomInt(0, taskLabels.length - 1)];

			var startDate = new Date(today.getFullYear(), today.getMonth(), day, startHour, startMinute, 0, 0);
			var endDate = new Date(today.getFullYear(), today.getMonth(), day, endHour, startMinute, 0, 0);

			items.push({
				title: prefix + '-' + ticket + ': ' + label,
				start: toLocalDateTimeString(startDate),
				end: toLocalDateTimeString(endDate),
				display: 'block',
				color: color.bg,
				backgroundColor: color.bg,
				textColor: color.text,
				extendedProps: {
					assignee: assignee,
					project: project,
					sprint: 'Sprint ' + getRandomInt(24, 28)
				}
			});
		}

		return items;
	}

	events = addRandomEvents(events, 28);

	var calendar = new FullCalendar.Calendar(calendarEl, {
		initialView: 'dayGridMonth',
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
				eventDisplay: 'block',
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
