#include <Adafruit_Sensor.h>
#include <DHT11.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include <DHT.h>
#include <ArduinoJson.h>
#include <time.h>
#include <TFT_eSPI.h>

// ================== CẤU HÌNH ==================
#define DHTPIN   4
#define DHTTYPE  DHT11

const char* ssid     = "PHONG IT";
const char* password = "123456a@";

const char* mqtt_server = "da9cd5e6dd2340789dd109e41d1b6e10.s1.eu.hivemq.cloud";
const int   mqtt_port   = 8883;
const char* mqtt_user   = "phongtech";
const char* mqtt_pass   = "Phong2025";

// Device info
const char* device_id   = "temp_007";
const char* device_type = "sensor";
const char* model       = "TI_SENSORTAG";
const char* firmware    = "v2.1.8";

// ================== OBJECT ==================
DHT dht(DHTPIN, DHTTYPE);
TFT_eSPI tft = TFT_eSPI();

WiFiClientSecure espClient;
PubSubClient client(espClient);

// ================== STATE ==================
bool wifiOK = false;
bool timeOK = false;
bool mqttOK = false;
bool mqttSent = false;

// ================== TIMER ==================
unsigned long lastWiFiTry = 0;
unsigned long lastMQTTTry = 0;
unsigned long lastSend    = 0;
unsigned long lastScreen  = 0;

// ================== WIFI ==================
void handleWiFi() {
  if (WiFi.status() == WL_CONNECTED) {
    wifiOK = true;
    return;
  }

  wifiOK = false;
  if (millis() - lastWiFiTry < 5000) return;
  lastWiFiTry = millis();

  WiFi.begin(ssid, password);
}

// ================== TIME ==================
void handleTime() {
  if (time(nullptr) > 100000) {
    timeOK = true;
    return;
  }
  timeOK = false;
}

// ================== MQTT ==================
void handleMQTT() {
  if (!wifiOK) return;

  if (client.connected()) {
    mqttOK = true;
    return;
  }

  mqttOK = false;
  if (millis() - lastMQTTTry < 5000) return;
  lastMQTTTry = millis();

  client.connect("ESP32Client", mqtt_user, mqtt_pass);
}

// ================== MQTT SEND ==================
void sendMQTT(float temp, float hum) {
  if (!mqttOK) return;

  StaticJsonDocument<512> doc;

  JsonObject device = doc.createNestedObject("device");
  device["id"] = device_id;
  device["type"] = device_type;
  device["model"] = model;
  device["firmware"] = firmware;

  JsonObject data = doc.createNestedObject("data");
  data["temperature"] = temp;
  data["humidity"] = hum;

  JsonObject network = doc.createNestedObject("network");
  network["rssi"] = WiFi.RSSI();

  time_t now = time(nullptr);
  char ts[30];
  strftime(ts, sizeof(ts), "%Y-%m-%dT%H:%M:%S+07:00", localtime(&now));
  doc["timestamp"] = ts;

  char out[512];
  serializeJson(doc, out);

  String topic = "device/" + String(device_type) + "/" + String(device_id);
  mqttSent = client.publish(topic.c_str(), out);
}

// ================== TFT ==================
void drawScreen(float temp, float hum) {
  if (millis() - lastScreen < 1000) return;
  lastScreen = millis();

  tft.fillScreen(TFT_BLACK);

  tft.setTextSize(2);
  tft.setCursor(10, 10);
  tft.setTextColor(TFT_CYAN);
  tft.println("ROOM 1");

  tft.setTextColor(TFT_YELLOW);
  tft.setCursor(10, 40);
  tft.printf("Nhiet do: %.1f C", temp);

  tft.setCursor(10, 70);
  tft.printf("Do am   : %.1f %%", hum);

  char buf[25] = "--:--:--";
  if (timeOK) {
    time_t now = time(nullptr);
    strftime(buf, sizeof(buf), "%d/%m/%Y %H:%M:%S", localtime(&now));
  }

  tft.setCursor(10, 100);
  tft.setTextColor(TFT_GREEN);
  tft.println(buf);

  tft.setCursor(10, 140);
  tft.setTextColor(wifiOK ? TFT_GREEN : TFT_RED);
  tft.printf("WiFi : %s", wifiOK ? "CONECTED" : "FAIL");

  tft.setCursor(10, 170);
  tft.setTextColor(mqttOK ? TFT_GREEN : TFT_RED);
  tft.printf("MQTT : %s", mqttOK ? "CONECTED" : "FAIL");

  tft.setCursor(10, 200);
  tft.setTextColor(mqttSent ? TFT_GREENYELLOW : TFT_ORANGE);
  tft.printf("Send : %s", mqttSent ? "SENDING" : "FAIL");
}

// ================== SETUP ==================
void setup() {
  Serial.begin(115200);
  dht.begin();

  tft.init();
  tft.setRotation(0);
  tft.fillScreen(TFT_BLACK);
  tft.setTextSize(2);
  tft.setTextColor(TFT_GREEN);
  tft.setCursor(20, 100);
  tft.println("BOOTING...");

  espClient.setInsecure();
  client.setServer(mqtt_server, mqtt_port);

  configTime(7 * 3600, 0, "pool.ntp.org", "time.nist.gov");
}

// ================== LOOP ==================
void loop() {
  handleWiFi();
  handleTime();
  handleMQTT();
  client.loop();

  float temp = dht.readTemperature();
  float hum  = dht.readHumidity();

  if (!isnan(temp) && !isnan(hum)) {
    if (millis() - lastSend > 5000) {
      lastSend = millis();
      sendMQTT(temp, hum);
    }
  }

  drawScreen(temp, hum);
}