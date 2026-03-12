# PC Monitor (ESP32-C3 + ILI9341 + LibreHardwareMonitor)

Projekt ma 2 casti:

- `PcMonitorHost/`: Windows aplikace (WinForms) cte data z LibreHardwareMonitor a posila je pres COM.
- `Pc monitor.ino`: firmware pro ESP32-C3, prijima data a kresli dashboard na ILI9341 (320x240, landscape).

## 1) Host aplikace (Windows)

### Pozadavky

- .NET SDK 8.0+
- Windows

### Spusteni

```powershell
dotnet run --project PcMonitorHost
```

V okne aplikace:

1. Vyber COM port ESP32.
2. Nastav baud (default `115200`, musi odpovidat `Pc monitor.ino`).
3. Klikni `Connect`.

UI je nastavene na tmavy motiv (cerna/seda), aby bylo prijemnejsi na oci.

## 2) ESP32-C3 firmware

### Pozadavky (Arduino IDE)

- Board package: ESP32 (Espressif)
- Knihovny:
1. `Adafruit GFX Library`
2. `Adafruit ILI9341`

### Zapojeni ILI9341 -> ESP32-C3 Super Mini

Pouzite piny odpovidaji aktualnimu `Pc monitor.ino`.

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
| LED / BL | 3V3 (nebo pres rezistor 100-220R na 3V3) |

Poznamky:

- Pokud modul nema pin `SDO/MISO` zapojeny, nic se nedeje, muze zustat odpojeny.
- Pokud ma tvuj modul navic `T_CS`, `T_CLK`, `T_DIN`, `T_DO`, `T_IRQ` (touch controller), zatim je nezapojujeme.

### Nahrani

1. Otevri `Pc monitor.ino`.
2. Nahraj sketch do ESP32-C3.

Firmware pouziva hardware SPI (`tft.begin(40000000)`), smooth interpolaci hodnot a inkrementalni prekreslovani (jen zmenene casti), aby byl displej rychly a plynuly.

## 3) Format serial dat

Host posila radky ve formatu:

```text
DATA;cpu=24.7;ct=57.3;co=8;th=16;tl=10,7,12,9,35,42,28,19;rp=46.2;ru=7.4;rt=15.9;gu=31.8;gt=62.1;nd=12.8;nu=2.4;ds=C:61,D:24;pw=262.3;id=0
```

Klice:

- `cpu` CPU load v %
- `ct` CPU teplota v C
- `co` pocet fyzickych jader CPU
- `th` pocet logickych vlaken CPU
- `tl` vytizeni logickych vlaken (0-100, CSV)
- `rp` RAM load v %
- `ru` RAM used v GB
- `rt` RAM total v GB
- `gu` GPU load v %
- `gt` GPU teplota v C
- `nd` download rychlost v MB/s
- `nu` upload rychlost v MB/s
- `ds` vyuziti disku v procentech (`C:61,D:24,...`)
- `pw` odhad celkove spotreby PC ve W (souctem dostupnych power sensoru)
- `id` pozadavek uspat ESP displej (`1` = monitor je OFF, `0` = monitor ON/DIM)

## 4) Poznamky

- Pokud se data neposilaji dele nez 3 sekundy, hodnoty na ESP prejdou do `N/A`.
- Kdyz je monitor ve Windows vypnuty (OFF), host posle `id=1` a ESP prepne panel do sleep rezimu.
- Pro uplne zhasnuti podsvitu pripoj `BL/LED` na GPIO (v `Pc monitor.ino` nastav `TFT_BL`), jinak muze podsvit svitit i pri sleep.
- Kdyz CPU teplota zustava `N/A`, spust host aplikaci jako Administrator.
- `pw` je odhad z hardwarovych sensoru (neni to mereni ze zasuvky/wattmetru).
- Displej zobrazuje `DOWN/UP` jako graf (download zeleny, upload modry).

## 5) Ktery soubor spousti program

Windows aplikaci spousti `PcMonitorHost/Program.cs`:

- Entry point je metoda `Main()`.
- V ni je `Application.Run(new MainForm());`, tim se otevre hlavni okno.

Spusteni:

```powershell
dotnet run --project PcMonitorHost
```

EXE build (mimo VS Code) vytvoris:

```powershell
dotnet publish "PcMonitorHost\\PcMonitorHost.csproj" -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o ".\\PcMonitorHost_EXE"
```

Vysledny soubor je `PcMonitorHost_EXE\\PcMonitorHost.exe`.
Spusteni jako Administrator:

```powershell
.\Run-PcMonitorHost-Admin.ps1
```

Alternativa:

- Otevri `Pc monitor.sln` ve Visual Studiu, nastav startup project `PcMonitorHost`, pak `F5`.
