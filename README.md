# TcpProducer

Симулятор TCP-устройств (пломб): автоматические сессии, телеметрия и обработка команд сервера.

## Конфигурация

| Источник | Назначение |
|----------|------------|
| `.env` | TCP: хост, порт, `AppendNewline`, `ADMIN_API_TOKEN` |
| `appsettings.json` | Протокол, телеметрия, storage, интервалы |
| `deviceserials.txt` | Серийные номера устройств |

Скопируйте `.env.example` в `.env` и задайте параметры подключения:

```env
Tcp__Host=127.0.0.1
Tcp__Port=3255
Tcp__AppendNewline=false
```

## Запуск

```bash
dotnet run
```

## Деплой

```bash
cd /var/www/TcpProducer
git pull
dotnet publish -c Release -o /var/www/TcpProducer/publish
cp /var/www/TcpProducer/.env /var/www/TcpProducer/publish/.env
systemctl restart tcpproducer
```

## Панель управления

Веб-клиент: **https://антон.su/tcp/**

Первый запуск панели:

```bash
cd /var/www/TcpProducer
dotnet publish admin/TcpProducer.Admin -c Release -o /var/www/TcpProducer/admin/publish
cp deploy/tcpproducer-admin.service /etc/systemd/system/
# Добавить location /tcp/ в /etc/nginx/sites-available/logexplain
# (фрагмент: deploy/nginx-logexplain-tcpproducer.conf)
systemctl daemon-reload
systemctl enable --now tcpproducer-admin
nginx -t && systemctl reload nginx
```

SSL-сертификат Let's Encrypt для домена уже настроен (`certbot certificates`).
Продление: `certbot renew --dry-run`

В `.env` задайте `ADMIN_API_TOKEN` — его нужно ввести в панели при первом открытии.

Обновление панели после изменений:

```bash
dotnet publish admin/TcpProducer.Admin -c Release -o /var/www/TcpProducer/admin/publish
systemctl restart tcpproducer-admin
```


## Смотреть логи онлайн
```bash
journalctl -u tcpproducer -f
```

## Остановка сервиса
```bash
systemctl stop  tcpproducer
```