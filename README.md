# PC Monitor (ESP32-C3 + ILI9341 + LibreHardwareMonitor)

Projekt ma 2 casti:

- `PcMonitorHost/`: Windows aplikace (WinForms), cte telemetrii a posila ji pres COM do ESP32.
- `Pc monitor.ino`: firmware pro ESP32-C3, prijima packet `DATA;...` a kresli dashboard na ILI9341.

## 1) Co to umi

- CPU load + CPU teplota
- GPU load + GPU teplota
- RAM usage
- sit `download/upload`
- vyuziti disku
- odhad spotreby PC
- sleep displeje pri vypnutem monitoru
- tray rezim, nastaveni jazyka, startup s Windows
- debug tlacitka `Test probuzeni` a `Test alert`
- overload alert pri vysokem CPU + GPU load

## 2) Host aplikace (Windows)

### Pozadavky

- Windows
- .NET 8 SDK nebo hotovy EXE build
- pro presnejsi teploty je vhodne spoustet aplikaci jako Administrator

### Spusteni ze zdrojaku

```powershell
dotnet run --project PcMonitorHost
```

### Build EXE

```powershell
dotnet publish "PcMonitorHost\\PcMonitorHost.csproj" -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o ".\\PcMonitorHost_EXE"
```

Vysledny soubor:

- `PcMonitorHost_EXE\\PcMonitorHost.exe`

### Co spousti aplikaci

Entry point je:

- `PcMonitorHost/Program.cs`

Hlavni okno:

- `PcMonitorHost/MainForm.cs`

### Pouziti

1. Vyber COM port ESP32.
2. Nech baud `115200`, pokud jsi ho nemenil i ve firmware.
3. Klikni `Pripojit`.
4. V nastaveni muzes zapnout:
   - start s Windows
   - vychozi COM
   - jazyk
   - sleep ESP displeje pri vypnutem monitoru
   - auto reconnect
   - low power pri neaktivite

### Debug tlacitka

- `Test probuzeni`: nasimuluje sleep/wake cyklus displeje
- `Test alert`: posle jednorazovy trigger overload alertu do ESP

## 3) ESP32-C3 firmware

### Pozadavky (Arduino IDE / arduino-cli)

- board package: `ESP32 by Espressif`
- board: `esp32:esp32:nologo_esp32c3_super_mini`
- knihovna:
  - `LovyanGFX`

Poznamka:

- stare instrukce s `Adafruit_GFX` a `Adafruit_ILI9341` uz pro tenhle firmware neplati

### Zapojeni ILI9341 -> ESP32-C3 Super Mini

Aktualni mapovani odpovida `Pc monitor.ino`.

| ILI9341 pin | ESP32-C3 Super Mini pin |
| --- | --- |
| VCC | 3V3 |
| GND | GND |
| CS | GPIO7 |
| RESET / RST | GPIO1 |
| DC / RS / A0 | GPIO3 |
| SDI / MOSI | GPIO6 |
| SCK / CLK | GPIO4 |
| SDO / MISO | GPIO5 |
| LED / BL | GPIO10 |

Poznamky:

- `LED / BL` je ted rizeny z `GPIO10`, takze pri sleep se umi fyzicky zhasnout podsvit.
- Pokud si nejsi jisty modulem, dej mezi `GPIO10` a `LED/BL` odpor `100-220 ohm`.
- Pokud modul nema `SDO/MISO`, muze zustat odpojeny.
- Touch piny (`T_CS`, `T_CLK`, `T_DIN`, `T_DO`, `T_IRQ`) se nepouzivaji.

### Nahrani firmware

Arduino IDE:

1. Otevri `Pc monitor.ino`
2. Vyber board `NoLogo ESP32C3 Super Mini`
3. Vyber spravny COM port
4. Nahraj sketch

arduino-cli:

```powershell
arduino-cli compile --fqbn "esp32:esp32:nologo_esp32c3_super_mini" "c:\Users\Lukasbertone\Documents\Pc monitor"
arduino-cli upload -p COM6 --fqbn "esp32:esp32:nologo_esp32c3_super_mini" "c:\Users\Lukasbertone\Documents\Pc monitor"
```

## 4) Serial packet format

Host posila radky ve formatu:

```text
DATA;cpu=24.7;ct=57.3;co=8;th=16;tl=10,7,12,9,35,42,28,19;rp=46.2;ru=7.4;rt=31.9;gu=31.8;gt=62.1;nd=12.8;nu=2.4;ds=C:61,D:24;pw=262.3;id=0;oa=0;crc=ABCD
```

Klice:

- `cpu` CPU load v procentech
- `ct` CPU teplota v C
- `co` pocet fyzickych jader CPU
- `th` pocet logickych vlaken CPU
- `tl` vytizeni vlaken jako CSV
- `rp` RAM usage v procentech
- `ru` pouzita RAM v GB
- `rt` celkova RAM v GB
- `gu` GPU load v procentech
- `gt` GPU teplota v C
- `nd` sit download v MB/s
- `nu` sit upload v MB/s
- `ds` usage disku, napr. `C:61,D:24`
- `pw` odhad celkove spotreby ve W
- `id` sleep request pro displej (`1` = sleep, `0` = bezet)
- `oa` debug overload alert trigger (`1` = jednorazove spustit alert)
- `crc` CRC16 packetu

ESP po prijeti validniho packetu odpovi:

```text
ACK
```

## 5) Chovani displeje

- pri beznem provozu se prekresluji hlavne zmenene casti
- kdyz dlouho neprijdou data, dashboard prejde do stale stavu
- pri vypnutem monitoru muze host poslat `id=1` a displej se uspi
- pri probuzeni se podsvit plynule vraci zpet
- overload alert se muze spustit automaticky pri vysokem CPU + GPU load nebo rucne pres `Test alert`

## 6) Poznamky

- pokud CPU teplota zustava `N/A`, spust host aplikaci jako Administrator
- `pw` je jen odhad z dostupnych sensoru, neni to presne mereni ze zasuvky
- pokud se COM port odpoji, aplikace umi auto reconnect
- aplikace se pri minimalizaci umi schovat do tray

## 7) Dulezite soubory

- `Pc monitor.ino` - firmware pro ESP32
- `PcMonitorHost/Program.cs` - start aplikace
- `PcMonitorHost/MainForm.cs` - hlavni okno, COM, nastaveni, debug tlacitka
- `PcMonitorHost/HardwareMonitorService.cs` - cteni telemetrie z PC
- `PcMonitorHost/SerialPacketFormatter.cs` - skladani packetu `DATA;...`
- `Run-PcMonitorHost-Admin.ps1` - spusteni host aplikace jako Administrator
