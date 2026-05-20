const TOKEN_KEY = 'tcpproducer_admin_token';

// Панель на https://антон.su/tcp/ — API не в корне домена
const API_BASE = window.location.pathname.startsWith('/tcp') ? '/tcp' : '';

function apiUrl(path) {
	return `${API_BASE}${path}`;
}

const tokenInput = document.getElementById('tokenInput');
const saveTokenBtn = document.getElementById('saveTokenBtn');
const statusBtn = document.getElementById('statusBtn');
const startBtn = document.getElementById('startBtn');
const stopBtn = document.getElementById('stopBtn');
const deployBtn = document.getElementById('deployBtn');
const clearBtn = document.getElementById('clearBtn');
const logsStartBtn = document.getElementById('logsStartBtn');
const logsStopBtn = document.getElementById('logsStopBtn');
const logsClearBtn = document.getElementById('logsClearBtn');
const output = document.getElementById('output');
const logsOutput = document.getElementById('logsOutput');
const statusBadge = document.getElementById('statusBadge');
const packetCount = document.getElementById('packetCount');
const packetCount60 = document.getElementById('packetCount60');
const packetsChart = document.getElementById('packetsChart');

let logsAbortController = null;
let statsAbortController = null;

const buttons = [statusBtn, startBtn, stopBtn, deployBtn];

function getToken() {
	return sessionStorage.getItem(TOKEN_KEY) || '';
}

function saveToken() {
	const token = tokenInput.value.trim();
	if (!token) {
		appendOutput('Введите API-токен.');
		return;
	}

	sessionStorage.setItem(TOKEN_KEY, token);
	appendOutput('Токен сохранён в sessionStorage.');
	startStatsStream();
}

async function apiRequest(path, method = 'GET') {
	const token = getToken();
	if (!token) {
		appendOutput('Сначала сохраните API-токен.');
		throw new Error('no token');
	}

	const response = await fetch(apiUrl(path), {
		method,
		headers: {
			Authorization: `Bearer ${token}`,
		},
	});

	const data = await response.json().catch(() => ({}));

	if (response.status === 401) {
		appendOutput('Ошибка авторизации. Проверьте ADMIN_API_TOKEN.');
		throw new Error('unauthorized');
	}

	if (!response.ok) {
		appendOutput(data.error || `HTTP ${response.status}`);
		throw new Error('request failed');
	}

	return data;
}

function setLoading(isLoading) {
	buttons.forEach((btn) => {
		btn.disabled = isLoading;
	});

	statusBadge.textContent = isLoading ? 'Загрузка…' : statusBadge.dataset.label || '—';
	statusBadge.className = `status-badge${isLoading ? ' loading' : ''}`;
}

function updateBadgeFromStatus(data) {
	const label = data.isRunning ? 'Работает' : `${data.activeState} / ${data.subState}`;
	statusBadge.dataset.label = label;
	statusBadge.textContent = label;
	statusBadge.className = `status-badge${data.isRunning ? ' running' : ' stopped'}`;
}

function appendOutput(text) {
	const stamp = new Date().toLocaleTimeString('ru-RU');
	output.textContent = `[${stamp}] ${text}\n\n${output.textContent}`.trim();
}

function formatPacketCount(value) {
	return new Intl.NumberFormat('ru-RU').format(value);
}

function renderPacketsChart(timeline) {
	if (!timeline?.length) {
		packetsChart.innerHTML = '<p class="chart-empty">Нет данных за последние 60 минут</p>';
		return;
	}

	const max = Math.max(1, ...timeline.map((p) => p.count ?? 0));
	const lastIndex = timeline.length - 1;
	const axisIndexes = [0, 10, 20, 30, 40, 50, lastIndex].filter(
		(idx, pos, arr) => idx <= lastIndex && arr.indexOf(idx) === pos,
	);

	const bars = timeline
		.map((point) => {
			const count = point.count ?? 0;
			const height = Math.round((count / max) * 100);
			const label = point.label || '';
			return `<div class="chart-bar-wrap" title="${label}: ${formatPacketCount(count)} пакетов">
				<div class="chart-bar" style="height:${height}%"></div>
			</div>`;
		})
		.join('');

	const axis = axisIndexes
		.map((idx) => `<span class="chart-axis-label">${timeline[idx].label}</span>`)
		.join('');

	packetsChart.innerHTML = `<div class="chart-bars">${bars}</div><div class="chart-axis">${axis}</div>`;
}

function applyStats(data) {
	const updated = data.updatedAt
		? new Date(data.updatedAt).toLocaleString('ru-RU')
		: '—';
	const unavailable = 'Сервис не запущен или stats.json ещё не создан';

	packetCount60.textContent = formatPacketCount(data.last60Minutes ?? 0);
	packetCount60.title = data.available
		? `За 60 мин · обновлено: ${updated}`
		: unavailable;

	packetCount.textContent = formatPacketCount(data.totalSent ?? 0);
	packetCount.title = data.available
		? `Всего · обновлено: ${updated}`
		: unavailable;

	if (data.available && data.timeline?.length)
		renderPacketsChart(data.timeline);
	else if (!data.available)
		packetsChart.innerHTML = '<p class="chart-empty">Сервис не запущен</p>';
}

async function consumeSseStream(path, onEvent, signal) {
	const token = getToken();
	if (!token)
		throw new Error('no token');

	const response = await fetch(apiUrl(path), {
		headers: { Authorization: `Bearer ${token}` },
		signal,
	});

	if (response.status === 401)
		throw new Error('unauthorized');

	if (!response.ok)
		throw new Error(`HTTP ${response.status}`);

	const reader = response.body.getReader();
	const decoder = new TextDecoder();
	let buffer = '';

	while (true) {
		const { done, value } = await reader.read();
		if (done)
			break;

		buffer += decoder.decode(value, { stream: true });
		const events = buffer.split('\n\n');
		buffer = events.pop() || '';

		for (const event of events) {
			const data = event
				.split('\n')
				.filter((row) => row.startsWith('data: '))
				.map((row) => row.slice(6))
				.join('\n');

			if (data)
				onEvent(data);
		}
	}
}

function stopStatsStream() {
	if (statsAbortController) {
		statsAbortController.abort();
		statsAbortController = null;
	}
}

async function startStatsStream() {
	const token = getToken();
	if (!token) {
		packetCount60.textContent = '—';
		packetCount.textContent = '—';
		packetsChart.innerHTML = '<p class="chart-empty">Подключите токен для загрузки графика</p>';
		return;
	}

	stopStatsStream();
	statsAbortController = new AbortController();

	try {
		await consumeSseStream(
			'/api/stats/stream',
			(raw) => {
				try {
					applyStats(JSON.parse(raw));
				} catch {
					// пропуск битого события
				}
			},
			statsAbortController.signal,
		);
	} catch (err) {
		if (err.name !== 'AbortError') {
			packetCount60.textContent = '—';
			packetCount.textContent = '—';
		}
	} finally {
		statsAbortController = null;
	}
}

async function loadStatus(silent = false) {
	if (!silent)
		appendOutput('Запрос статуса…');

	const data = await apiRequest('/api/status');
	updateBadgeFromStatus(data);

	if (!silent) {
		const lines = [
			`Сервис: ${data.service}`,
			`Состояние: ${data.activeState} (${data.subState})`,
			`PID: ${data.mainPid || '—'}`,
			`С: ${data.activeSince || '—'}`,
			'',
			data.raw,
		];
		appendOutput(lines.join('\n'));
	}
}

async function runAction(label, path) {
	appendOutput(`${label}…`);
	setLoading(true);

	try {
		const data = await apiRequest(path, 'POST');
		appendOutput(data.output || (data.ok ? 'OK' : 'Ошибка'));
		await loadStatus(true);
	} finally {
		setLoading(false);
	}
}

saveTokenBtn.addEventListener('click', saveToken);
statusBtn.addEventListener('click', async () => {
	setLoading(true);
	try {
		await loadStatus(false);
	} catch {
		// ошибка уже в output
	} finally {
		setLoading(false);
	}
});
startBtn.addEventListener('click', () => runAction('Запуск сервиса', '/api/start').catch(() => {}));
stopBtn.addEventListener('click', () => runAction('Остановка сервиса', '/api/stop').catch(() => {}));
deployBtn.addEventListener('click', () => runAction('Деплой', '/api/deploy').catch(() => {}));
clearBtn.addEventListener('click', () => {
	output.textContent = 'Готово.';
});

function appendLogLine(text) {
	logsOutput.textContent += `${text}\n`;
	logsOutput.scrollTop = logsOutput.scrollHeight;
}

function setLogsStreaming(isStreaming) {
	logsStartBtn.disabled = isStreaming;
	logsStopBtn.disabled = !isStreaming;
}

function stopLogs() {
	if (logsAbortController) {
		logsAbortController.abort();
		logsAbortController = null;
	}

	setLogsStreaming(false);
}

async function startLogs() {
	const token = getToken();
	if (!token) {
		logsOutput.textContent = 'Сначала сохраните API-токен.';
		return;
	}

	stopLogs();
	logsOutput.textContent = 'Подключение к потоку логов…\n';
	setLogsStreaming(true);

	logsAbortController = new AbortController();

	try {
		logsOutput.textContent = '--- логи tcpproducer ---\n';
		await consumeSseStream('/api/logs/stream', (line) => appendLogLine(line), logsAbortController.signal);
		appendLogLine('--- поток завершён ---');
	} catch (err) {
		if (err.message === 'unauthorized') {
			logsOutput.textContent = 'Ошибка авторизации. Проверьте ADMIN_API_TOKEN.';
			return;
		}
		if (err.name !== 'AbortError')
			appendLogLine(`Ошибка: ${err.message}`);
	} finally {
		setLogsStreaming(false);
		logsAbortController = null;
	}
}

logsStartBtn.addEventListener('click', () => startLogs());
logsStopBtn.addEventListener('click', () => {
	stopLogs();
	appendLogLine('--- остановлено пользователем ---');
});
logsClearBtn.addEventListener('click', () => {
	logsOutput.textContent = 'Нажмите «Смотреть онлайн».';
});

const savedToken = getToken();
if (savedToken)
	tokenInput.value = savedToken;

if (savedToken) {
	startStatsStream();
	loadStatus(true).catch(() => {});
}
