# OmniCart.NET 🛒

Ультимативное решение для e-commerce: Telegram-бот для покупателей и мощная Blazor-админка для владельцев бизнеса.

## 🚀 Основные возможности

- **Telegram Бот**: Полноценный цикл покупки от каталога до оформления заказа.
- **Blazor Admin Panel**: Современная панель управления на MudBlazor для управления товарами, заказами и просмотра аналитики.
- **EF Core + PostgreSQL**: Надежное хранение данных.
- **Docker Ready**: Вся система разворачивается одной командой.

## 🛠 Стек технологий

- **Backend**: .NET 10
- **Database**: PostgreSQL + Entity Framework Core
- **Frontend**: Blazor Server + MudBlazor
- **Bot API**: Telegram.Bot
- **DevOps**: Docker, Docker Compose

## 🏗 Архитектура

Проект разбит на логические слои:
- **Domain**: Ядро системы, C# сущности (User, Product, Order).
- **Infrastructure**: Реализация БД, логика Telegram-бота.
- **TelegramBot**: Воркер, обеспечивающий работу бота.
- **OmniCart.BlazorAdmin**: Веб-интерфейс администратора.

## 🚦 Быстрый запуск

### Предварительные условия
1. Установлен Docker и Docker Compose.
2. Получен токен для Telegram-бота у [@BotFather](https://t.me/BotFather).

### Запуск через Docker
1. Клонируйте репозиторий.
2. Создайте переменные окружения или передайте их при запуске:
   ```bash
   export POSTGRES_PASSWORD=your_secure_password
   export TELEGRAM_BOT_TOKEN=your_bot_token
   ```
3. Запустите проект:
   ```bash
   docker-compose up -d
   ```
4. Админка будет доступна по адресу: `http://localhost:8080`

## 📊 Дашборд
Админ-панель включает в себя интерактивный дашборд с:
- Общей выручкой.
- Количеством заказов за сегодня.
- Количеством активных клиентов.
- Графиком статистики заказов за последнюю неделю.
