# Dev Blog - PC Monitor Host

## Projekt: PC Monitor Host (Windows aplikace pro ESP32-C3)

**Autor:** AI asistent (Qwen Code) ve spolupráci s uživatelem Lukáš  
**Repozitář:** `c:\Users\Lukasbertone\Documents\Pc monitor`  
**Technologie:** C# / .NET 8 / WinForms / LibreHardwareMonitor

---

## 14. 4. 2026 - Redesign UI ✅

### Co se dělo předtím
- Aplikace měla starší UI s `GroupBox` komponentami pro preview a log
- Screenshot ukazoval zastaralý layout s GroupBoxy "Obecné", "Připojení", "Správa napájení", "Senzory"
- Čeština bez diakritiky ("Pripojit", "Odpojit", "Nastaveni")
- Log panel byl stále viditelný ve SplitContaineru
- UI nebylo konzistentně zarovnané
- Textový preview místo vizuálního dashboardu

### Co bylo uděláno
1. **Čeština s diakritikou** ✅ - opraveny všechny stringy v T() metodě:
   - "Pripojit" → "Připojit", "Odpojit" → "Odpojit"
   - "Nastaveni" → "Nastavení", "Spoustet" → "Spouštět"
   - "Vychozi" → "Výchozí", "Uspat" → "Uspat", "pri" → "při"
   - A mnoho dalších...

2. **Lepší zarovnání** ✅ - TableLayoutPanel s konzistentními sloupci:
   - Connection bar: Label | Combo | Baud | Input | Interval | Input | Buttons
   - Settings: 4 sloupce s label+input páry
   - Status bar: 8 sloupců s poměrovým rozložením

3. **Log na tlačítko** ✅ - Toggle tlačítko "📋 Log" v connection baru:
   - Kliknutí zobrazí/skryje log panel
   - Tlačítko mění barvu podle stavu
   - Log má vlastní header s tlačítkem "Vymazat"

4. **Dashboard místo textového preview** ✅:
   - TelemetryCard komponenty pro CPU, GPU, teploty, RAM, spotřebu
   - Sparkline grafy pro síť a disky
   - Vizuální progress bary v kartách
   - Barevné akcenty podle typu metriky

5. **Moderní dark téma** ✅ - Zachováno existující barevné schéma:
   - WindowBack #141416, PanelBack #1C1C20, SectionBack #202026
   - AccentGreen pro connect tlačítko
   - AccentColor pro save tlačítko

### Nový layout
```
┌──────────────────────────────────────────────────────────┐
│ COM port: [COM6▼] Rychlost: [115200] Interval: [70]     │
│ [Obnovit porty] [Připojit]                         [📋 Log]│
├──────────────────────────────────────────────────────────┤
│ Jazyk:[Čeština] Výchozí COM:[COM6] CPU senzor:[Auto]     │
│ GPU senzor:[Auto]                                        │
│ ☑ Spouštět s Windows ☑ Uspat ESP... ☑ Automaticky...    │
│ Neaktivita:[60s] Interval úspory:[250ms]  [Uložit nast.]│
├──────────────────────────────────────────────────────────┤
│  ┌──── CPU ────┐  ┌──── GPU ────┐                       │
│  │     24%     │  │     31%     │  ← TelemetryCard      │
│  │  ▓▓▓░░░░░░  │  │  ▓▓▓▓░░░░░  │     + sparkline      │
│  ├─────────────┤  ├─────────────┤                       │
│  │ CPU Teplota │  │ GPU Teplota │  RAM    Spotřeba      │
│  │    57°C     │  │    62°C     │  46%    262W          │
│  ├─────────────┴──┴─────────────┴───────────────────────┤
│  🌐 SÍŤ              💾 DISKY                            │
│  ↓ 12.8  ↑ 2.4 MB/s  C:61%  D:24%                        │
│  [sparkline]         [sparkline]                         │
├──────────────────────────────────────────────────────────┤
│ ● Připojeno   Poslední: 14:32:05  ACK: 14:32:05  70ms   │
└──────────────────────────────────────────────────────────┘
```

### Smazané komponenty
- `_previewBox` (TextBox) - nahrazen dashboardem
- `_previewGroup` (GroupBox) - nahrazen dashboard panelem
- `_logGroup` (GroupBox) - nahrazen log panelem s headerem
- `ShouldRefreshPreview()` - již není potřeba
- `BuildPreview()` - již není potřeba

### Přidané komponenty
- `_statusIndicator` (StatusIndicator) - status s barevnou tečkou
- `_cpuCard`, `_gpuCard`, `_cpuTempCard`, `_gpuTempCard`, `_ramCard`, `_powerCard` (TelemetryCard)
- `_netSparkline`, `_diskSparkline` (SparklineGraph)
- `UpdateDashboard(HardwareSample)` - aktualizace dashboardu

### Architektura aplikace
```
PcMonitorHost/
├── Program.cs              # Entry point
├── MainForm.cs             # Hlavní okno (1679 řádků)
├── AppSettings.cs          # Nastavení (jazyk, COM, senzory)
├── AppSettingsStore.cs     # Ukládání/načítání settings
├── HardwareMonitorService.cs # LibreHardwareMonitor wrapper
├── HardwareSample.cs       # Datová struktura telemetrie
├── SerialService.cs        # COM port komunikace
├── SerialPacketFormatter.cs # Formatování DATA;... packetů
├── TelemetrySmoother.cs    # Vyhlazování dat
├── TelemetryCard.cs        # Custom control - karta s metrikou
├── SparklineGraph.cs       # Custom control - sparkline graf
├── StatusIndicator.cs      # Custom control - status s tečkou
├── RoundedPanel.cs         # Custom control - panel se zaoblením
├── ProgressBarEx.cs        # Vlastní progress bar
├── ToastNotification.cs    # Toast notifikace
├── StartupManager.cs       # Start s Windows (registry)
├── UserIdleDetector.cs     # Detekce neaktivity
├── CpuUsageProvider.cs     # CPU usage helper
└── DiskUsageSample.cs      # Disk usage datová struktura
```

### Serial packet format
Host posílá ESP32 packet:
```
DATA;cpu=24.7;ct=57.3;gu=31.8;gt=62.1;rp=46.2;ru=7.4;rt=31.9;nd=12.8;nu=2.4;ds=C:61,D:24;pw=262.3;id=0;oa=0;crc=ABCD
```

ESP odpovídá `ACK` při úspěšném příjmu.

### Barevné schéma (aktuální)
| Proměnná | Barva | Použití |
|----------|-------|---------|
| WindowBack | #141416 | Hlavní pozadí |
| PanelBack | #1C1C20 | Sekce |
| SectionBack | #202026 | Karty/sekce |
| InputBack | #2D2D34 | Vstupy |
| ForeText | #F0F0F5 | Hlavní text |
| SecondaryText | #A0A0AA | Vedlejší text |
| AccentColor | #64C8FF | Akcentní modrá |
| AccentGreen | #64DC96 | Úspěch/spojení |
| ErrorText | #FF7878 | Chyba |

---

## Struktura hlavního okna (aktuální)

```
┌─────────────────────────────────────────────────────┐
│  COM Port: [COM6▼]  [Obnovit] [Připojit]      [📋] │  ← Connection bar
├─────────────────────────────────────────────────────┤
│  Jazyk: [Čeština▼] ☑ Start s Windows ☑ Auto...     │  ← Settings bar
├─────────────────────────────────────────────────────┤
│  ┌───────────── Preview ──────────────┐             │
│  │ CPU usage: 24.7%                   │             │
│  │ CPU temp:  57.3°C                  │             │
│  │ ...telemetrie v textové podobě...  │             │
│  ├───────────── Log ──────────────────┤  ← Split    │
│  │ [14:32:01] Connected: COM6 @ 115200│             │
│  │ [14:32:02] Settings saved.         │             │
│  └────────────────────────────────────┘             │
├─────────────────────────────────────────────────────┤
│  Stav: Připojeno  Poslední: 14:32:05  ACK: 14:32:05 │  ← Status bar
└─────────────────────────────────────────────────────┘
```

---

## Poznámky k vývoji

### Co funguje dobře
- LibreHardwareMonitor integrace
- Serial komunikace s ACK timeoutem
- Auto reconnect při odpojení
- Sleep displeje při vypnutém monitoru (Windows power broadcast)
- Lokalizace CZ/EN
- Low power režim při neaktivitě
- Tray minimalizace

### Co se bude řešit
- ✅ Čeština s diakritikou
- ✅ Lepší zarovnání a layout
- ✅ Log toggle tlačítko
- ⏳ Možnost přidat dashboard karty (TelemetryCard komponenty)
- ⏳ Možnost přidat grafy

### Důležité soubory k zapamatování
- `MainForm.cs` - Hlavní UI logika, BuildUi(), ApplyLocalization()
- `SerialPacketFormatter.cs` - Formátování packetů pro ESP32
- `HardwareMonitorService.cs` - Čtení telemetrie z PC
