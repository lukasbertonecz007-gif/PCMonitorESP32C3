#include <LovyanGFX.hpp>
#include <SPI.h>
#include <ctype.h>
#include <math.h>

// Recommended mapping for ESP32-C3 Super Mini (ILI9341 SPI):
// SCK=GPIO4, MISO=GPIO5, MOSI=GPIO6, CS=GPIO7, DC=GPIO3, RST=GPIO1.
constexpr int TFT_CS = 7;
constexpr int TFT_DC = 3;
constexpr int TFT_RST = 1;
constexpr int TFT_SCLK = 4;
constexpr int TFT_MISO = 5;
constexpr int TFT_MOSI = 6;
// Optional: connect display BL/LED pin here (e.g. GPIO10) to fully turn off backlight on sleep.
// Keep -1 when BL is hardwired to 3V3.
constexpr int TFT_BL = 10;

constexpr uint32_t SERIAL_BAUD = 115200;
constexpr uint32_t TFT_SPI_HZ = 40000000;
constexpr uint32_t DATA_TIMEOUT_MS = 3000;
constexpr uint32_t RENDER_INTERVAL_MS = 16;
constexpr uint32_t GRAPH_SAMPLE_MS = 70;
constexpr uint32_t FRAME_BUDGET_MS = 12;
constexpr uint32_t BACKLIGHT_PWM_HZ = 18000;
constexpr uint8_t BACKLIGHT_PWM_BITS = 8;
constexpr uint8_t BACKLIGHT_ON_LEVEL = 255;
constexpr uint8_t BACKLIGHT_OFF_LEVEL = 0;
constexpr uint16_t WAKE_FADE_MS = 260;
constexpr uint16_t SLEEP_FADE_MS = 120;
constexpr uint8_t WAKE_START_LEVEL = 10;
constexpr float OVERLOAD_CPU_THRESHOLD = 90.0f;
constexpr float OVERLOAD_GPU_THRESHOLD = 90.0f;
constexpr uint32_t OVERLOAD_HOLD_MS = 1600;
constexpr uint32_t OVERLOAD_COOLDOWN_MS = 60000;
constexpr uint32_t OVERLOAD_ALERT_MS = 2600;
constexpr uint32_t OVERLOAD_BLINK_MS = 280;

constexpr uint8_t DISPLAY_ROTATION = 1;  // 320x240 landscape (rotated 180 deg from mode 3)
constexpr uint16_t SCREEN_W = 320;
constexpr uint16_t SCREEN_H = 240;

constexpr size_t SERIAL_BUFFER_SIZE = 980;
constexpr int16_t CACHE_EMPTY = -32768;
constexpr int MAX_THREADS_PACKET = 32;
constexpr int MAX_THREAD_TILES = 24;
constexpr int MAX_DISKS_PACKET = 8;

constexpr uint16_t COLOR_BG = 0x1082;
constexpr uint16_t COLOR_PANEL = 0x18E3;
constexpr uint16_t COLOR_CARD = 0x2104;
constexpr uint16_t COLOR_BORDER = 0x31A6;
constexpr uint16_t COLOR_TEXT = 0xE71C;
constexpr uint16_t COLOR_TEXT_DIM = 0x9CD3;
constexpr uint16_t COLOR_GREEN = 0x07E0;
constexpr uint16_t COLOR_YELLOW = 0xFFE0;
constexpr uint16_t COLOR_RED = 0xF800;
constexpr uint16_t COLOR_CYAN = 0x07FF;
constexpr uint16_t COLOR_DOWN = 0x07E0;
constexpr uint16_t COLOR_UP = 0x001F;
constexpr uint16_t COLOR_BLACK = 0x0000;

constexpr int ROW_START_Y = 8;
constexpr int ROW_STEP = 28;
constexpr int LABEL_X = 6;
constexpr int LABEL_W = 58;
constexpr int LEFT_X = 70;
constexpr int RIGHT_X = 196;
constexpr int CELL_W = 118;
constexpr int CELL_H = 18;

constexpr int GRAPH_X = 2;
constexpr int GRAPH_Y = 88;
constexpr int GRAPH_W = 316;
constexpr int GRAPH_H = 62;
constexpr int GRAPH_INNER_X = GRAPH_X + 3;
constexpr int GRAPH_INNER_Y = GRAPH_Y + 13;
constexpr int GRAPH_INNER_W = GRAPH_W - 6;
constexpr int GRAPH_INNER_H = GRAPH_H - 16;
constexpr int GRAPH_PLOT_X = GRAPH_INNER_X + 1;
constexpr int GRAPH_PLOT_Y = GRAPH_INNER_Y + 1;
constexpr int GRAPH_PLOT_W = GRAPH_INNER_W - 2;
constexpr int GRAPH_PLOT_H = GRAPH_INNER_H - 2;
constexpr int GRAPH_SPLIT_GAP = 4;
constexpr int NET_PLOT_W = (GRAPH_PLOT_W - GRAPH_SPLIT_GAP) / 2;
constexpr int DISK_PLOT_W = GRAPH_PLOT_W - GRAPH_SPLIT_GAP - NET_PLOT_W;
constexpr int NET_PLOT_X = GRAPH_PLOT_X;
constexpr int DISK_PLOT_X = NET_PLOT_X + NET_PLOT_W + GRAPH_SPLIT_GAP;
constexpr int NET_POINTS = 90;
constexpr int GRAPH_LINE_HALF_THICKNESS = 1;
constexpr float TILE_LOAD_ALPHA_RISE = 0.18f;
constexpr float TILE_LOAD_ALPHA_FALL = 0.12f;
constexpr uint16_t TILE_CACHE_EMPTY = 65535;

constexpr int BOTTOM_Y = 154;
constexpr int BOTTOM_H = 82;
constexpr int CPU_PANEL_X = 2;
constexpr int CPU_PANEL_W = 196;
constexpr int POWER_PANEL_X = 202;
constexpr int POWER_PANEL_W = 116;
constexpr int CPU_GRID_X = CPU_PANEL_X + 6;
constexpr int CPU_GRID_Y = BOTTOM_Y + 16;
constexpr int CPU_GRID_W = CPU_PANEL_W - 12;
constexpr int CPU_GRID_H = BOTTOM_H - 20;

class LGFX : public lgfx::LGFX_Device {
  lgfx::Panel_ILI9341 _panel_instance;
  lgfx::Bus_SPI _bus_instance;

 public:
  LGFX() {
    {
      auto cfg = _bus_instance.config();
      cfg.spi_host = SPI2_HOST;  // ESP32-C3
      cfg.spi_mode = 0;
      cfg.freq_write = TFT_SPI_HZ;
      cfg.freq_read = 16000000;
      cfg.spi_3wire = false;
      cfg.use_lock = true;
      cfg.dma_channel = SPI_DMA_CH_AUTO;
      cfg.pin_sclk = TFT_SCLK;
      cfg.pin_mosi = TFT_MOSI;
      cfg.pin_miso = TFT_MISO;
      cfg.pin_dc = TFT_DC;
      _bus_instance.config(cfg);
      _panel_instance.setBus(&_bus_instance);
    }

    {
      auto cfg = _panel_instance.config();
      cfg.pin_cs = TFT_CS;
      cfg.pin_rst = TFT_RST;
      cfg.pin_busy = -1;
      cfg.panel_width = 240;
      cfg.panel_height = 320;
      cfg.offset_x = 0;
      cfg.offset_y = 0;
      cfg.offset_rotation = 0;
      cfg.dummy_read_pixel = 8;
      cfg.dummy_read_bits = 1;
      cfg.readable = false;
      cfg.invert = false;
      cfg.rgb_order = false;
      cfg.dlen_16bit = false;
      cfg.bus_shared = false;
      _panel_instance.config(cfg);
    }

    setPanel(&_panel_instance);
  }
};

LGFX tft;

enum MetricKind : uint8_t {
  MetricPercent,
  MetricTemperature,
  MetricPower
};

struct Telemetry {
  float cpuUsage = -1.0f;
  float cpuTemp = -1.0f;
  int cpuCores = 0;
  int cpuThreads = 0;
  int threadLoadCount = 0;
  uint8_t threadLoads[MAX_THREADS_PACKET]{};

  float ramUsage = -1.0f;
  float ramUsedGb = -1.0f;
  float ramTotalGb = -1.0f;

  float gpuUsage = -1.0f;
  float gpuTemp = -1.0f;

  float netDownMBps = -1.0f;
  float netUpMBps = -1.0f;
  int diskCount = 0;
  uint8_t diskUsage[MAX_DISKS_PACKET]{};
  char diskLabels[MAX_DISKS_PACKET][6]{};
  float totalPowerW = -1.0f;
  bool userIdle = false;
  bool overloadAlertRequested = false;

  bool hasData = false;
  uint32_t lastUpdateMs = 0;
};

struct SmoothState {
  float cpuUsage = -1.0f;
  float cpuTemp = -1.0f;
  float ramUsage = -1.0f;
  float ramUsedGb = -1.0f;
  float ramTotalGb = -1.0f;
  float gpuUsage = -1.0f;
  float gpuTemp = -1.0f;
  float netDownMBps = -1.0f;
  float netUpMBps = -1.0f;
  float totalPowerW = -1.0f;
};

struct RenderCache {
  bool initialized = false;
  bool stale = true;

  int16_t cpuUsage = CACHE_EMPTY;
  int16_t cpuTemp = CACHE_EMPTY;
  int16_t ramUsage = CACHE_EMPTY;
  int16_t ramUsed = CACHE_EMPTY;
  int16_t ramTotal = CACHE_EMPTY;
  int16_t gpuUsage = CACHE_EMPTY;
  int16_t gpuTemp = CACHE_EMPTY;
  int16_t power = CACHE_EMPTY;
  uint32_t cpuInfoHash = 0;
  uint32_t netLegendHash = 0;
  uint32_t diskLegendHash = 0;
  uint32_t cpuGridLayoutHash = 0;
  bool cpuGridValid = false;
  uint16_t cpuTileLoadCache[MAX_THREAD_TILES]{};
  uint8_t connState = 0;
};

Telemetry telemetry;
SmoothState smoothState;
RenderCache renderCache;

float downHistory[NET_POINTS];
float upHistory[NET_POINTS];
float diskHistory[MAX_DISKS_PACKET][NET_POINTS];
int historyCount = 0;
uint32_t lastGraphSampleMs = 0;
float dynamicPowerScaleW = 260.0f;

char serialBuffer[SERIAL_BUFFER_SIZE];
size_t serialPos = 0;
bool serialOverflow = false;

uint32_t lastRenderMs = 0;

LGFX_Sprite graphSprite(&tft);
bool graphSpriteReady = false;
LGFX_Sprite diskSprite(&tft);
bool diskSpriteReady = false;
LGFX_Sprite cpuTilesSprite(&tft);
bool cpuTilesSpriteReady = false;
bool backlightPwmReady = false;
uint8_t backlightLevel = BACKLIGHT_ON_LEVEL;
float cpuTileSmoothLoad[MAX_THREAD_TILES];
char diskSeriesLabels[MAX_DISKS_PACKET][6];
int diskSeriesCount = 0;
bool displaySleeping = false;
bool wakeAnimationPending = false;
bool overloadAlertActive = false;
bool overloadOverlayDrawn = false;
bool fullRedrawPending = false;
uint32_t overloadCandidateStartMs = 0;
uint32_t overloadLastTriggerMs = 0;
uint32_t overloadAlertStartMs = 0;
uint8_t overloadBlinkPhase = 255;

uint16_t blendColor565(uint16_t colorA, uint16_t colorB, float a);
uint16_t tempGradientColor(float tempC);
void drawCenteredText(int x, int y, int w, int h, const char* text, uint8_t textSize, uint16_t color, uint16_t bg);
void drawCenteredTextSprite(LGFX_Sprite& canvas, int x, int y, int w, int h, const char* text, uint8_t textSize, uint16_t color, uint16_t bg);
bool applyDisplaySleepState(bool shouldSleep);
void setBacklightLevel(uint8_t level);
void animateBacklight(uint8_t fromLevel, uint8_t toLevel, uint16_t durationMs);
void playWakeAnimation(bool stale);
void updateOverloadAlertState(bool stale);
void startOverloadAlert(bool ignoreCooldown);
void drawOverloadAlert(bool force);
void parseDiskUsages(const char* text, Telemetry& parsed);
void drawDiskLegend(const char* legend, bool dim);
void drawDiskPanel(bool stale, bool force);
uint16_t crc16Ccitt(const uint8_t* data, size_t len);
bool validateCrc(char* line);
void drawConnectionIndicator(bool stale, bool force);

void setup() {
  Serial.begin(SERIAL_BAUD);

  for (int i = 0; i < NET_POINTS; i++) {
    downHistory[i] = 0.0f;
    upHistory[i] = 0.0f;
    for (int d = 0; d < MAX_DISKS_PACKET; d++) {
      diskHistory[d][i] = -1.0f;
    }
  }

  tft.init();
  tft.setRotation(DISPLAY_ROTATION);
  if (TFT_BL >= 0) {
    pinMode(TFT_BL, OUTPUT);
    backlightPwmReady = ledcAttach(TFT_BL, BACKLIGHT_PWM_HZ, BACKLIGHT_PWM_BITS);
    setBacklightLevel(BACKLIGHT_OFF_LEVEL);
  }

  graphSprite.setColorDepth(16);
  graphSprite.setPsram(false);
  graphSpriteReady = graphSprite.createSprite(NET_PLOT_W, GRAPH_PLOT_H) != nullptr;
  diskSprite.setColorDepth(16);
  diskSprite.setPsram(false);
  diskSpriteReady = diskSprite.createSprite(DISK_PLOT_W, GRAPH_PLOT_H) != nullptr;
  cpuTilesSprite.setColorDepth(16);
  cpuTilesSprite.setPsram(false);
  cpuTilesSpriteReady = cpuTilesSprite.createSprite(CPU_GRID_W, CPU_GRID_H) != nullptr;

  drawStaticLayout();
  resetHistory();
  renderCache = RenderCache();
  for (int i = 0; i < MAX_THREAD_TILES; i++) {
    renderCache.cpuTileLoadCache[i] = TILE_CACHE_EMPTY;
    cpuTileSmoothLoad[i] = -1.0f;
  }
  for (int i = 0; i < MAX_DISKS_PACKET; i++) {
    diskSeriesLabels[i][0] = '\0';
  }
  diskSeriesCount = 0;
  drawDynamic(true);
  if (TFT_BL >= 0) {
    animateBacklight(BACKLIGHT_OFF_LEVEL, BACKLIGHT_ON_LEVEL, WAKE_FADE_MS);
  }
}

void loop() {
  readSerial();

  uint32_t now = millis();
  if (now - lastRenderMs >= RENDER_INTERVAL_MS) {
    lastRenderMs = now;
    drawDynamic(false);
  }
}

void readSerial() {
  while (Serial.available() > 0) {
    char ch = static_cast<char>(Serial.read());

    if (serialOverflow) {
      if (ch == '\n') {
        serialOverflow = false;
        serialPos = 0;
      }
      continue;
    }

    if (ch == '\r') {
      continue;
    }

    if (ch == '\n') {
      serialBuffer[serialPos] = '\0';
      if (serialPos > 0) {
        processLine(serialBuffer);
      }
      serialPos = 0;
      continue;
    }

    if (serialPos >= SERIAL_BUFFER_SIZE - 1) {
      serialOverflow = true;
      serialPos = 0;
      continue;
    }

    serialBuffer[serialPos++] = ch;
  }
}

void processLine(char* line) {
  if (!validateCrc(line)) {
    return;
  }

  char* save = nullptr;
  char* token = strtok_r(line, ";", &save);
  if (token == nullptr || strcmp(token, "DATA") != 0) {
    return;
  }

  Telemetry parsed;

  while (true) {
    token = strtok_r(nullptr, ";", &save);
    if (token == nullptr) {
      break;
    }

    char* equalPos = strchr(token, '=');
    if (equalPos == nullptr) {
      continue;
    }

    *equalPos = '\0';
    const char* key = token;
    const char* valueText = equalPos + 1;
    float value = strtof(valueText, nullptr);

    if (strcmp(key, "cpu") == 0) {
      parsed.cpuUsage = value;
      continue;
    }
    if (strcmp(key, "ct") == 0) {
      parsed.cpuTemp = value;
      continue;
    }
    if (strcmp(key, "co") == 0) {
      parsed.cpuCores = static_cast<int>(strtol(valueText, nullptr, 10));
      continue;
    }
    if (strcmp(key, "th") == 0) {
      parsed.cpuThreads = static_cast<int>(strtol(valueText, nullptr, 10));
      continue;
    }
    if (strcmp(key, "tl") == 0) {
      parseThreadLoads(valueText, parsed);
      continue;
    }
    if (strcmp(key, "rp") == 0) {
      parsed.ramUsage = value;
      continue;
    }
    if (strcmp(key, "ru") == 0) {
      parsed.ramUsedGb = value;
      continue;
    }
    if (strcmp(key, "rt") == 0) {
      parsed.ramTotalGb = value;
      continue;
    }
    if (strcmp(key, "gu") == 0) {
      parsed.gpuUsage = value;
      continue;
    }
    if (strcmp(key, "gt") == 0) {
      parsed.gpuTemp = value;
      continue;
    }
    if (strcmp(key, "nd") == 0) {
      parsed.netDownMBps = value;
      continue;
    }
    if (strcmp(key, "nu") == 0) {
      parsed.netUpMBps = value;
      continue;
    }
    if (strcmp(key, "ds") == 0) {
      parseDiskUsages(valueText, parsed);
      continue;
    }
    if (strcmp(key, "pw") == 0) {
      parsed.totalPowerW = value;
      continue;
    }
    if (strcmp(key, "id") == 0) {
      parsed.userIdle = strtol(valueText, nullptr, 10) != 0;
      continue;
    }
    if (strcmp(key, "oa") == 0) {
      parsed.overloadAlertRequested = strtol(valueText, nullptr, 10) != 0;
      continue;
    }
    if (strcmp(key, "crc") == 0) {
      continue;
    }
  }

  parsed.hasData = true;
  parsed.lastUpdateMs = millis();
  telemetry = parsed;

  Serial.println("ACK");
}

void parseThreadLoads(const char* text, Telemetry& parsed) {
  parsed.threadLoadCount = 0;

  if (text == nullptr || *text == '\0') {
    return;
  }

  if ((text[0] == 'n' || text[0] == 'N') &&
      (text[1] == 'a' || text[1] == 'A') &&
      text[2] == '\0') {
    return;
  }

  const char* ptr = text;
  while (*ptr != '\0' && parsed.threadLoadCount < MAX_THREADS_PACKET) {
    while (*ptr == ' ') {
      ptr++;
    }

    char* endPtr = nullptr;
    long load = strtol(ptr, &endPtr, 10);
    if (endPtr == ptr) {
      while (*ptr != '\0' && *ptr != ',') {
        ptr++;
      }
      if (*ptr == ',') {
        ptr++;
      }
      continue;
    }

    if (load < 0) {
      load = 0;
    } else if (load > 100) {
      load = 100;
    }

    parsed.threadLoads[parsed.threadLoadCount++] = static_cast<uint8_t>(load);
    ptr = endPtr;

    if (*ptr == ',') {
      ptr++;
    }
  }
}

void parseDiskUsages(const char* text, Telemetry& parsed) {
  parsed.diskCount = 0;
  for (int i = 0; i < MAX_DISKS_PACKET; i++) {
    parsed.diskUsage[i] = 0;
    parsed.diskLabels[i][0] = '\0';
  }

  if (text == nullptr || *text == '\0') {
    return;
  }

  if ((text[0] == 'n' || text[0] == 'N') &&
      (text[1] == 'a' || text[1] == 'A') &&
      text[2] == '\0') {
    return;
  }

  const char* ptr = text;
  while (*ptr != '\0' && parsed.diskCount < MAX_DISKS_PACKET) {
    while (*ptr == ',' || *ptr == ' ') {
      ptr++;
    }
    if (*ptr == '\0') {
      break;
    }

    int diskIndex = parsed.diskCount;
    int labelLen = 0;
    while (*ptr != '\0' && *ptr != ':' && *ptr != ',') {
      if (isalnum(static_cast<unsigned char>(*ptr)) && labelLen < 5) {
        parsed.diskLabels[diskIndex][labelLen++] = static_cast<char>(toupper(static_cast<unsigned char>(*ptr)));
      }
      ptr++;
    }
    parsed.diskLabels[diskIndex][labelLen] = '\0';

    if (labelLen == 0) {
      parsed.diskLabels[diskIndex][0] = 'D';
      parsed.diskLabels[diskIndex][1] = static_cast<char>('1' + diskIndex);
      parsed.diskLabels[diskIndex][2] = '\0';
    }

    int usage = 0;
    if (*ptr == ':') {
      ptr++;
      char* endPtr = nullptr;
      long value = strtol(ptr, &endPtr, 10);
      if (endPtr != ptr) {
        usage = static_cast<int>(value);
        ptr = endPtr;
      } else {
        while (*ptr != '\0' && *ptr != ',') {
          ptr++;
        }
      }
    }

    if (usage < 0) {
      usage = 0;
    } else if (usage > 100) {
      usage = 100;
    }

    parsed.diskUsage[diskIndex] = static_cast<uint8_t>(usage);
    parsed.diskCount++;

    if (*ptr == ',') {
      ptr++;
    }
  }
}

void drawDynamic(bool force) {
  bool stale = !telemetry.hasData || (millis() - telemetry.lastUpdateMs > DATA_TIMEOUT_MS);
  // Sleep only when we are actively receiving data and host reports user idle.
  // Do not sleep on stale/disconnected state, otherwise some panels stay white.
  bool shouldSleep = !stale && telemetry.userIdle;
  bool wokeUp = applyDisplaySleepState(shouldSleep);
  if (displaySleeping) {
    renderCache.stale = stale;
    return;
  }
  bool hardReset = !renderCache.initialized || wokeUp;
  bool fullRedraw = force || hardReset || fullRedrawPending;
  bool staleChanged = fullRedraw || renderCache.stale != stale;

  if (hardReset) {
    renderCache = RenderCache();
    for (int i = 0; i < MAX_THREAD_TILES; i++) {
      renderCache.cpuTileLoadCache[i] = TILE_CACHE_EMPTY;
      cpuTileSmoothLoad[i] = -1.0f;
    }
    for (int i = 0; i < MAX_DISKS_PACKET; i++) {
      diskSeriesLabels[i][0] = '\0';
    }
    diskSeriesCount = 0;
  }

  updateSmoothedValues(stale, fullRedraw || staleChanged);
  updateOverloadAlertState(stale);
  bool overloadEnded = overloadOverlayDrawn && !overloadAlertActive;
  if (overloadAlertActive) {
    drawOverloadAlert(force || !overloadOverlayDrawn);
    overloadOverlayDrawn = true;
    renderCache.stale = stale;
    renderCache.initialized = true;
    return;
  }
  if (overloadEnded) {
    overloadOverlayDrawn = false;
    fullRedrawPending = true;
    fullRedraw = true;
    staleChanged = true;
    renderCache.netLegendHash = 0;
    renderCache.diskLegendHash = 0;
  }

  if (fullRedraw) {
    drawStaticLayout();
    if (hardReset) {
      resetHistory();
    }
    fullRedrawPending = false;
  }

  bool bypassFrameBudget = fullRedraw || wokeUp || wakeAnimationPending;
  uint32_t frameStart = millis();
  tft.startWrite();
  updateRows(fullRedraw || staleChanged);
  drawConnectionIndicator(stale, fullRedraw || staleChanged);
  if (!bypassFrameBudget && millis() - frameStart > FRAME_BUDGET_MS) {
    tft.endWrite();
    renderCache.stale = stale;
    renderCache.initialized = true;
    return;
  }
  updateCpuInfoPanel(stale, fullRedraw || staleChanged);
  if (!bypassFrameBudget && millis() - frameStart > FRAME_BUDGET_MS) {
    tft.endWrite();
    renderCache.stale = stale;
    renderCache.initialized = true;
    return;
  }
  updatePowerPanel(fullRedraw || staleChanged);
  if (!bypassFrameBudget && millis() - frameStart > FRAME_BUDGET_MS) {
    tft.endWrite();
    renderCache.stale = stale;
    renderCache.initialized = true;
    return;
  }
  updateNetworkGraph(stale, fullRedraw || staleChanged);
  tft.endWrite();
  if (wakeAnimationPending) {
    playWakeAnimation(stale);
  }

  renderCache.stale = stale;
  renderCache.initialized = true;
}

bool applyDisplaySleepState(bool shouldSleep) {
  if (shouldSleep == displaySleeping) {
    return false;
  }

  if (shouldSleep) {
    // Keep panel content black before sleep to avoid bright flash on some ILI9341 modules.
    tft.fillScreen(COLOR_BLACK);
    if (TFT_BL >= 0 && backlightLevel > BACKLIGHT_OFF_LEVEL) {
      animateBacklight(backlightLevel, BACKLIGHT_OFF_LEVEL, SLEEP_FADE_MS);
    }
    tft.startWrite();
    tft.writeCommand(0x28);  // Display OFF
    tft.endWrite();
    delay(5);
    tft.sleep();
    if (TFT_BL >= 0) {
      setBacklightLevel(BACKLIGHT_OFF_LEVEL);
    }
    wakeAnimationPending = false;
    overloadAlertActive = false;
    overloadOverlayDrawn = false;
    overloadBlinkPhase = 255;
    displaySleeping = true;
    return false;
  }

  if (TFT_BL >= 0) {
    setBacklightLevel(BACKLIGHT_OFF_LEVEL);
  }
  tft.wakeup();
  delay(120);
  tft.startWrite();
  tft.writeCommand(0x29);  // Display ON
  tft.endWrite();
  delay(5);
  tft.setRotation(DISPLAY_ROTATION);
  wakeAnimationPending = TFT_BL >= 0;
  displaySleeping = false;
  return true;
}

void setBacklightLevel(uint8_t level) {
  if (TFT_BL < 0) {
    return;
  }

  backlightLevel = level;
  if (backlightPwmReady) {
    ledcWrite(TFT_BL, level);
    return;
  }

  digitalWrite(TFT_BL, level >= 128 ? HIGH : LOW);
}

void animateBacklight(uint8_t fromLevel, uint8_t toLevel, uint16_t durationMs) {
  if (TFT_BL < 0) {
    return;
  }

  if (!backlightPwmReady || durationMs == 0 || fromLevel == toLevel) {
    setBacklightLevel(toLevel);
    return;
  }

  int delta = abs(static_cast<int>(toLevel) - static_cast<int>(fromLevel));
  int steps = delta / 12;
  if (steps < 8) {
    steps = 8;
  } else if (steps > 24) {
    steps = 24;
  }

  uint16_t stepDelay = durationMs / steps;
  if (stepDelay < 4) {
    stepDelay = 4;
  }

  for (int i = 0; i <= steps; i++) {
    float t = static_cast<float>(i) / static_cast<float>(steps);
    float eased = 1.0f - powf(1.0f - t, 3.0f);
    int level = static_cast<int>(roundf(fromLevel + (toLevel - fromLevel) * eased));
    if (level < 0) {
      level = 0;
    } else if (level > 255) {
      level = 255;
    }

    setBacklightLevel(static_cast<uint8_t>(level));
    delay(stepDelay);
  }

  setBacklightLevel(toLevel);
}

void playWakeAnimation(bool stale) {
  wakeAnimationPending = false;
  if (TFT_BL < 0) {
    return;
  }

  tft.startWrite();
  drawStaticLayout();
  updateRows(true);
  drawConnectionIndicator(stale, true);
  updateCpuInfoPanel(stale, true);
  updatePowerPanel(true);
  updateNetworkGraph(stale, true);
  tft.endWrite();

  setBacklightLevel(WAKE_START_LEVEL);
  animateBacklight(backlightLevel, BACKLIGHT_ON_LEVEL, WAKE_FADE_MS + 80);
}

void updateOverloadAlertState(bool stale) {
  uint32_t now = millis();

  if (overloadAlertActive) {
    if (now - overloadAlertStartMs >= OVERLOAD_ALERT_MS) {
      overloadAlertActive = false;
      overloadBlinkPhase = 255;
    }
    return;
  }

  bool forceTrigger = telemetry.overloadAlertRequested;
  if (forceTrigger) {
    startOverloadAlert(true);
    return;
  }

  float cpuLoad = max(telemetry.cpuUsage, smoothState.cpuUsage);
  float gpuLoad = max(telemetry.gpuUsage, smoothState.gpuUsage);
  if (stale || cpuLoad < OVERLOAD_CPU_THRESHOLD || gpuLoad < OVERLOAD_GPU_THRESHOLD) {
    overloadCandidateStartMs = 0;
    return;
  }

  if (now - overloadLastTriggerMs < OVERLOAD_COOLDOWN_MS) {
    overloadCandidateStartMs = 0;
    return;
  }

  if (overloadCandidateStartMs == 0) {
    overloadCandidateStartMs = now;
    return;
  }

  if (now - overloadCandidateStartMs >= OVERLOAD_HOLD_MS) {
    startOverloadAlert(false);
  }
}

void startOverloadAlert(bool ignoreCooldown) {
  uint32_t now = millis();
  if (!ignoreCooldown && now - overloadLastTriggerMs < OVERLOAD_COOLDOWN_MS) {
    return;
  }

  overloadAlertActive = true;
  overloadOverlayDrawn = false;
  overloadCandidateStartMs = 0;
  overloadLastTriggerMs = now;
  overloadAlertStartMs = now;
  overloadBlinkPhase = 255;
}

void drawOverloadAlert(bool force) {
  uint8_t phase = static_cast<uint8_t>(((millis() - overloadAlertStartMs) / OVERLOAD_BLINK_MS) & 1u);
  if (!force && overloadBlinkPhase == phase) {
    return;
  }
  overloadBlinkPhase = phase;

  uint16_t bgColor = tft.color565(56, 0, 0);
  uint16_t borderColor = phase ? tft.color565(255, 220, 0) : tft.color565(220, 40, 40);
  uint16_t triangleColor = phase ? tft.color565(255, 236, 96) : tft.color565(255, 214, 0);
  uint16_t innerColor = bgColor;
  uint16_t textColor = phase ? COLOR_YELLOW : tft.color565(255, 235, 150);

  if (force) {
    tft.fillScreen(bgColor);
  }

  int topX = SCREEN_W / 2;
  int topY = 18;
  int leftX = 44;
  int rightX = SCREEN_W - 44;
  int baseY = SCREEN_H - 22;
  tft.drawRect(2, 2, SCREEN_W - 4, SCREEN_H - 4, borderColor);
  tft.drawRect(5, 5, SCREEN_W - 10, SCREEN_H - 10, borderColor);
  tft.fillTriangle(topX, topY, leftX, baseY, rightX, baseY, triangleColor);
  tft.fillTriangle(topX, topY + 14, leftX + 16, baseY - 14, rightX - 16, baseY - 14, innerColor);
  tft.drawTriangle(topX, topY, leftX, baseY, rightX, baseY, borderColor);

  int barW = 18;
  int barH = 76;
  int barX = (SCREEN_W - barW) / 2;
  int barY = 72;
  tft.fillRoundRect(barX, barY, barW, barH, 7, triangleColor);
  tft.fillCircle(SCREEN_W / 2, 169, 11, triangleColor);
  tft.drawRoundRect(barX, barY, barW, barH, 7, borderColor);
  tft.drawCircle(SCREEN_W / 2, 169, 11, borderColor);

  tft.fillRect(0, 188, SCREEN_W, 22, bgColor);
  drawCenteredText(0, 190, SCREEN_W, 18, "OVERLOAD", 2, textColor, bgColor);
}

void drawStaticLayout() {
  tft.fillScreen(COLOR_BG);

  drawRowSkeleton(ROW_START_Y, "CPU");
  drawRowSkeleton(ROW_START_Y + ROW_STEP, "GPU");
  drawRowSkeleton(ROW_START_Y + ROW_STEP * 2, "RAM");

  drawNetworkSkeleton();
  drawBottomSkeleton();
}

void drawRowSkeleton(int y, const char* label) {
  tft.fillRect(2, y - 3, SCREEN_W - 4, 24, COLOR_PANEL);
  tft.drawRect(2, y - 3, SCREEN_W - 4, 24, COLOR_BORDER);

  tft.fillRect(LABEL_X, y, LABEL_W, CELL_H, COLOR_PANEL);
  drawCenteredText(LABEL_X, y, LABEL_W, CELL_H, label, 2, COLOR_CYAN, COLOR_PANEL);

  tft.fillRect(LEFT_X, y, CELL_W, CELL_H, COLOR_CARD);
  tft.fillRect(RIGHT_X, y, CELL_W, CELL_H, COLOR_CARD);
  tft.drawRect(LEFT_X, y, CELL_W, CELL_H, COLOR_BORDER);
  tft.drawRect(RIGHT_X, y, CELL_W, CELL_H, COLOR_BORDER);
}

void drawNetworkSkeleton() {
  tft.fillRect(GRAPH_X, GRAPH_Y, GRAPH_W, GRAPH_H, COLOR_PANEL);
  tft.drawRect(GRAPH_X, GRAPH_Y, GRAPH_W, GRAPH_H, COLOR_BORDER);

  drawCenteredText(NET_PLOT_X, GRAPH_Y + 1, NET_PLOT_W, 10, "NETWORK", 1, COLOR_TEXT_DIM, COLOR_PANEL);
  drawCenteredText(DISK_PLOT_X, GRAPH_Y + 1, DISK_PLOT_W, 10, "DISKS", 1, COLOR_TEXT_DIM, COLOR_PANEL);

  tft.fillRect(NET_PLOT_X, GRAPH_PLOT_Y, NET_PLOT_W, GRAPH_PLOT_H, COLOR_CARD);
  tft.drawRect(NET_PLOT_X, GRAPH_PLOT_Y, NET_PLOT_W, GRAPH_PLOT_H, COLOR_BORDER);

  tft.fillRect(DISK_PLOT_X, GRAPH_PLOT_Y, DISK_PLOT_W, GRAPH_PLOT_H, COLOR_CARD);
  tft.drawRect(DISK_PLOT_X, GRAPH_PLOT_Y, DISK_PLOT_W, GRAPH_PLOT_H, COLOR_BORDER);
}

void drawBottomSkeleton() {
  tft.fillRect(CPU_PANEL_X, BOTTOM_Y, CPU_PANEL_W, BOTTOM_H, COLOR_PANEL);
  tft.drawRect(CPU_PANEL_X, BOTTOM_Y, CPU_PANEL_W, BOTTOM_H, COLOR_BORDER);
  drawCenteredText(CPU_PANEL_X + 2, BOTTOM_Y + 2, CPU_PANEL_W - 4, 10, "CPU THREAD LOAD", 1, COLOR_TEXT_DIM, COLOR_PANEL);

  tft.fillRect(POWER_PANEL_X, BOTTOM_Y, POWER_PANEL_W, BOTTOM_H, COLOR_PANEL);
  tft.drawRect(POWER_PANEL_X, BOTTOM_Y, POWER_PANEL_W, BOTTOM_H, COLOR_BORDER);
  drawCenteredText(POWER_PANEL_X + 4, BOTTOM_Y + 2, POWER_PANEL_W - 18, 10, "TOTAL POWER", 1, COLOR_TEXT_DIM, COLOR_PANEL);
}

void updateSmoothedValues(bool stale, bool force) {
  if (stale && telemetry.hasData) {
    return;
  }

  float cpuTarget = stale ? -1.0f : telemetry.cpuUsage;
  float cpuTempTarget = stale ? -1.0f : telemetry.cpuTemp;
  float gpuTarget = stale ? -1.0f : telemetry.gpuUsage;
  float gpuTempTarget = stale ? -1.0f : telemetry.gpuTemp;
  float ramTarget = stale ? -1.0f : telemetry.ramUsage;
  float ramUsedTarget = stale ? -1.0f : telemetry.ramUsedGb;
  float ramTotalTarget = stale ? -1.0f : telemetry.ramTotalGb;
  float downTarget = stale ? -1.0f : telemetry.netDownMBps;
  float upTarget = stale ? -1.0f : telemetry.netUpMBps;
  float powerTarget = stale ? -1.0f : telemetry.totalPowerW;

  smoothState.cpuUsage = smoothValue(smoothState.cpuUsage, cpuTarget, 0.24f, force);
  smoothState.cpuTemp = smoothValue(smoothState.cpuTemp, cpuTempTarget, 0.20f, force);

  smoothState.gpuUsage = smoothValue(smoothState.gpuUsage, gpuTarget, 0.24f, force);
  smoothState.gpuTemp = smoothValue(smoothState.gpuTemp, gpuTempTarget, 0.20f, force);

  smoothState.ramUsage = smoothValue(smoothState.ramUsage, ramTarget, 0.22f, force);
  smoothState.ramUsedGb = smoothValue(smoothState.ramUsedGb, ramUsedTarget, 0.18f, force);
  smoothState.ramTotalGb = smoothValue(smoothState.ramTotalGb, ramTotalTarget, 0.16f, force);

  smoothState.netDownMBps = smoothValue(smoothState.netDownMBps, downTarget, 0.26f, force);
  smoothState.netUpMBps = smoothValue(smoothState.netUpMBps, upTarget, 0.26f, force);

  smoothState.totalPowerW = smoothValue(smoothState.totalPowerW, powerTarget, 0.20f, force);
}

void updateRows(bool force) {
  int16_t cpuUsage = quantizeRounded(smoothState.cpuUsage, 0.0f, 100.0f);
  int16_t cpuTemp = quantizeRounded(smoothState.cpuTemp, 0.0f, 130.0f);

  int16_t gpuUsage = quantizeRounded(smoothState.gpuUsage, 0.0f, 100.0f);
  int16_t gpuTemp = quantizeRounded(smoothState.gpuTemp, 0.0f, 130.0f);

  int16_t ramUsage = quantizeRounded(smoothState.ramUsage, 0.0f, 100.0f);
  int16_t ramUsed = quantizeRounded(smoothState.ramUsedGb, 0.0f, 512.0f);
  int16_t ramTotal = quantizeRounded(smoothState.ramTotalGb, 0.0f, 512.0f);

  updateMetricCell(LEFT_X, ROW_START_Y, cpuUsage, renderCache.cpuUsage, MetricPercent, force);
  updateMetricCell(RIGHT_X, ROW_START_Y, cpuTemp, renderCache.cpuTemp, MetricTemperature, force);

  updateMetricCell(LEFT_X, ROW_START_Y + ROW_STEP, gpuUsage, renderCache.gpuUsage, MetricPercent, force);
  updateMetricCell(RIGHT_X, ROW_START_Y + ROW_STEP, gpuTemp, renderCache.gpuTemp, MetricTemperature, force);

  updateMetricCell(LEFT_X, ROW_START_Y + ROW_STEP * 2, ramUsage, renderCache.ramUsage, MetricPercent, force);
  updateRamTotalCell(RIGHT_X, ROW_START_Y + ROW_STEP * 2, ramUsed, ramTotal, force);
}

void updateMetricCell(int x, int y, int16_t valueTenths, int16_t& cachedValue, MetricKind kind, bool force) {
  if (!force && cachedValue == valueTenths) {
    return;
  }
  cachedValue = valueTenths;

  char text[16];
  uint16_t color = metricColor(valueTenths, kind);
  if (valueTenths == CACHE_EMPTY) {
    strcpy(text, "N/A");
  } else {
    if (kind == MetricPercent) {
      snprintf(text, sizeof(text), "%d%%", valueTenths);
    } else if (kind == MetricTemperature) {
      snprintf(text, sizeof(text), "%dC", valueTenths);
    } else {
      snprintf(text, sizeof(text), "%d", valueTenths);
    }
  }

  tft.fillRect(x + 1, y + 1, CELL_W - 2, CELL_H - 2, COLOR_CARD);
  drawCenteredText(x + 1, y + 1, CELL_W - 2, CELL_H - 2, text, 2, color, COLOR_CARD);
}

void updateRamTotalCell(int x, int y, int16_t usedTenths, int16_t totalTenths, bool force) {
  if (!force && renderCache.ramUsed == usedTenths && renderCache.ramTotal == totalTenths) {
    return;
  }
  renderCache.ramUsed = usedTenths;
  renderCache.ramTotal = totalTenths;

  char text[18];
  if (usedTenths == CACHE_EMPTY || totalTenths == CACHE_EMPTY) {
    strcpy(text, "N/A");
    tft.fillRect(x + 1, y + 1, CELL_W - 2, CELL_H - 2, COLOR_CARD);
    drawCenteredText(x + 1, y + 1, CELL_W - 2, CELL_H - 2, text, 2, COLOR_TEXT_DIM, COLOR_CARD);
    return;
  }

  int used = static_cast<int>(usedTenths);
  int total = static_cast<int>(totalTenths);
  snprintf(text, sizeof(text), "%d/%dG", used, total);

  tft.fillRect(x + 1, y + 1, CELL_W - 2, CELL_H - 2, COLOR_CARD);
  drawCenteredText(x + 1, y + 1, CELL_W - 2, CELL_H - 2, text, 2, COLOR_TEXT, COLOR_CARD);
}

void updateCpuInfoPanel(bool stale, bool force) {
  uint32_t hash = hashCpuInfo(stale);
  bool panelChanged = force || renderCache.cpuInfoHash != hash;
  renderCache.cpuInfoHash = hash;
  drawThreadTiles(stale, panelChanged);
}

void drawThreadTiles(bool stale, bool force) {
  int sourceCount = telemetry.cpuThreads > 0 ? telemetry.cpuThreads : telemetry.threadLoadCount;
  if (sourceCount <= 0) {
    sourceCount = telemetry.threadLoadCount;
  }

  if (sourceCount <= 0) {
    uint32_t layoutHash = 0xA5A55A5Au;
    bool needsClear = force || !renderCache.cpuGridValid || renderCache.cpuGridLayoutHash != layoutHash;
    if (needsClear) {
      if (cpuTilesSpriteReady) {
        cpuTilesSprite.fillScreen(COLOR_PANEL);
        drawCenteredTextSprite(cpuTilesSprite, 0, 0, CPU_GRID_W, CPU_GRID_H, "N/A", 2, COLOR_TEXT_DIM, COLOR_PANEL);
        cpuTilesSprite.pushSprite(CPU_GRID_X, CPU_GRID_Y);
      } else {
        tft.fillRect(CPU_GRID_X, CPU_GRID_Y, CPU_GRID_W, CPU_GRID_H, COLOR_PANEL);
        drawCenteredText(CPU_GRID_X, CPU_GRID_Y, CPU_GRID_W, CPU_GRID_H, "N/A", 2, COLOR_TEXT_DIM, COLOR_PANEL);
      }
      renderCache.cpuGridLayoutHash = layoutHash;
      renderCache.cpuGridValid = true;
      for (int i = 0; i < MAX_THREAD_TILES; i++) {
        renderCache.cpuTileLoadCache[i] = TILE_CACHE_EMPTY;
        cpuTileSmoothLoad[i] = -1.0f;
      }
    }
    return;
  }

  int displayCount = sourceCount;
  if (displayCount > MAX_THREAD_TILES) {
    displayCount = MAX_THREAD_TILES;
  }

  int columns = displayCount <= 6 ? displayCount : (displayCount <= 12 ? 6 : 8);
  if (columns <= 0) {
    columns = 1;
  }
  int rows = (displayCount + columns - 1) / columns;
  if (rows <= 0) {
    rows = 1;
  }

  constexpr int spacing = 2;
  int tileW = (CPU_GRID_W - (columns - 1) * spacing) / columns;
  int tileH = (CPU_GRID_H - (rows - 1) * spacing) / rows;
  if (tileW < 6) {
    tileW = 6;
  }
  if (tileH < 6) {
    tileH = 6;
  }

  uint32_t layoutHash = 2166136261u;
  layoutHash ^= static_cast<uint32_t>(sourceCount);
  layoutHash *= 16777619u;
  layoutHash ^= static_cast<uint32_t>(displayCount);
  layoutHash *= 16777619u;
  layoutHash ^= static_cast<uint32_t>(columns);
  layoutHash *= 16777619u;
  layoutHash ^= static_cast<uint32_t>(rows);
  layoutHash *= 16777619u;
  layoutHash ^= static_cast<uint32_t>(tileW);
  layoutHash *= 16777619u;
  layoutHash ^= static_cast<uint32_t>(tileH);
  layoutHash *= 16777619u;

  bool layoutChanged = force || !renderCache.cpuGridValid || renderCache.cpuGridLayoutHash != layoutHash;
  if (layoutChanged) {
    renderCache.cpuGridLayoutHash = layoutHash;
    renderCache.cpuGridValid = true;
    for (int i = 0; i < MAX_THREAD_TILES; i++) {
      renderCache.cpuTileLoadCache[i] = TILE_CACHE_EMPTY;
      cpuTileSmoothLoad[i] = -1.0f;
    }
  }

  // Always use sprite for double-buffering when available (eliminates tearing/artifacts).
  bool useSprite = cpuTilesSpriteReady;
  if (useSprite) {
    cpuTilesSprite.fillScreen(COLOR_PANEL);
  } else if (layoutChanged) {
    tft.fillRect(CPU_GRID_X, CPU_GRID_Y, CPU_GRID_W, CPU_GRID_H, COLOR_PANEL);
  }

  for (int i = 0; i < displayCount; i++) {
    int srcStart = (i * sourceCount) / displayCount;
    int srcEnd = ((i + 1) * sourceCount) / displayCount;
    if (srcEnd <= srcStart) {
      srcEnd = srcStart + 1;
    }

    float avgLoad = 0.0f;
    int count = 0;
    for (int src = srcStart; src < srcEnd; src++) {
      avgLoad += threadLoadValue(src);
      count++;
    }
    if (count > 0) {
      avgLoad /= static_cast<float>(count);
    }

    float targetLoad = stale ? -1.0f : avgLoad;
    float smoothedLoad = cpuTileSmoothLoad[i];

    if (targetLoad < 0.0f) {
      if (smoothedLoad < 0.0f) {
        smoothedLoad = -1.0f;
      } else {
        smoothedLoad -= 3.0f;
        if (smoothedLoad <= 0.25f) {
          smoothedLoad = -1.0f;
        }
      }
    } else {
      float clampedTarget = min(100.0f, max(0.0f, targetLoad));
      if (smoothedLoad < 0.0f) {
        smoothedLoad = clampedTarget;
      } else {
        float alpha = clampedTarget >= smoothedLoad ? TILE_LOAD_ALPHA_RISE : TILE_LOAD_ALPHA_FALL;
        float delta = (clampedTarget - smoothedLoad) * alpha;
        float maxStep = clampedTarget >= smoothedLoad ? 1.8f : 1.4f;
        if (delta > maxStep) {
          delta = maxStep;
        } else if (delta < -maxStep) {
          delta = -maxStep;
        }
        smoothedLoad += delta;
        if (fabsf(clampedTarget - smoothedLoad) < 0.10f) {
          smoothedLoad = clampedTarget;
        }
      }
    }
    cpuTileSmoothLoad[i] = smoothedLoad;

    float displayLoad = -1.0f;
    uint16_t cacheLevel = TILE_CACHE_EMPTY;
    if (smoothedLoad >= 0.0f) {
      displayLoad = min(100.0f, max(0.0f, smoothedLoad));
      cacheLevel = static_cast<uint16_t>(displayLoad * 2.0f + 0.5f);  // 0.5% steps
      if (cacheLevel > 200) {
        cacheLevel = 200;
      }
    }

    bool needsDraw = useSprite || layoutChanged || renderCache.cpuTileLoadCache[i] != cacheLevel;
    renderCache.cpuTileLoadCache[i] = cacheLevel;
    if (!needsDraw) {
      continue;
    }

    int row = i / columns;
    int col = i % columns;
    int x = col * (tileW + spacing);
    int y = row * (tileH + spacing);
    if (!useSprite) {
      x += CPU_GRID_X;
      y += CPU_GRID_Y;
    }

    if (useSprite) {
      cpuTilesSprite.fillRect(x, y, tileW, tileH, COLOR_CARD);
      cpuTilesSprite.drawRect(x, y, tileW, tileH, COLOR_BORDER);
    } else {
      tft.fillRect(x, y, tileW, tileH, COLOR_CARD);
      tft.drawRect(x, y, tileW, tileH, COLOR_BORDER);
    }

    if (cacheLevel != TILE_CACHE_EMPTY && tileW > 2 && tileH > 2) {
      int innerX = x + 1;
      int innerY = y + 1;
      int innerW = tileW - 2;
      int innerH = tileH - 2;
      uint16_t fillColor = usageGradientColor(displayLoad / 100.0f);

      float fillHeight = (innerH * displayLoad) / 100.0f;
      int fullRows = static_cast<int>(fillHeight);
      float topFrac = fillHeight - static_cast<float>(fullRows);

      if (fullRows > 0) {
        int fillY = innerY + innerH - fullRows;
        if (useSprite) {
          cpuTilesSprite.fillRect(innerX, fillY, innerW, fullRows, fillColor);
        } else {
          tft.fillRect(innerX, fillY, innerW, fullRows, fillColor);
        }
      }

      if (topFrac > 0.01f && fullRows < innerH) {
        int yTop = innerY + innerH - fullRows - 1;
        uint16_t topColor = blendColor565(fillColor, COLOR_CARD, topFrac);
        if (useSprite) {
          cpuTilesSprite.fillRect(innerX, yTop, innerW, 1, topColor);
        } else {
          tft.fillRect(innerX, yTop, innerW, 1, topColor);
        }
      }
    }
  }

  for (int i = displayCount; i < MAX_THREAD_TILES; i++) {
    renderCache.cpuTileLoadCache[i] = TILE_CACHE_EMPTY;
    cpuTileSmoothLoad[i] = -1.0f;
  }

  if (useSprite) {
    cpuTilesSprite.pushSprite(CPU_GRID_X, CPU_GRID_Y);
  }
}

float threadLoadValue(int index) {
  if (telemetry.threadLoadCount > 0) {
    if (index < telemetry.threadLoadCount) {
      return telemetry.threadLoads[index];
    }
    return telemetry.threadLoads[telemetry.threadLoadCount - 1];
  }

  if (telemetry.cpuUsage >= 0.0f) {
    return telemetry.cpuUsage;
  }
  return -1.0f;
}

uint32_t hashCpuInfo(bool stale) {
  uint32_t hash = 2166136261u;
  hash ^= static_cast<uint32_t>(telemetry.cpuCores + 31 * telemetry.cpuThreads + (stale ? 97 : 0));
  hash *= 16777619u;
  return hash;
}

void updatePowerPanel(bool force) {
  int16_t powerTenths = quantizeRounded(smoothState.totalPowerW, 0.0f, 3000.0f);
  if (!force && renderCache.power == powerTenths) {
    return;
  }
  renderCache.power = powerTenths;

  int boxX = POWER_PANEL_X + 4;
  int boxY = BOTTOM_Y + 20;
  int boxW = POWER_PANEL_W - 8;
  int boxH = BOTTOM_H - 26;
  if (force) {
    tft.fillRect(boxX, boxY, boxW, boxH, COLOR_PANEL);
  } else {
    int textBandH = 30;
    int textBandY = boxY + (boxH - textBandH) / 2;
    tft.fillRect(boxX, textBandY, boxW, textBandH, COLOR_PANEL);
  }

  char text[16];
  uint16_t textColor = COLOR_TEXT_DIM;
  if (powerTenths == CACHE_EMPTY) {
    strcpy(text, "N/A");
  } else {
    float powerW = static_cast<float>(powerTenths);
    if (powerW > dynamicPowerScaleW) {
      dynamicPowerScaleW = powerW;
    } else {
      dynamicPowerScaleW = max(220.0f, dynamicPowerScaleW - 0.18f);
    }

    float refMaxW = max(250.0f, dynamicPowerScaleW * 1.20f);
    textColor = usageGradientColor(powerW / refMaxW);
    snprintf(text, sizeof(text), "%dW", static_cast<int>(powerW + 0.5f));
  }

  drawCenteredText(boxX, boxY, boxW, boxH, text, 3, textColor, COLOR_PANEL);
}

void updateNetworkGraph(bool stale, bool force) {
  uint32_t now = millis();
  bool canSample = !stale && (now - lastGraphSampleMs >= GRAPH_SAMPLE_MS);
  if (canSample) {
    lastGraphSampleMs = now;
    appendHistory(
      max(0.0f, smoothState.netDownMBps),
      max(0.0f, smoothState.netUpMBps),
      telemetry);
    force = true;
  } else if (!stale && historyCount > 0) {
    float liveDown = max(0.0f, smoothState.netDownMBps);
    float liveUp = max(0.0f, smoothState.netUpMBps);
    int lastIndex = historyCount - 1;

    if (fabsf(downHistory[lastIndex] - liveDown) >= 0.05f || fabsf(upHistory[lastIndex] - liveUp) >= 0.05f) {
      downHistory[lastIndex] = liveDown;
      upHistory[lastIndex] = liveUp;
      force = true;
    }
  }

  if (!force) {
    return;
  }

  int plotW = NET_PLOT_W;
  int plotH = GRAPH_PLOT_H;

  if (graphSpriteReady) {
    graphSprite.fillScreen(COLOR_CARD);
    drawGraphGridTo(graphSprite, 0, 0, plotW, plotH);
    drawGraphLinesTo(graphSprite, 0, plotH - 1, plotW - 1, plotH - 1);
    graphSprite.pushSprite(NET_PLOT_X, GRAPH_PLOT_Y);
  } else {
    tft.fillRect(NET_PLOT_X, GRAPH_PLOT_Y, plotW, plotH, COLOR_CARD);
    drawGraphGridTo(tft, NET_PLOT_X, GRAPH_PLOT_Y, plotW, plotH);
    drawGraphLinesTo(tft, NET_PLOT_X, GRAPH_PLOT_Y + plotH - 1, plotW - 1, plotH - 1);
  }

  drawGraphLegend(stale);
  drawDiskPanel(stale, force);
}

template <typename TCanvas>
void drawGraphGridTo(TCanvas& canvas, int x0, int y0, int w, int h) {
  uint16_t grid = COLOR_BORDER;

  for (int i = 1; i < 4; i++) {
    int y = y0 + (h * i) / 4;
    canvas.drawFastHLine(x0, y, w, grid);
  }
}

template <typename TCanvas>
void drawThickGraphLine(TCanvas& canvas, int x1, int y1, int x2, int y2, uint16_t color) {
  int dx = abs(x2 - x1);
  int dy = abs(y2 - y1);

  if (dx >= dy) {
    for (int o = -GRAPH_LINE_HALF_THICKNESS; o <= GRAPH_LINE_HALF_THICKNESS; o++) {
      canvas.drawLine(x1, y1 + o, x2, y2 + o, color);
    }
  } else {
    for (int o = -GRAPH_LINE_HALF_THICKNESS; o <= GRAPH_LINE_HALF_THICKNESS; o++) {
      canvas.drawLine(x1 + o, y1, x2 + o, y2, color);
    }
  }
}

template <typename TCanvas>
void drawGraphLinesTo(TCanvas& canvas, int xStart, int yBase, int width, int height) {
  if (historyCount < 2) {
    return;
  }

  float maxVal = 3.0f;
  for (int i = 0; i < historyCount; i++) {
    maxVal = max(maxVal, downHistory[i]);
    maxVal = max(maxVal, upHistory[i]);
  }
  if (maxVal < 0.5f) {
    maxVal = 0.5f;
  }

  float step = static_cast<float>(width) / static_cast<float>(NET_POINTS - 1);

  for (int i = 1; i < historyCount; i++) {
    int x1 = xStart + static_cast<int>((i - 1) * step);
    int x2 = xStart + static_cast<int>(i * step);

    int y1d = yBase - static_cast<int>((downHistory[i - 1] / maxVal) * height);
    int y2d = yBase - static_cast<int>((downHistory[i] / maxVal) * height);
    int y1u = yBase - static_cast<int>((upHistory[i - 1] / maxVal) * height);
    int y2u = yBase - static_cast<int>((upHistory[i] / maxVal) * height);

    drawThickGraphLine(canvas, x1, y1d, x2, y2d, COLOR_DOWN);
    drawThickGraphLine(canvas, x1, y1u, x2, y2u, COLOR_UP);
  }
}

void drawGraphLegend(bool stale) {
  char legend[48];
  if (stale && historyCount > 0) {
    int last = historyCount - 1;
    int down = static_cast<int>(max(0.0f, downHistory[last]) + 0.5f);
    int up = static_cast<int>(max(0.0f, upHistory[last]) + 0.5f);
    snprintf(legend, sizeof(legend), "D %d  U %d MB/s", down, up);
  } else if (stale) {
    strcpy(legend, "D --  U -- MB/s");
  } else {
    int down = static_cast<int>(max(0.0f, smoothState.netDownMBps) + 0.5f);
    int up = static_cast<int>(max(0.0f, smoothState.netUpMBps) + 0.5f);
    snprintf(legend, sizeof(legend), "D %d  U %d MB/s", down, up);
  }

  uint32_t hash = 2166136261u;
  for (int i = 0; legend[i] != '\0'; i++) {
    hash ^= static_cast<uint8_t>(legend[i]);
    hash *= 16777619u;
  }
  if (renderCache.netLegendHash == hash) {
    return;
  }
  renderCache.netLegendHash = hash;

  tft.fillRect(NET_PLOT_X, GRAPH_Y + 1, NET_PLOT_W, 10, COLOR_PANEL);
  drawCenteredText(
    NET_PLOT_X,
    GRAPH_Y + 1,
    NET_PLOT_W,
    10,
    legend,
    1,
    stale ? COLOR_TEXT_DIM : COLOR_TEXT,
    COLOR_PANEL);
}

void drawDiskPanel(bool stale, bool force) {
  if (!force) {
    return;
  }

  bool useSprite = diskSpriteReady;
  if (useSprite) {
    diskSprite.fillScreen(COLOR_CARD);
  } else {
    tft.fillRect(DISK_PLOT_X, GRAPH_PLOT_Y, DISK_PLOT_W, GRAPH_PLOT_H, COLOR_CARD);
  }

  if (diskSeriesCount <= 0 || historyCount < 2) {
    if (useSprite) {
      drawCenteredTextSprite(diskSprite, 0, 0, DISK_PLOT_W, GRAPH_PLOT_H, "N/A", 2, COLOR_TEXT_DIM, COLOR_CARD);
      diskSprite.pushSprite(DISK_PLOT_X, GRAPH_PLOT_Y);
    } else {
      drawCenteredText(DISK_PLOT_X, GRAPH_PLOT_Y, DISK_PLOT_W, GRAPH_PLOT_H, "N/A", 2, COLOR_TEXT_DIM, COLOR_CARD);
    }
    drawDiskLegend("DISKS", true);
    return;
  }

  constexpr uint16_t diskColors[8] = {
    0xF800,  // red
    0x07E0,  // green
    0x001F,  // blue
    0xFFE0,  // yellow
    0xF81F,  // magenta
    0x07FF,  // cyan
    0xFD20,  // orange
    0xFFFF   // white
  };

  int plotW = DISK_PLOT_W;
  int plotH = GRAPH_PLOT_H;
  if (useSprite) {
    drawGraphGridTo(diskSprite, 0, 0, plotW, plotH);
  } else {
    drawGraphGridTo(tft, DISK_PLOT_X, GRAPH_PLOT_Y, plotW, plotH);
  }

  int lines = diskSeriesCount;
  if (lines > MAX_DISKS_PACKET) {
    lines = MAX_DISKS_PACKET;
  }
  if (lines > 5) {
    lines = 5;  // readability on 240p
  }

  float step = static_cast<float>(plotW - 1) / static_cast<float>(NET_POINTS - 1);
  for (int d = 0; d < lines; d++) {
    uint16_t color = diskColors[d % 8];
    for (int i = 1; i < historyCount; i++) {
      float v1 = diskHistory[d][i - 1];
      float v2 = diskHistory[d][i];
      if (v1 < 0.0f || v2 < 0.0f) {
        continue;
      }

      int x1 = static_cast<int>((i - 1) * step);
      int x2 = static_cast<int>(i * step);
      int y1 = (plotH - 1) - static_cast<int>((v1 / 100.0f) * (plotH - 1));
      int y2 = (plotH - 1) - static_cast<int>((v2 / 100.0f) * (plotH - 1));

      if (useSprite) {
        drawThickGraphLine(diskSprite, x1, y1, x2, y2, color);
      } else {
        drawThickGraphLine(tft, DISK_PLOT_X + x1, GRAPH_PLOT_Y + y1, DISK_PLOT_X + x2, GRAPH_PLOT_Y + y2, color);
      }
    }
  }

  if (useSprite) {
    diskSprite.pushSprite(DISK_PLOT_X, GRAPH_PLOT_Y);
  }

  char legend[72];
  legend[0] = '\0';
  int legendPos = 0;
  int lastIdx = historyCount - 1;
  for (int d = 0; d < lines; d++) {
    const char* label = diskSeriesLabels[d][0] != '\0' ? diskSeriesLabels[d] : "D";
    int value = 0;
    if (lastIdx >= 0 && diskHistory[d][lastIdx] >= 0.0f) {
      value = static_cast<int>(diskHistory[d][lastIdx] + 0.5f);
    }

    int wrote = snprintf(legend + legendPos, sizeof(legend) - legendPos, "%s:%d%% ", label, value);
    if (wrote <= 0 || wrote >= static_cast<int>(sizeof(legend) - legendPos)) {
      break;
    }
    legendPos += wrote;
    if (legendPos >= static_cast<int>(sizeof(legend) - 1)) {
      break;
    }
  }

  drawDiskLegend(legendPos > 0 ? legend : "DISKS", stale);
}

void drawDiskLegend(const char* legend, bool dim) {
  uint32_t hash = 2166136261u;
  hash ^= dim ? 0xA5u : 0x5Au;
  hash *= 16777619u;
  for (int i = 0; legend[i] != '\0'; i++) {
    hash ^= static_cast<uint8_t>(legend[i]);
    hash *= 16777619u;
  }

  if (renderCache.diskLegendHash == hash) {
    return;
  }
  renderCache.diskLegendHash = hash;

  tft.fillRect(DISK_PLOT_X, GRAPH_Y + 1, DISK_PLOT_W, 10, COLOR_PANEL);
  drawCenteredText(
    DISK_PLOT_X,
    GRAPH_Y + 1,
    DISK_PLOT_W,
    10,
    legend,
    1,
    dim ? COLOR_TEXT_DIM : COLOR_TEXT,
    COLOR_PANEL);
}

void drawConnectionIndicator(bool stale, bool force) {
  uint8_t state = 0;
  if (telemetry.hasData) {
    state = stale ? 1 : 2;
  }

  if (!force && renderCache.connState == state) {
    return;
  }
  renderCache.connState = state;

  int x = POWER_PANEL_X + POWER_PANEL_W - 10;
  int y = BOTTOM_Y + 7;
  tft.fillRect(POWER_PANEL_X + POWER_PANEL_W - 18, BOTTOM_Y + 1, 14, 12, COLOR_PANEL);
  uint16_t color = state == 2 ? COLOR_GREEN : (state == 1 ? COLOR_YELLOW : COLOR_TEXT_DIM);
  tft.fillCircle(x, y, 4, color);
  tft.drawCircle(x, y, 4, COLOR_BORDER);
}

void appendHistory(float down, float up, const Telemetry& snapshot) {
  int diskCount = snapshot.diskCount;
  if (diskCount > MAX_DISKS_PACKET) {
    diskCount = MAX_DISKS_PACKET;
  }
  diskSeriesCount = diskCount;

  for (int d = 0; d < MAX_DISKS_PACKET; d++) {
    if (d < diskCount && snapshot.diskLabels[d][0] != '\0') {
      strncpy(diskSeriesLabels[d], snapshot.diskLabels[d], sizeof(diskSeriesLabels[d]) - 1);
      diskSeriesLabels[d][sizeof(diskSeriesLabels[d]) - 1] = '\0';
    } else {
      diskSeriesLabels[d][0] = '\0';
    }
  }

  if (historyCount < NET_POINTS) {
    downHistory[historyCount] = down;
    upHistory[historyCount] = up;
    for (int d = 0; d < MAX_DISKS_PACKET; d++) {
      diskHistory[d][historyCount] = d < diskCount ? snapshot.diskUsage[d] : -1.0f;
    }
    historyCount++;
    return;
  }

  for (int i = 1; i < NET_POINTS; i++) {
    downHistory[i - 1] = downHistory[i];
    upHistory[i - 1] = upHistory[i];
    for (int d = 0; d < MAX_DISKS_PACKET; d++) {
      diskHistory[d][i - 1] = diskHistory[d][i];
    }
  }

  downHistory[NET_POINTS - 1] = down;
  upHistory[NET_POINTS - 1] = up;
  for (int d = 0; d < MAX_DISKS_PACKET; d++) {
    diskHistory[d][NET_POINTS - 1] = d < diskCount ? snapshot.diskUsage[d] : -1.0f;
  }
}

void resetHistory() {
  historyCount = 0;
  for (int i = 0; i < NET_POINTS; i++) {
    downHistory[i] = 0.0f;
    upHistory[i] = 0.0f;
    for (int d = 0; d < MAX_DISKS_PACKET; d++) {
      diskHistory[d][i] = -1.0f;
    }
  }
  for (int i = 0; i < MAX_DISKS_PACKET; i++) {
    diskSeriesLabels[i][0] = '\0';
  }
  diskSeriesCount = 0;
  lastGraphSampleMs = millis();
}

float smoothValue(float current, float target, float alpha, bool force) {
  if (force) {
    return target;
  }

  if (target < 0.0f) {
    if (current < 0.0f) {
      return -1.0f;
    }
    float drop = current - 9.0f;
    return drop <= 0.1f ? -1.0f : drop;
  }

  if (current < 0.0f) {
    return target;
  }

  float next = current + (target - current) * alpha;
  if (fabsf(next - target) < 0.04f) {
    return target;
  }
  return next;
}

int16_t quantizeRounded(float value, float minValue, float maxValue) {
  if (isnan(value) || isinf(value) || value < 0.0f) {
    return CACHE_EMPTY;
  }

  if (value < minValue) {
    value = minValue;
  } else if (value > maxValue) {
    value = maxValue;
  }

  return static_cast<int16_t>(value + 0.5f);
}

uint16_t metricColor(int16_t valueTenths, MetricKind kind) {
  if (valueTenths == CACHE_EMPTY) {
    return COLOR_TEXT_DIM;
  }

  float value = static_cast<float>(valueTenths);
  if (kind == MetricTemperature) {
    return tempGradientColor(value);
  }

  if (kind == MetricPower) {
    return usageGradientColor(value / 450.0f);
  }

  return usageGradientColor(value / 100.0f);
}

uint16_t tempGradientColor(float tempC) {
  if (tempC <= 40.0f) {
    return COLOR_GREEN;
  }
  if (tempC >= 90.0f) {
    return COLOR_RED;
  }

  if (tempC < 65.0f) {
    float t = (tempC - 40.0f) / 25.0f;
    return blendColor565(COLOR_YELLOW, COLOR_GREEN, t);
  }

  float t = (tempC - 65.0f) / 25.0f;
  return blendColor565(COLOR_RED, COLOR_YELLOW, t);
}

uint16_t usageGradientColor(float ratio) {
  if (ratio < 0.0f) {
    ratio = 0.0f;
  } else if (ratio > 1.0f) {
    ratio = 1.0f;
  }

  uint8_t red = static_cast<uint8_t>(ratio * 255.0f);
  uint8_t green = static_cast<uint8_t>((1.0f - ratio) * 255.0f);
  return tft.color565(red, green, 0);
}

uint16_t blendColor565(uint16_t colorA, uint16_t colorB, float a) {
  if (a <= 0.0f) {
    return colorB;
  }
  if (a >= 1.0f) {
    return colorA;
  }

  uint8_t a8 = static_cast<uint8_t>(a * 255.0f + 0.5f);
  uint8_t inv = static_cast<uint8_t>(255 - a8);

  uint8_t rA = static_cast<uint8_t>((colorA >> 11) & 0x1F);
  uint8_t gA = static_cast<uint8_t>((colorA >> 5) & 0x3F);
  uint8_t bA = static_cast<uint8_t>(colorA & 0x1F);

  uint8_t rB = static_cast<uint8_t>((colorB >> 11) & 0x1F);
  uint8_t gB = static_cast<uint8_t>((colorB >> 5) & 0x3F);
  uint8_t bB = static_cast<uint8_t>(colorB & 0x1F);

  uint8_t r = static_cast<uint8_t>((rA * a8 + rB * inv) / 255);
  uint8_t g = static_cast<uint8_t>((gA * a8 + gB * inv) / 255);
  uint8_t b = static_cast<uint8_t>((bA * a8 + bB * inv) / 255);

  return static_cast<uint16_t>((r << 11) | (g << 5) | b);
}

uint16_t crc16Ccitt(const uint8_t* data, size_t len) {
  uint16_t crc = 0xFFFF;
  for (size_t i = 0; i < len; i++) {
    crc ^= static_cast<uint16_t>(data[i] << 8);
    for (int b = 0; b < 8; b++) {
      if (crc & 0x8000) {
        crc = static_cast<uint16_t>((crc << 1) ^ 0x1021);
      } else {
        crc <<= 1;
      }
    }
  }
  return crc;
}

bool validateCrc(char* line) {
  char* crcTag = strstr(line, ";crc=");
  if (crcTag == nullptr) {
    return true;  // backward compatible
  }

  char* valuePtr = crcTag + 5;
  if (*valuePtr == '\0') {
    return false;
  }

  char* endPtr = nullptr;
  long parsed = strtol(valuePtr, &endPtr, 16);
  if (endPtr == valuePtr || parsed < 0 || parsed > 0xFFFF) {
    return false;
  }

  size_t dataLen = static_cast<size_t>(crcTag - line);
  uint16_t expected = static_cast<uint16_t>(parsed);
  uint16_t actual = crc16Ccitt(reinterpret_cast<const uint8_t*>(line), dataLen);
  return expected == actual;
}

void drawCenteredText(int x, int y, int w, int h, const char* text, uint8_t textSize, uint16_t color, uint16_t bg) {
  int textW = textPixelWidth(text, textSize);
  int textH = 8 * textSize;

  int textX = x + (w - textW) / 2;
  int textY = y + (h - textH) / 2;
  if (textX < x) {
    textX = x;
  }
  if (textY < y) {
    textY = y;
  }

  tft.setTextSize(textSize);
  tft.setTextColor(color, bg);
  tft.setCursor(textX, textY);
  tft.print(text);
}

void drawCenteredTextSprite(LGFX_Sprite& canvas, int x, int y, int w, int h, const char* text, uint8_t textSize, uint16_t color, uint16_t bg) {
  int textW = textPixelWidth(text, textSize);
  int textH = 8 * textSize;

  int textX = x + (w - textW) / 2;
  int textY = y + (h - textH) / 2;
  if (textX < x) {
    textX = x;
  }
  if (textY < y) {
    textY = y;
  }

  canvas.setTextSize(textSize);
  canvas.setTextColor(color, bg);
  canvas.setCursor(textX, textY);
  canvas.print(text);
}

int textPixelWidth(const char* text, uint8_t textSize) {
  return static_cast<int>(strlen(text)) * 6 * textSize;
}
