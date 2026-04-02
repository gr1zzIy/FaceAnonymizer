# FaceAnonymizer

Система автоматичної анонімізації облич із використанням глибинного навчання.

## Про проєкт

`FaceAnonymizer` — це Web API для:
- виявлення облич на зображеннях;
- анонімізації облич (розмиття, пікселізація, заливка кольором);
- пакетної обробки наборів зображень;
- оцінювання якості анонімізації та формування звітів (CSV/PDF).

## Архітектура рішення

Проєкт побудовано за шаровим підходом:
- `FaceAnonymizer.Api` — HTTP API, Swagger UI, валідація запитів, робота з файлами/результатами;
- `FaceAnonymizer.Application` — прикладна логіка детекції, анонімізації та batch-процесингу;
- `FaceAnonymizer.Core` — доменні моделі та абстракції;
- `FaceAnonymizer.Infrastructure` — реалізації детекторів, анонімізаторів, ONNX/OpenCV, генерація звітів.

## Основні можливості

- Підтримка детекторів облич: `Haar Cascade` та `YuNet (ONNX)`.
- Підтримка anti-spoof класифікації (`MiniFASNetV2.onnx`).
- Методи анонімізації:
  - `GaussianBlur`
  - `Pixelation`
  - `SolidColor`
- Batch-режим із формуванням артефактів та звітів.

## Технології

- `.NET 10` (ASP.NET Core Web API)
- `OpenCvSharp`
- `ONNX Runtime`
- `QuestPDF`
- `ScottPlot`
- `Docker` / `Docker Compose`

## Структура даних і ресурсів

- Моделі детекції: `FaceAnonymizer.Api/assets/`
- Модель anti-spoof: `FaceAnonymizer.Api/Models/MiniFASNetV2.onnx`
- Вхід/вихід batch-обробки: `FaceAnonymizer.Api/storage/`

## Запуск локально

### Передумови

- Встановлений `.NET SDK 10`
- Встановлений Docker (опційно, для контейнерного запуску)

### Команди

```bash
dotnet restore
dotnet build
dotnet run --project FaceAnonymizer.Api/FaceAnonymizer.Api.csproj
```

Після запуску Swagger доступний за адресою:
- `http://localhost:5296/swagger` (типовий порт локального запуску може відрізнятись)

## Запуск через Docker Compose

```bash
docker compose up --build
```

Сервіс буде доступний за адресою:
- `http://localhost:8080/swagger`

### Якщо виникає помилка з Docker socket

Якщо бачите помилку на кшталт:
`failed to connect to the docker API ... /Users/<user>/.docker/run/docker.sock`

Перевірте:
1. Docker Desktop запущений.
2. Контекст Docker коректний:
   ```bash
   docker context ls
   docker context use default
   ```
3. Демон відповідає:
   ```bash
   docker info
   ```

## Основні API endpoints

Базовий маршрут: `api/faces`

- `GET /api/faces/health` — перевірка стану сервісу
- `GET /api/faces/capabilities` — доступні детектори/методи
- `POST /api/faces/detect` — детекція облич
- `POST /api/faces/anonymize` — анонімізація зображення
- `POST /api/faces/batch/upload` — завантаження набору файлів
- `POST /api/faces/batch` — запуск пакетної обробки
- `GET /api/faces/batch/report.csv?runId=...` — CSV-звіт
- `GET /api/faces/batch/report.pdf?runId=...` — PDF-звіт

## Наукова та практична цінність

У межах бакалаврської роботи проєкт демонструє практичне застосування методів комп'ютерного зору та глибинного навчання для захисту приватності у фото- та відеоданих.

## Можливі напрями розвитку

- Додавання нових моделей детекції/сегментації облич
- Підтримка потокового відео (real-time)
- Деплой у хмарну інфраструктуру
- Розширення метрик оцінки якості анонімізації

## Автор

Студент Вінницького Національного Технічного Університету Іщенко Олексій Русланович, бакалаврської кваліфікаційної роботи на тему:
**«СИСТЕМА АВТОМАТИЧНОЇ АНОНІМІЗАЦІЇ ОБЛИЧ ІЗ ВИКОРИСТАННЯМ ГЛИБИННОГО НАВЧАННЯ»**

