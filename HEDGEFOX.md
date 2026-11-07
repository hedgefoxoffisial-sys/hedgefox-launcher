# HedgeFox Launcher

Лаунчер сервера HedgeFox ([hedgefox.ru](https://www.hedgefox.ru)). Форк [Gml-Launcher/Gml.Launcher](https://github.com/Gml-Launcher/Gml.Launcher).

## База

- Тег `hf-base` → `ff73afa`, upstream `master` на 7 ноября 2026.
- Сабмодули `src/Gml.Client` и `src/GamerVII.Notification.Avalonia`: клонировать с `--recursive`.

## Что меняем

Отличия от upstream собраны в нескольких файлах, чтобы обновления вливались без конфликтов.

| Файл | Что там |
|---|---|
| `branding/tokens.json` | цвета бренда HedgeFox, копия токенов сайта |
| `branding/logo.svg`, `branding/background.jpg`, `branding/hedgefox.png` | исходники картинок |
| `tools/brand.py` | `tokens.json` → `Colors.axaml`, проверка контраста и следов чужого бренда |
| `tools/package_release.py` | упаковка сборок в ассеты релиза и `SHA256SUMS.txt`, версия из тега |
| `tools/macos/` | `.app` и `.dmg` для macOS |
| `tools/av/`, `docs/av-submissions.md` | заявки антивирусам о ложных срабатываниях |
| `tools/tests/` | тесты инструментов (`unittest`) |
| `.github/workflows/hedgefox-release.yml` | релиз по тегу `v*` под Windows, macOS и Linux |
| `src/Gml.Launcher/Assets/Styles/Colors.axaml` | генерируется `tools/brand.py`, руками не правим |
| `src/Gml.Launcher/Assets/Resources/ResourceKeysDictionary.cs` | адрес API и папка данных `.hedgefox` |
| `src/Gml.Launcher/Assets/Resources/Resources.ru.resx` | русские тексты |
| `src/Gml.Launcher/Assets/Images/` | логотип, иконка, фон |
| `src/Gml.Launcher/Views/MainWindow.axaml`, `Views/SplashScreen/SplashScreen.axaml`, `Views/Components/ProgressBar.axaml` | заголовки окон и мелкие правки вида |
| `src/Gml.Launcher/Gml.Launcher.csproj` | `Product`, `Company`, `Description`; `AssemblyName` не меняем |

В форке выключены upstream-ные `ci.yml` (Build All) и `dependabot-auto-merge.yml`.

## Как вливать upstream

1. `git tag backup/pre-merge-<дата> && git push origin backup/pre-merge-<дата>`.
2. `git fetch upstream && git merge upstream/master`, затем `git submodule update --init --recursive`.
3. `python3 -m unittest discover -s tools/tests`.
4. Запустить `hedgefox-release.yml` вручную (`workflow_dispatch`) и проверить сборки.

## Решения по исходникам

- Сборка на `hf-base` падала с NU1605: сабмодуль `Gml.Client` (`v2025.3.3-10-g5761973`) требует Avalonia ≥ 11.3.22 и Sentry ≥ 5.16.3, а `Gml.Launcher.csproj` держал 11.3.3 и 5.13.0. Подняли Avalonia до 11.3.22 и Sentry до 5.16.3. `Avalonia.ReactiveUI` — 11.3.9: это последняя версия пакета.
- Совместимость с бэкендом GML v2026.2 — дописывается после проверки.

## Лицензия

Лаунчер основан на GML (Recloud Technologies, MIT). Файл `LICENSE` — upstream-ный, его не меняем.
