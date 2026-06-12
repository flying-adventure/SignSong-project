// SerialPort는 .NET Framework 또는 .NET Standard 2.1 + NuGet(System.IO.Ports) 에서만 사용 가능
// API 호환성이 .NET Standard 2.1 이하일 경우 SERIAL_PORT_AVAILABLE 심볼 없이 컴파일되어
// 터미널 출력 전용 시뮬레이션 모드로 동작
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
#define SERIAL_PORT_AVAILABLE
#endif

using System;
using UnityEngine;
#if SERIAL_PORT_AVAILABLE
using System.IO.Ports;
#endif

public class GloveBluetoothSender : MonoBehaviour
{
    [SerializeField] private string portName = "COM5";
    [SerializeField] private int baudRate = 9600;

#if SERIAL_PORT_AVAILABLE
    private SerialPort serialPort;
    public bool IsConnected => serialPort != null && serialPort.IsOpen;

    public void Connect()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout  = 100,
                WriteTimeout = 100
            };
            serialPort.Open();
            Debug.Log($"[GloveBluetoothSender] {portName} 연결 성공");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GloveBluetoothSender] 연결 실패 ({portName}): {e.Message}\n→ 터미널 출력 전용 모드로 진행");
        }
    }

    // ESP32로 전송 포맷: "M1:{motor1},M2:{motor2},D:{durationMs}\n"
    public void Send(int motor1, int motor2, int durationMs)
    {
        if (!IsConnected) return;
        try
        {
            serialPort.Write($"M1:{motor1},M2:{motor2},D:{durationMs}\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GloveBluetoothSender] 전송 실패: {e.Message}");
        }
    }

    public void Disconnect()
    {
        if (IsConnected)
        {
            serialPort.Close();
            Debug.Log("[GloveBluetoothSender] 연결 해제");
        }
    }

    void OnDestroy()         => Disconnect();
    void OnApplicationQuit() => Disconnect();

#else
    // SerialPort 미지원 환경 — 컴파일 에러 없이 스텁으로 동작
    public bool IsConnected => false;
    public void Connect()    => Debug.Log("[GloveBluetoothSender] 시뮬레이션 모드 (SerialPort 미지원 환경)");
    public void Send(int motor1, int motor2, int durationMs) { }
    public void Disconnect() { }
#endif
}
