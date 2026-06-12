/*
 * SignSong_Glove.ino
 *
 * [하드웨어 연결]
 *  - MOTOR1_PIN (GPIO 25) → MOSFET 게이트 → 왼손 진동모터
 *  - MOTOR2_PIN (GPIO 26) → MOSFET 게이트 → 오른손 진동모터
 *  - 전원: 3.7V Li-Po + TP4056 모듈
 *
 * [Unity → ESP32 명령 포맷]
 *  "M1:{0~255},M2:{0~255},D:{ms}\n"
 *  예) "M1:40,M2:0,D:100\n"
 *
 * [블루투스 디바이스 이름]
 *  "SignSong_Glove" → PC 장치 관리자에서 이 이름으로 페어링 후 COM 포트 확인
 *
 * [주의] BluetoothSerial은 ESP32 Classic Bluetooth (SPP) 사용
 *        ESP32 Arduino Core 2.x 기준으로 작성됨
 */

#include "BluetoothSerial.h"

BluetoothSerial SerialBT;

// 모터 핀 (MOSFET 게이트에 연결)
const int MOTOR1_PIN = 25;  // 왼손
const int MOTOR2_PIN = 26;  // 오른손

// LEDC PWM 설정
const int LEDC_CH_LEFT  = 0;
const int LEDC_CH_RIGHT = 1;
const int LEDC_FREQ     = 5000;  // 5kHz
const int LEDC_RES      = 8;     // 8비트 해상도 (0~255)

String inputBuffer = "";

// 논블로킹 모터 타이머
unsigned long motorOffTime = 0;
bool motorActive = false;

void setup()
{
    Serial.begin(115200);

    // PWM 채널 초기화
    ledcSetup(LEDC_CH_LEFT,  LEDC_FREQ, LEDC_RES);
    ledcSetup(LEDC_CH_RIGHT, LEDC_FREQ, LEDC_RES);
    ledcAttachPin(MOTOR1_PIN, LEDC_CH_LEFT);
    ledcAttachPin(MOTOR2_PIN, LEDC_CH_RIGHT);

    // 모터 OFF 상태로 시작
    ledcWrite(LEDC_CH_LEFT,  0);
    ledcWrite(LEDC_CH_RIGHT, 0);

    // 블루투스 시작
    SerialBT.begin("SignSong_Glove");
    Serial.println("[SignSong] 블루투스 준비 완료 - 디바이스명: SignSong_Glove");
}

void loop()
{
    // 지속 시간이 끝나면 모터 OFF
    if (motorActive && millis() >= motorOffTime)
    {
        ledcWrite(LEDC_CH_LEFT,  0);
        ledcWrite(LEDC_CH_RIGHT, 0);
        motorActive = false;
    }

    // BT 수신 버퍼 처리 (\n 단위로 명령 파싱)
    while (SerialBT.available())
    {
        char c = (char)SerialBT.read();
        if (c == '\n')
        {
            inputBuffer.trim();
            if (inputBuffer.length() > 0)
            {
                processCommand(inputBuffer);
            }
            inputBuffer = "";
        }
        else
        {
            inputBuffer += c;
        }
    }
}

// "M1:40,M2:0,D:100" 형식 파싱 후 모터 구동
void processCommand(String cmd)
{
    int m1  = parseValue(cmd, "M1:");
    int m2  = parseValue(cmd, "M2:");
    int dur = parseValue(cmd, "D:");

    if (m1 < 0 || m2 < 0 || dur < 0)
    {
        Serial.println("[SignSong] 파싱 실패: " + cmd);
        return;
    }

    Serial.printf("[SignSong] 수신: M1=%3d  M2=%3d  D=%dms\n", m1, m2, dur);
    vibrateMotors(m1, m2, dur);
}

// 논블로킹 모터 진동: 세기 설정 후 타이머로 종료
void vibrateMotors(int m1, int m2, int durationMs)
{
    ledcWrite(LEDC_CH_LEFT,  constrain(m1, 0, 255));
    ledcWrite(LEDC_CH_RIGHT, constrain(m2, 0, 255));
    motorOffTime = millis() + durationMs;
    motorActive  = true;
}

// "KEY:value" 에서 value 파싱, 실패 시 -1 반환
int parseValue(String cmd, String key)
{
    int idx = cmd.indexOf(key);
    if (idx == -1) return -1;

    int start = idx + key.length();
    int end   = cmd.indexOf(',', start);
    if (end == -1) end = cmd.length();

    String valStr = cmd.substring(start, end);
    valStr.trim();
    return valStr.toInt();
}
