# ИС «СервисДеск» — управление заявками в сервисный центр

Учебный проект, группа 23П-1, Баймуратов Д. А.

Система автоматизирует работу сервисного центра от приёма устройства до выдачи:
приём заявок, управление ремонтом, склад запчастей, расчёты и документы, личный кабинет клиента.

| Компонент | Технологии | Каталог |
|---|---|---|
| Сервер приложений (REST API, SignalR, PDF) | ASP.NET Core 8, EF Core 8 + Pomelo MySQL, JWT, QuestPDF, Swagger | `src/ServiceDesk.Api` |
| Общие DTO и константы | .NET 8 | `src/ServiceDesk.Contracts` |
| Рабочее место сотрудника (приёмщик, мастер, кладовщик) | WPF, .NET 8 | `src/ServiceDesk.Desktop` |
| Веб-кабинет клиента | Vue 3, Vite, Vue Router, @microsoft/signalr | `web` |
| Модульные тесты | xUnit, EF Core InMemory | `tests/ServiceDesk.Tests` |
| База данных | MySQL 8.0 (18 таблиц, 2 представления) | `db` |
| Документация | ТЗ, структура БД, модульная схема, протокол тестирования, руководство, отчёт | `docs` |

## Развёртывание серверной части в Docker

Требуется Docker Engine 24+ и Docker Compose v2.

```bash
cp .env.example .env        # задайте пароли и JWT_KEY (не короче 32 символов)
docker compose up -d --build
```

| Служба | Адрес | Назначение |
|---|---|---|
| web | http://localhost:8088 | веб-кабинет клиента (nginx), прокси `/api`, `/hubs`, `/swagger` |
| api | http://localhost:5080 | REST API; Swagger UI — http://localhost:5080/swagger |
| db | localhost:3307 | MySQL 8.0; при первом запуске выполняются скрипты `db/*.sql` |
| backup | — | ежедневный `mysqldump` в каталог `backups/`, хранение 30 дней |

Данные СУБД хранятся в томе `db_data`, контейнеры перезапускаются автоматически (`restart: unless-stopped`).
Полный сброс демо-данных: `docker compose down -v && docker compose up -d`.

## Запуск без Docker (разработка)

1. MySQL 8.0: создайте пользователя с правами на БД `servicedesk` и выполните `db/01_schema.sql`, затем `db/02_seed.sql`.
2. Создайте `src/ServiceDesk.Api/appsettings.Local.json` (файл не попадает в репозиторий):
   ```json
   {
     "ConnectionStrings": { "Default": "Server=localhost;Port=3306;Database=servicedesk;User=servicedesk;Password=...;CharSet=utf8mb4" },
     "Jwt": { "Key": "случайная строка не короче 32 символов" }
   }
   ```
3. Сервер: `dotnet run --project src/ServiceDesk.Api` → http://localhost:5080/swagger
4. Веб-кабинет: `cd web && npm install && npm run dev` → http://localhost:5173
5. Настольное приложение: `dotnet run --project src/ServiceDesk.Desktop`
   (адрес сервера задаётся в `appsettings.json` рядом с exe, параметр `ApiUrl`).

Скрипт `tools/reseed.sh` пересоздаёт локальную БД с демо-данными.

## Демо-учётные записи

| Роль | Логин | Пароль | Приложение |
|---|---|---|---|
| Приёмщик | +7 900 000-00-01 или priem@servicedesk.ru | Priem2026 | настольное |
| Мастер | +7 900 000-00-03 (Орлов С. В.) | Master2026 | настольное |
| Кладовщик | sklad@servicedesk.ru | Sklad2026 | настольное |
| Клиент | +7 900 123-45-67 (Иванов К.) | Client2026 | веб-кабинет |

## Тестирование

```bash
dotnet test tests/ServiceDesk.Tests          # 62 модульных теста бизнес-логики
python tools/e2e_api.py                       # 58 функциональных тест-кейсов через REST API (сервер на :5080)
```

После `e2e_api.py` демо-данные изменяются — пересоздайте БД (`tools/reseed.sh`).

## Основные методы API

Полное описание — Swagger UI (`/swagger`). Ошибки возвращаются в формате `{ "message": "..." }`
с кодами 400 (валидация), 401 (нет входа), 403 (нет прав), 404, 409 (нарушение бизнес-правил), 423 (учётная запись заблокирована).
Уведомления в реальном времени: хаб SignalR `/hubs/orders`, событие `OrderUpdated`.
