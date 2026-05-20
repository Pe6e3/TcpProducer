const TOKEN_KEY = 'tcpproducer_admin_token';

// Панель на https://антон.su/tcp/ — API не в корне домена
const API_BASE = window.location.pathname.startsWith('/tcp') ? '/tcp' : '';

function apiUrl(path) {
	return `${API_BASE}${path}`;
}

const tokenCard = document.getElementById('tokenCard');
const tokenInput = document.getElementById('tokenInput');
const saveTokenBtn = document.getElementById('saveTokenBtn');
const powerToggle = document.getElementById('powerToggle');
const logsStartBtn = document.getElementById('logsStartBtn');
const logsStopBtn = document.getElementById('logsStopBtn');
const logsClearBtn = document.getElementById('logsClearBtn');
const logsOutput = document.getElementById('logsOutput');
const packetCount = document.getElementById('packetCount');
const packetCount60 = document.getElementById('packetCount60');
const packetsChart = document.getElementById('packetsChart');
const configCard = document.getElementById('configCard');
const serialsPanel = document.getElementById('serialsPanel');
const toggleDevicesBtn = document.getElementById('toggleDevicesBtn');
const configReloadBtn = document.getElementById('configReloadBtn');
const configSaveBtn = document.getElementById('configSaveBtn');
const serialsReloadBtn = document.getElementById('serialsReloadBtn');
const serialsSaveBtn = document.getElementById('serialsSaveBtn');
const serialsContent = document.getElementById('serialsContent');
const serialsLineCount = document.getElementById('serialsLineCount');

let logsAbortController = null;
let statsAbortController = null;
let statusPollTimer = null;
let serviceIsRunning = false;
let serviceToggleBusy = false;
let serialsLoaded = false;
let devicesPanelVisible = false;

function getToken() {
	return sessionStorage.getItem(TOKEN_KEY) || '';
}

function setTokenCardVisible(visible) {
	tokenCard.hidden = !visible;
}

async function validateToken(token = getToken()) {
	if (!token)
		return false;

	try {
		const response = await fetch(apiUrl('/api/status'), {
			headers: { Authorization: `Bearer ${token}` },
		});
		return response.ok;
	} catch {
		return false;
	}
}

function stopStatusPolling() {
	if (statusPollTimer) {
		clearInterval(statusPollTimer);
		statusPollTimer = null;
	}
}

function startStatusPolling() {
	stopStatusPolling();
	refreshServiceStatus().catch(() => {});
	statusPollTimer = setInterval(() => refreshServiceStatus().catch(() => {}), 5000);
}

function setPowerToggleLoading(isLoading) {
	serviceToggleBusy = isLoading;
	powerToggle.disabled = isLoading || !getToken();
	powerToggle.classList.toggle('is-loading', isLoading);
	if (isLoading)
		powerToggle.classList.remove('is-on', 'is-off');
}

function updateServiceToggle(data) {
	serviceIsRunning = !!data.isRunning;
	powerToggle.classList.toggle('is-on', data.isRunning);
	powerToggle.classList.toggle('is-off', !data.isRunning);
	powerToggle.setAttribute('aria-pressed', String(data.isRunning));
	powerToggle.title = data.isRunning
		? 'Сервис работает — нажмите для остановки'
		: 'Сервис остановлен — нажмите для запуска';
	if (!serviceToggleBusy)
		powerToggle.disabled = !getToken();
}

async function refreshServiceStatus() {
	const data = await apiRequest('/api/status');
	updateServiceToggle(data);
	return data;
}

async function toggleService() {
	if (serviceToggleBusy || !getToken())
		return;

	const path = serviceIsRunning ? '/api/stop' : '/api/start';

	setPowerToggleLoading(true);

	try {
		await apiRequest(path, 'POST');
		await refreshServiceStatus();
	} catch {
		// ошибка показана в showError
	} finally {
		setPowerToggleLoading(false);
		updateServiceToggle({ isRunning: serviceIsRunning });
	}
}

function toggleDevicesPanel() {
	devicesPanelVisible = !devicesPanelVisible;
	serialsPanel.hidden = !devicesPanelVisible;
	toggleDevicesBtn.classList.toggle('btn-active', devicesPanelVisible);

	if (devicesPanelVisible && !serialsLoaded)
		loadSerials()
			.then(() => {
				serialsLoaded = true;
			})
			.catch(() => {});
}

async function saveToken() {
	const token = tokenInput.value.trim();
	if (!token) {
		showError('Введите API-токен.');
		return;
	}

	if (!await validateToken(token)) {
		showError('Неверный API-токен.');
		return;
	}

	sessionStorage.setItem(TOKEN_KEY, token);
	setTokenCardVisible(false);
	configCard.hidden = false;
	powerToggle.disabled = false;
	startStatsStream();
	startStatusPolling();
	loadConfig().catch(() => {});
}

function fillConfigForm(config) {
	const d = config.device || {};

	document.getElementById('cfgMaxDevices').value = d.maxDevices ?? 0;
	document.getElementById('cfgConnectInterval').value = d.connectInterval || '';
	document.getElementById('cfgPacketInterval').value = d.packetInterval || '';
	document.getElementById('cfgSessionDuration').value = d.sessionDuration || '';
}

function collectConfigForm() {
	return {
		device: {
			maxDevices: Number(document.getElementById('cfgMaxDevices').value),
			connectInterval: document.getElementById('cfgConnectInterval').value.trim(),
			packetInterval: document.getElementById('cfgPacketInterval').value.trim(),
			sessionDuration: document.getElementById('cfgSessionDuration').value.trim(),
		},
	};
}

function updateSerialsLineCount(count) {
	serialsLineCount.textContent = `${count} в файле`;
}

async function loadSerials() {
	const data = await apiRequest('/api/serials');
	serialsContent.value = data.content || '';
	updateSerialsLineCount(data.lineCount ?? 0);
}

async function saveSerials() {
	if (!confirm('Сохранить deviceserials.txt и перезапустить tcpproducer?'))
		return;

	serialsSaveBtn.disabled = true;
	try {
		const result = await apiRequest('/api/serials?restart=true', 'PUT', {
			content: serialsContent.value,
		});
		updateSerialsLineCount(
			serialsContent.value
				.split(/\r?\n/)
				.filter((l) => l.trim() && !l.trim().startsWith('#'))
				.length,
		);
		await refreshServiceStatus();
	} catch {
		// ошибка показана в showError
	} finally {
		serialsSaveBtn.disabled = false;
	}
}

async function loadConfig() {
	const config = await apiRequest('/api/config');
	fillConfigForm(config);
}

async function saveConfig() {
	if (!confirm('Сохранить appsettings.json и перезапустить tcpproducer?'))
		return;

	configSaveBtn.disabled = true;
	try {
		const result = await apiRequest('/api/config?restart=true', 'PUT', collectConfigForm());
		await refreshServiceStatus();
	} catch {
		// ошибка показана в showError
	} finally {
		configSaveBtn.disabled = false;
	}
}

configReloadBtn.addEventListener('click', () => loadConfig().catch(() => {}));
configSaveBtn.addEventListener('click', () => saveConfig().catch(() => {}));
toggleDevicesBtn.addEventListener('click', toggleDevicesPanel);
serialsReloadBtn.addEventListener('click', () => loadSerials().catch(() => {}));
serialsSaveBtn.addEventListener('click', () => saveSerials().catch(() => {}));
powerToggle.addEventListener('click', () => toggleService().catch(() => {}));

async function apiRequest(path, method = 'GET', body = null) {
	const token = getToken();
	if (!token) {
		showError('Сначала сохраните API-токен.');
		throw new Error('no token');
	}

	const headers = { Authorization: `Bearer ${token}` };
	if (body !== null)
		headers['Content-Type'] = 'application/json';

	const response = await fetch(apiUrl(path), {
		method,
		headers,
		body: body !== null ? JSON.stringify(body) : undefined,
	});

	const data = await response.json().catch(() => ({}));

	if (response.status === 401) {
		sessionStorage.removeItem(TOKEN_KEY);
		setTokenCardVisible(true);
		stopStatusPolling();
		powerToggle.disabled = true;
		powerToggle.classList.remove('is-on', 'is-off');
		powerToggle.classList.add('is-off');
		showError('Ошибка авторизации. Введите API-токен заново.');
		throw new Error('unauthorized');
	}

	if (!response.ok) {
		showError(data.error || `HTTP ${response.status}`);
		throw new Error('request failed');
	}

	return data;
}

function showError(text) {
	alert(text);
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
		.map((point, index) => {
			const count = point.count ?? 0;
			const height = Math.round((count / max) * 100);
			const label = point.label || '';
			const barClass = index === lastIndex ? 'chart-bar chart-bar-current' : 'chart-bar';
			return `<div class="chart-bar-wrap" title="${label}: ${formatPacketCount(count)} пакетов">
				<div class="${barClass}" style="height:${height}%"></div>
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

	if (response.status === 401) {
		sessionStorage.removeItem(TOKEN_KEY);
		setTokenCardVisible(true);
		stopStatusPolling();
		powerToggle.disabled = true;
		throw new Error('unauthorized');
	}

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

saveTokenBtn.addEventListener('click', saveToken);

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

async function initAuth() {
	const savedToken = getToken();

	if (!savedToken) {
		setTokenCardVisible(true);
		powerToggle.disabled = true;
		return;
	}

	tokenInput.value = savedToken;

	if (await validateToken(savedToken)) {
		setTokenCardVisible(false);
		configCard.hidden = false;
		powerToggle.disabled = false;
		startStatsStream();
		startStatusPolling();
		loadConfig().catch(() => {});
		return;
	}

	sessionStorage.removeItem(TOKEN_KEY);
	setTokenCardVisible(true);
	powerToggle.disabled = true;
}

initAuth();
