// ========================= DISPLAY DRIVER =========================
#define ST7789_DRIVER    // Dùng driver ST7789

// ======================== PIN CONFIG ==============================
// Kết nối SPI cho TFT
#define TFT_CS   5      // Chip select cho TFT
#define TFT_DC    16     // Data/Command
#define TFT_RST   17    // Reset (hoặc -1 nếu không dùng)

#define TFT_MOSI 23
#define TFT_SCLK 18
#define TFT_MISO -1      // ST7789 không cần MISO

// ========================= CẢM ỨNG (nếu có) ======================
#define TOUCH_CS  -1     // Không dùng cảm ứng

// ========================= TÙY CHỈNH ==============================
#define TFT_WIDTH  240
#define TFT_HEIGHT 320

#define SPI_FREQUENCY       40000000  // 40 MHz
#define SPI_READ_FREQUENCY  20000000
#define SPI_TOUCH_FREQUENCY 2500000

#define TFT_INVERSION_ON         // ST7789 cần đảo màu
#define TFT_RGB_ORDER TFT_BGR    // Một số màn ST7789 cần đảo màu RGB → BGR

// ====================== FONT OPTION ===============================
#define LOAD_GLCD
#define LOAD_FONT2
#define LOAD_FONT4
#define LOAD_FONT6
#define LOAD_FONT7
#define LOAD_FONT8
#define LOAD_GFXFF
#define SMOOTH_FONT
