# TrayPenguinDPI - DPI Bypass

![TrayPenguinDPI Icon](https://github.com/zhivem/TrayPenguinDPI/blob/master/penguin_icon.ico)

TrayPenguinDPI is a Windows application written in `C#` designed to bypass Deep Packet Inspection (DPI) systems. It operates from the system tray with support for both dark and light themes.

> [!NOTE]
> There is also a `Python` version called [DPI Penguin](https://github.com/zhivem/DPI-Penguin) available on GitHub. Both versions aim to provide a convenient and effective way to bypass censorship and DPI filtering.

## Features

- 🚀 Multiple DPI bypass strategies
- 🖥️ Convenient tray management
- ⚙️ Configuration via INI files
- 🔄 Automatic blacklist updates
- 🌙 Dark/light theme support
- 🌍 Russian/English interface
- 🔧 Windows service creation and management
- 🔔 Notification system
- 🔄 Update checks
- 🖱️ Double - clicking on the tray icon starts and stops Zapret

## Requirements

- **Windows 10/11**
- **.NET 9 Runtime** ([download](https://dotnet.microsoft.com/en-us/download))

## Installation

1. Download the latest version from the [Releases](https://github.com/zhivem/TrayPenguinDPI/releases) section
2. Extract the archive
3. Run `TrayPenguinDPI.exe` (requires administrator privileges)

## Usage
![image](https://github.com/user-attachments/assets/44c8f10a-fcc3-4eeb-b698-6fb1a7762382)
![image](https://github.com/user-attachments/assets/5ec81eae-3979-4e2d-986e-31b9aaa230bf)

After installation, the icon will appear in the system tray. Available actions:

- Start/stop DPI bypass
- Strategy selection
- Program settings
- Blacklist updates
- DNS management

## Strategy Configuration

Strategies are configured via INI files in the `Program/Strateg/` folder. Example:

```ini
[Strategy Name]
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

## Technologies Used

- **[NotifyIconEx](https://github.com/lemutec/NotifyIconEx)** —  Enhanced system tray icon component 
- **[AdonisUI](https://github.com/benruehl/adonis-ui)** —  Modern UI framework for WPF
- **[Zapret](https://github.com/bol-van/zapret)** — Core DPI bypass system

## License

MIT License. For more details, see the [LICENSE](https://raw.githubusercontent.com/zhivem/TrayPenguinDPI/refs/heads/master/LICENSE.txt).
