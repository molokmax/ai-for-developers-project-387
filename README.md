# Календарь звонков (продолжение)


[![hexlet-check](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/hexlet-check.yml/badge.svg)](https://github.com/molokmax/ai-for-developers-project-387/actions)

Интегрируйте работу агентов в GitHub проект

Учебный проект Хекслета: https://ru.hexlet.io/programs/ai-for-developers
Как это должно работать: https://files.hexlet.app/a/2ipc5m

## Стек

- Разное

## Установка

<!-- Опишите установку: клонирование, зависимости, переменные окружения -->

```bash
git clone https://github.com/molokmax/ai-for-developers-project-387.git
cd ai-for-developers-project-387
```

## Использование

Задеплоенное приложение доступно по адресу http://31.59.126.45:8080/

<!-- Добавьте примеры запуска и запись asciinema — именно это смотрит работодатель -->

## Воркфлоу

Все прогоны собраны во вкладке [Actions](https://github.com/molokmax/ai-for-developers-project-387/actions). В колонке «Прогоны» прямая ссылка на историю запусков конкретного воркфлоу. Модель указана только для агентских воркфлоу OpenCode, остальные запускаются без LLM.

| Воркфлоу | Событие | Модель | Назначение | Прогоны |
| --- | --- | --- | --- | --- |
| CI | push в любую ветку, pull_request | нет | Бэкенд: build с warnings-as-errors (ловит стиль), `dotnet format`, тесты. Фронтенд: oxlint, tsc + vite, vitest, drift-check сгенерированного SDK. Контракт: tsp lint/format, генерация OpenAPI, drift-check закоммиченного артефакта | [ci.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/ci.yml) |
| Docker | push в `main`, теги `v*`, публикация релиза, pull_request | нет | Сборка образа API + SPA. На PR только build без пуша, на push в `main` и теги публикация в `ghcr.io/molokmax/ai-for-developers-project-387` (теги `main`, `sha-*`, семвер) | [docker.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/docker.yml) |
| hexlet-check | push в любую ветку, теги | нет | Служебная проверка проекта на стороне Хекслета | [hexlet-check.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/hexlet-check.yml) |
| OpenCode PR Review | pull_request (opened, synchronize, reopened, ready_for_review) | `opencode/deepseek-v4-flash` | Ревью PR: баги, рискованные изменения, читаемость, поддерживаемость, комментарии. Пропускается, если автор PR или последний push сделан ботом | [opencode-review.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/opencode-review.yml) |
| Issue Triage | issues (opened) | `opencode/deepseek-v4-flash` | Разбор нового issue: ссылки на документацию, подсказки по обработке ошибок в примерах. Запускается для не-ботов, чей аккаунт старше 30 дней | [opencode-triage.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/opencode-triage.yml) |
| Weekly Code Audit | schedule (cron `0 9 * * 1`, понедельник 9:00 UTC), workflow_dispatch | `opencode/deepseek-v4-flash` (переопределяется входом `model` при ручном запуске) | Скан кодовой базы на TODO/FIXME/HACK, группировка по файлам и важности, создание issue со сводкой. Ручной запуск принимает свои `prompt` и `model` | [opencode-weekly-audit.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/opencode-weekly-audit.yml) |
| opencode | issue_comment (created), pull_request_review_comment (created) | `opencode/deepseek-v4-flash` | Агент по требованию: срабатывает на комментарий владельца репозитория с командой `/oc` или `/opencode` | [opencode.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/opencode.yml) |
| Release Please | push в `main` | нет | Создаёт или обновляет release PR, из мержа которого release-please строит версию, тег и changelog | [release-please.yml](https://github.com/molokmax/ai-for-developers-project-387/actions/workflows/release-please.yml) |

---

<details>
<summary>Автоматические тесты Хекслета</summary>

Тесты запускаются на каждый коммит. За запуск отвечает файл `.github/workflows/hexlet-check.yml` — не удаляйте и не переименовывайте ни его, ни репозиторий.

</details>

## О Хекслете

[Хекслет](https://ru.hexlet.io/) — школа программирования: авторские программы обучения с практикой, поддержкой наставников и реальными проектами, которые остаются в резюме. Этот репозиторий — один из таких проектов.
