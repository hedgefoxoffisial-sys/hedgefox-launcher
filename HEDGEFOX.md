# HedgeFox Launcher

Лаунчер сервера HedgeFox ([hedgefox.ru](https://www.hedgefox.ru)). Форк [Gml-Launcher/Gml.Launcher](https://github.com/Gml-Launcher/Gml.Launcher).

## База

- Тег `hf-base` → `ff73afa`, upstream `master` на 7 ноября 2026.
- Сабмодули `src/Gml.Client` и `src/GamerVII.Notification.Avalonia`: клонировать с `--recursive`. Их не правим.

## Что меняем

Отличия от upstream собраны в своих файлах, чтобы обновления вливались без конфликтов.

Свои файлы (upstream их не знает):

| Файл | Что там |
|---|---|
| `branding/tokens.json` | 12 цветов и шрифты бренда, копия `web/site/brand/tokens.json` (ключи не добавляем) |
| `branding/site-derived.json` | производные цвета сайта из `web/site/src/styles/global.css :root` (`brand.py sync-site`) |
| `branding/background.jpg`, `branding/hedgefox.png` | исходники фона (сцена Пелены 1920×1080) и логотипа |
| `tools/brand.py` | `tokens.json` + `site-derived.json` → `Colors.axaml`; проверка контраста и следов чужого бренда |
| `tools/fonts.py` | статические TTF из WOFF2 сайта (fontTools), см. «Шрифты» |
| `tools/package_release.py` | упаковка сборок в ассеты релиза и `SHA256SUMS.txt`, версия из тега |
| `tools/macos/` | `.app` и `.dmg` для macOS; в `Info.plist` есть доступ к микрофону для голосового чата |
| `tools/tests/` | тесты инструментов (`unittest`): цвета, следы бренда, шрифты, хуки форка |
| `tools/shots/` | снимки экранов без окна (Avalonia.Headless), см. «Снимки экранов» |
| `tests/HedgeFox.Launcher.Tests/` | юнит-тесты логики `Core/HedgeFox` (xunit) |
| `.github/workflows/hedgefox-release.yml` | релиз по тегу `v*` под Windows, macOS и Linux |
| `src/Gml.Launcher/Assets/HedgeFox/` | шрифты Tiny5, Unbounded, Rubik с OFL; текстура тумана |
| `src/Gml.Launcher/Assets/Styles/HedgeFoxTheme.axaml` | тема в стиле сайта: кнопки, рамки, текст, поля, полосы прокрутки |
| `src/Gml.Launcher/Core/HedgeFox/` | ссылки, тексты, статус, новости, скин, карточки профилей, совет по железу |
| `src/Gml.Launcher/Views/HedgeFox/` | свои контролы и полоса заголовка окна |
| `src/Gml.Launcher/Views/MainWindow.axaml(.cs)`, `Views/SplashScreen/SplashScreen.axaml` | окно и заставка (наши целиком) |

Правки в файлах upstream:

| Файл | Правка |
|---|---|
| `App.axaml` | `RequestedThemeVariant="Dark"` и последней строкой `HedgeFoxTheme.axaml` |
| `Core/Extensions/ServiceLocator.cs` | одна строка: `HedgeFoxServices.Register(storageService)` (наши сервисы, русский по умолчанию) |
| `Assets/Styles/Colors.axaml` | генерируется `tools/brand.py`, руками не правим |
| `Assets/Resources/ResourceKeysDictionary.cs` | адрес API `https://api.hedgefox.ru` и папка данных `.hedgefox` |
| `Assets/Resources/Resources.ru.resx` | строка про VPN |
| `Assets/Images/` | логотип, иконка, фон |
| `Views/Components/LogoComponent.axaml` | логотип и подпись HedgeFox |
| `Views/Components/ProfileUserControl.axaml.cs` | без чужого скина и ника по умолчанию |
| `Views/Pages/OverviewPageView.axaml`, `ProfilePageView.axaml` | ссылки из `HfLinks` вместо чужих |
| `Gml.Launcher.parcel` | имя и идентификатор HedgeFox |
| `Gml.Launcher.csproj` | `Product`, `Company`, `Description`, версии пакетов; `AssemblyName` не меняем |

В форке выключены upstream-ные `ci.yml` (Build All) и `dependabot-auto-merge.yml`.

## Тема

`HedgeFoxTheme.axaml` подключается последней и перекрывает Fluent и стоковые компоненты: углы прямые (`CornerRadius` 0
везде), акцент оранжевый. Список классов и контролов — в шапке файла. Пиксельные картинки — с классом `hf-pixel`
(без размытия). Цвета берём только из ключей `Hf…Brush` / `Hf…Color` в `Colors.axaml`.

## Шрифты

Avalonia не читает WOFF2 и вариативные шрифты, поэтому в `Assets/HedgeFox/Fonts` лежат статические TTF, нарезанные из
подмножеств сайта (латиница, кириллица, стрелки): Tiny5 400, Unbounded 700/800, Rubik 400/500/600/700. Пересобрать:

```sh
uvx --from fonttools --with brotli python tools/fonts.py ~/hedgefox-work/web/site/public/fonts
```

Глифа ✓ нет ни в одном шрифте, у Rubik нет → и ▶: вместо них пиксельные значки `hf:PixelIcon`.

## Проверка

```sh
python3 -m unittest discover -s tools/tests                                       # как в CI
uvx --from fonttools --with brotli python -m unittest discover -s tools/tests     # плюс проверка шрифтов fontTools
dotnet test tests/HedgeFox.Launcher.Tests                                          # логика Core/HedgeFox
```

## Снимки экранов

`tools/shots` рисует окна и страницы в PNG 1180×720 без окна на экране: настоящий `App` и тема, сервисы заменены
фейками с постоянными данными (3 профиля, 4 новости, онлайн 7 из 45, Пелена Y 80 → 96, скин из `Fixtures/skin.png`).
Сеть и `~/.hedgefox` не трогает. Проект не входит в `Gml.Launcher.sln` и в релиз.

```sh
dotnet run --project tools/shots -c Release -- /путь/к/папке/снимков
```

Снимки: `theme-gallery` (все элементы темы и их состояния), `splash`, `splash-ready`, `login`, `overview`, `settings`,
`profile`, `mods`, `data-preview` (данные сервисов в окне) и варианты `-macos` с местом под «светофоры».
Каждый снимок ждёт условия (страница открыта, данные пришли, разметка устоялась), а не паузы. Переходы между страницами
в харнессе выключены, чтобы снимок не попал на середину затухания.

Запускать собранный лаунчер вручную — только с `-skip-update` и отдельным `HOME`, чтобы не задеть свою сессию в
`~/.hedgefox`: `HOME=/tmp/hf-home ./Gml.Launcher -skip-update`.

## Как вливать upstream

1. `git tag backup/pre-merge-<дата> && git push origin backup/pre-merge-<дата>`.
2. `git fetch upstream && git merge upstream/master`, затем `git submodule update --init --recursive`.
3. Тесты из «Проверка» и снимки из «Снимки экранов».
4. Запустить `hedgefox-release.yml` вручную (`workflow_dispatch`) и проверить сборки.

## Решения по исходникам

- Сборка на `hf-base` падала с NU1605: сабмодуль `Gml.Client` (`v2025.3.3-10-g5761973`) требует Avalonia ≥ 11.3.22 и Sentry ≥ 5.16.3, а `Gml.Launcher.csproj` держал 11.3.3 и 5.13.0. Подняли Avalonia до 11.3.22 и Sentry до 5.16.3. `Avalonia.ReactiveUI` — 11.3.9: это последняя версия пакета.
- Совместимость с бэкендом GML v2026.2 — дописывается после проверки.

## Лицензия

Лаунчер основан на GML (Recloud Technologies, MIT). Файл `LICENSE` — upstream-ный, его не меняем. Шрифты — под SIL OFL, тексты лицензий лежат
рядом с ними.
