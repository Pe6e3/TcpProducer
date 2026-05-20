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

let logsAbortController = null;

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
		const response = await fetch(apiUrl('/api/logs/stream'), {
			headers: { Authorization: `Bearer ${token}` },
			signal: logsAbortController.signal,
		});

		if (response.status === 401) {
			logsOutput.textContent = 'Ошибка авторизации. Проверьте ADMIN_API_TOKEN.';
			return;
		}

		if (!response.ok) {
			logsOutput.textContent = `Ошибка подключения: HTTP ${response.status}`;
			return;
		}

		logsOutput.textContent = '--- логи tcpproducer ---\n';

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
				const line = event
					.split('\n')
					.filter((row) => row.startsWith('data: '))
					.map((row) => row.slice(6))
					.join('\n');

				if (line)
					appendLogLine(line);
			}
		}

		appendLogLine('--- поток завершён ---');
	} catch (err) {
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
	loadStatus(true).catch(() => {});
}
