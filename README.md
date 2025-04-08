# TrayPenguinDPI - Обход DPI

![Иконка TrayPenguinDPI](https://github.com/zhivem/TrayPenguinDPI/blob/master/penguin_icon.ico)

TrayPenguinDPI - это приложение для Windows написаное на `C#`, позволяющее обходить системы Deep Packet Inspection (DPI). Работает из системного трея с поддержкой темной/светлой темы.

## Возможности

- 🚀 Несколько стратегий обхода DPI
- 🖥️ Удобное управление из трея
- ⚙️ Настройка через INI-файлы
- 🔄 Автообновление черных списков
- 🌙 Поддержка темной/светлой темы
- 🌍 Русский/английский интерфейс
- 🔧 Создание и управление службой Windows
- 🔔 Система уведомлений
- 🔄 Проверка обновлений

## Требования

- **Windows 10/11**
- **.NET 9 Runtime** ([скачать](https://dotnet.microsoft.com/ru-ru/download))

## Установка

1. Скачайте последнюю версию в разделе [Releases](https://github.com/zhivem/TrayPenguinDPI/releases)
2. Распакуйте архив
3. Запустите `TrayPenguinDPI.exe` (требуются права администратора)

## Использование
![image](https://github.com/user-attachments/assets/a1b6791b-c67f-44e3-826a-039540c187b3)
![image](https://github.com/user-attachments/assets/99f23d6d-b10b-4ffc-a96a-d7153755a485)

После установки иконка появится в системном трее. Доступные действия:

- Запуск/остановка обхода DPI
- Выбор стратегий
- Настройки программы
- Обновление черных списков
- Управление DNS

## Настройка стратегий

Стратегии настраиваются через INI-файлы в папке `Program/Strateg/`. Пример:

```ini
[Название стратегии]
executable = {ZAPRET}\winws.exe
args = 
--wf-tcp=80,443;
--wf-udp=443,50000-50099;
--filter-tcp=80;
--dpi-desync=fake,fakedsplit;
--dpi-desync-autottl=2;
--dpi-desync-fooling=md5sig;
--hostlist-auto={BLACKLIST}\autohostlist.txt;
--new;
--filter-tcp=443;
--hostlist={BLACKLIST}\list-general.txt;
--dpi-desync=fake,multidisorder;
--dpi-desync-split-pos=1,midsld;
--dpi-desync-repeats=11;
--dpi-desync-fooling=md5sig;
--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com;
--new;
--filter-tcp=443;
--dpi-desync=fake,multidisorder;
--dpi-desync-split-pos=midsld;
--dpi-desync-repeats=6;
--dpi-desync-fooling=badseq,md5sig;
--hostlist-auto={BLACKLIST}\autohostlist.txt;
--new;
--filter-udp=443;
--hostlist={BLACKLIST}\list-general.txt;
--dpi-desync=fake;
--dpi-desync-repeats=11;
--dpi-desync-fake-quic={ZAPRET}\quic_initial_www_google_com.bin;
--new;
--filter-udp=443;
--dpi-desync=fake;
--dpi-desync-repeats=11;
--hostlist={BLACKLIST}\autohostlist.txt;
--new;
--filter-udp=50000-50099;
--filter-l7=discord,stun;
--dpi-desync=fake;
```

## Используемые технологии

- **[NotifyIconEx](https://github.com/lemutec/NotifyIconEx)** — расширенный компонент для работы с иконкой в трее  
- **[AdonisUI](https://github.com/benruehl/adonis-ui)** — современный UI-фреймворк для WPF  
- **[Zapret](https://github.com/bol-van/zapret)** — ядро системы обхода DPI  

## Лицензия

MIT License. Подробнее в файле [LICENSE](https://raw.githubusercontent.com/zhivem/TrayPenguinDPI/refs/heads/master/LICENSE.txt).
