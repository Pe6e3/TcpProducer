# TcpProducer

Симулятор TCP-устройств (пломб): автоматические сессии, телеметрия и обработка команд сервера.

## Конфигурация

| Источник | Назначение |
|----------|------------|
| `.env` | TCP: хост, порт, `AppendNewline` |
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


## Смотреть логи онлайн
```bash
journalctl -u tcpproducer -f
```

## Остановка сервиса
```bash
systemctl stop  tcpproducer
```