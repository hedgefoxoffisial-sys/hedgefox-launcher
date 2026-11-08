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
| `tools/macos/` | `.app` и `.dmg` для macOS; в `Info.plist` доступ к микрофону для голосового чата и русский регион пакета |
| `tools/tests/` | тесты инструментов (`unittest`): цвета, следы бренда, шрифты, хуки форка |
| `tools/shots/` | снимки экранов без окна (Avalonia.Headless), см. «Снимки экранов» |
| `tests/HedgeFox.Launcher.Tests/` | юнит-тесты логики `Core/HedgeFox` (xunit) |
| `.github/workflows/hedgefox-release.yml` | релиз по тегу `v*` под Windows, macOS и Linux |
| `src/Gml.Launcher/Assets/HedgeFox/` | шрифты Tiny5, Unbounded, Rubik с OFL; текстура тумана заставки |
| `src/Gml.Launcher/Assets/HedgeFox/Images/Menu/` | спрайты главного меню игры 1:1 (заголовок, вывеска, верёвки, облако под вывеской в 6 кадрах, луна, горы Пелены, две полосы тумана, дирижабль): копия того, что рисует `pack/scripts/gen_branding.py` основного репозитория HedgeFox; здесь не правим |
| `src/Gml.Launcher/Assets/Styles/HedgeFoxTheme.axaml` | тема в стиле сайта: кнопки, рамки, текст, поля, полосы прокрутки |
| `src/Gml.Launcher/Core/HedgeFox/` | ссылки, тексты, статус, новости, скин, карточки профилей, совет по железу, ошибки входа по-русски (`HfAuthGuard`), меню приложения на macOS, тон тостов |
| `src/Gml.Launcher/Views/HedgeFox/` | свои страницы (вход, главный экран, профиль, настройки), заставка, контролы (ночь меню `HfMenuScene`, вывеска с загрузкой `HfSignMeter`, спрайт с кадрами `HfSprite`), полоса заголовка, размер окна под рабочую область (`HfWindowFit`) |
| `src/Gml.Launcher/ViewModels/HedgeFox/` | состояние своих страниц рядом со стоковыми view model: статус и новости, карточки профилей, выход |
| `src/Gml.Launcher/Views/MainWindow.axaml(.cs)`, `Views/SplashScreen/SplashScreen.axaml` | окно и заставка (наши целиком) |

Правки в файлах upstream:

| Файл | Правка |
|---|---|
| `App.axaml` | `RequestedThemeVariant="Dark"` и последней строкой `HedgeFoxTheme.axaml` |
| `Core/Extensions/ServiceLocator.cs` | одна строка: `HedgeFoxServices.Register(storageService, builder)` (наши сервисы; язык всегда русский, сохранённый v0.9 английский или китайский переписывается; на macOS своё меню приложения по-русски вместо «About Avalonia»; вход через `HfAuthGuard`) |
| `Core/Helpers/AppViewLocator.cs` | одна строка первой веткой: `HfViews.Resolve(viewModel)` (наши страницы; «Моды» остаётся стоковой) |
| `Assets/Styles/Colors.axaml` | генерируется `tools/brand.py`, руками не правим |
| `Assets/Resources/ResourceKeysDictionary.cs` | адрес API `https://api.hedgefox.ru` и папка данных `.hedgefox` |
| `Assets/Resources/Resources.ru.resx` | только значения: VPN, нет связи со входом, моды, ошибки запуска — коротко и на «ты» |
| `Assets/Images/` | логотип, иконка, фон |
| `Views/Components/LogoComponent.axaml` | логотип и подпись HedgeFox |
| `Views/Components/ProfileUserControl.axaml.cs` | без чужого скина и ника по умолчанию |
| `Views/Pages/OverviewPageView.axaml`, `ProfilePageView.axaml` | ссылки из `HfLinks` вместо чужих |
| `Gml.Launcher.parcel` | имя и идентификатор HedgeFox |
| `Gml.Launcher.csproj` | `AssemblyTitle` (описание exe в Windows), `Product`, `Company`, `Description`, версии пакетов; `AssemblyName` не меняем |

В форке выключены upstream-ные `ci.yml` (Build All) и `dependabot-auto-merge.yml`.

## Вид

Лаунчер выглядит как главное меню игры: ночь, звёзды (часть мерцает), луна слева, дирижабль справа сверху, горы
Пелены с хабом и два слоя тумана — всё за каждой страницей (`HfMenuScene` в `MainWindow`). На экране одно главное
действие на деревянной вывеске, мало слов и много пустого места:

- **Вход**: заголовок и «Пираты над Пеленой», ник или почта, пароль, вывеска «ВОЙТИ», под ней «Регистрация» и «Забыл
  пароль?»; внизу одна строка статуса («Онлайн 7 из 45», «Сервер спит» или ничего без данных).
- **Главный экран**: заголовок, вывеска «ИГРАТЬ» на верёвках над облаком тумана, под ней три вкладки профиля
  (Обычный / Слабый ПК / Шейдеры, на подходящем этому ПК — «советуем», описание — в подсказке) и строка статуса с
  Пеленой. Игрок в левом углу (→ Профиль), «Настройки» и «Выйти» в правом; внизу «Сайт · Telegram · Помощь» и одна
  последняя новость. Пока игра качается, вывеска светлеет слева направо и показывает процент, под ней — шаг и
  скорость.
- **Настройки**: одна тихая плашка — память, окно игры, папка игры и «Игра»: «Моды» и «Переустановить» (спрашивает).
- **Профиль**: скин во весь рост, ник, «Сменить скин на сайте».

Пиксельные спрайты рисуются в целом масштабе (×2) без размытия; гладко уменьшаются только заголовок и дирижабль.
Движение (туман, звёзды, дирижабль, облако) — один медленный таймер, на паузе, пока окно скрыто или свёрнуто;
`HfMenuScene.Motion = false` оставляет первый кадр.

## Тема

`HedgeFoxTheme.axaml` подключается последней и перекрывает Fluent и стоковые компоненты: углы прямые (`CornerRadius` 0
везде), акцент оранжевый. Тосты сабмодуля уведомлений рисуются ночной плашкой: цвет, который передаёт стоковая view
model, выбирает только полоску слева (ошибка, успех, остальное), английский бейдж «WARNING» не рисуется. Меню поля ввода
(«Вырезать / Копировать / Вставить»), подсказки и меню — в цветах сайта. Список классов и контролов — в шапке файла. Пиксельные картинки — с классом `hf-pixel`
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

Снимки: `theme-gallery` (все элементы темы и их состояния), `theme-menu` (вывеска, вкладки, текстовые кнопки, полоса
новости и строка статуса над ночью меню, с состояниями), `splash`, `splash-ready`, `splash-offline` (сервер входа
не отвечает), затем каждый экран в двух размерах окна — `-1180x720` (по умолчанию) и `-1000x620` (минимум): `login`,
`login-2fa`, `login-nodata` (наш API молчит: строки статуса нет), `overview`, `overview-download` (вывеска с
процентом, под ней шаг и скорость), `overview-asleep` («Сервер спит»), `overview-nodata` (ни статуса, ни новости),
`settings`, `settings-reinstall` (вопрос перед переустановкой), `profile`, `mods`, `login-offline` (сервер входа не
ответил при запуске: строка под «Войти» и тост); `overview-toasts-1180x720` (тосты ошибки, успеха, VPN и
переподключения) и `login-macos-1180x720` с местом под «светофоры». Ночь и облако в снимках стоят на первом кадре.

## Страницы

Стоковые view model (`ViewModels/Pages`) остаются: роутер, вход и 2FA, профили, запуск игры, сохранение настроек.
`AppViewLocator` отдаёт им наши страницы из `Views/HedgeFox` (`HfViews.Resolve`). Страница (`HfPageView`) создаёт
рядом свою модель из `ViewModels/HedgeFox` и кладёт её в `DataContext` элемента `Root`; модель работает, пока страница
на экране (опрос статуса, новости). Страница «Моды» стоковая, её перекрашивает тема; «Моды» и «Переустановить»
открываются из «Настроек» и берут профиль, выбранный на главном экране (стоковая `OverviewPageViewModel` под ними в
стеке роутера). Если upstream меняет
`Views/Pages/*.axaml`, новые привязки переносим в наши страницы руками.
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
