import cv2
import mediapipe as mp
import numpy as np
import os

# ==========================================
# 설정 (팀원들이 여기만 수정하면 됨)
# ==========================================
TARGET_CLASS = "테스트"       # 지금 녹화할 지화 이름 (예: ㄱ, ㄴ, j ...)
USER_NAME = "hb"     # 팀원 이름 (파일 겹침 방지용)
SAVE_DIR = "my_data"      # 저장할 폴더명

SEQ_LEN = 15              # 시퀀스 길이
VALID_Y_LIMIT = 0.9       # 손목이 이 선보다 위에 있어야 녹화됨 (0~1)
# ==========================================

if not os.path.exists(SAVE_DIR):
    os.makedirs(SAVE_DIR)

# 미디어파이프 설정
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(
    max_num_hands=1,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)
mp_drawing = mp.solutions.drawing_utils

# 정규화 함수 (우리가 만든 로직 그대로)
def normalize_landmarks(landmarks):
    data = np.array(landmarks)
    wrist = data[0]
    data = data - wrist
    scale = np.linalg.norm(data[9]) # 중지 뿌리 기준
    if scale < 1e-6: scale = 1e-6
    data = data / scale
    return data.flatten()

# 웹캠 실행
cap = cv2.VideoCapture(0)

data_buffer = []   # 현재 동작 프레임 모음
all_sequences = [] # 저장될 전체 시퀀스들
is_recording = False

print(f"=== [{TARGET_CLASS}] 데이터 수집 시작 ===")
print("손을 화면 중앙 박스 안에 넣으면 녹화가 시작됩니다.")
print("종료하려면 'q'를 누르세요.")

while cap.isOpened():
    ret, frame = cap.read()
    if not ret: break

    # 거울 모드 (좌우 반전)
    frame = cv2.flip(frame, 1)
    h, w, c = frame.shape
    
    # 색상 변환
    img_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    result = hands.process(img_rgb)
    
    # 화면 가이드라인 그리기 (녹화 영역)
    cv2.line(frame, (0, int(h * VALID_Y_LIMIT)), (w, int(h * VALID_Y_LIMIT)), (0, 255, 0), 2)
    cv2.putText(frame, "Limit Line", (10, int(h * VALID_Y_LIMIT) - 10), 
                cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)

    valid_frame = False

    if result.multi_hand_landmarks:
        hand_landmarks = result.multi_hand_landmarks[0]
        
        # 랜드마크 그리기
        mp_drawing.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)
        
        # 좌표 추출
        lm_list = [[lm.x, lm.y, lm.z] for lm in hand_landmarks.landmark]
        wrist_y = lm_list[0][1]
        
        # 손이 라인보다 위에 있으면 녹화!
        if wrist_y < VALID_Y_LIMIT:
            valid_frame = True
            norm_vec = normalize_landmarks(lm_list)
            data_buffer.append(norm_vec)
            
            # 녹화 중 표시
            cv2.circle(frame, (30, 30), 15, (0, 0, 255), -1) # 빨간불
            cv2.putText(frame, f"REC ({len(data_buffer)})", (55, 40), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
            is_recording = True
    
    # 손이 내려갔거나 사라지면 -> 동작 끊기
    if not valid_frame and is_recording:
        if len(data_buffer) >= SEQ_LEN:
            # 시퀀스 생성 (슬라이딩 윈도우)
            for i in range(len(data_buffer) - SEQ_LEN + 1):
                all_sequences.append(data_buffer[i : i+SEQ_LEN])
            print(f"✅ 동작 저장됨! (현재 누적: {len(all_sequences)}개)")
        else:
            print("⚠️ 동작이 너무 짧아서 버려짐")
            
        data_buffer = [] # 버퍼 초기화
        is_recording = False

    # 상태 표시
    cv2.putText(frame, f"Class: {TARGET_CLASS}", (10, h-20), 
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
    cv2.putText(frame, f"Collected: {len(all_sequences)}", (w-200, h-20), 
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 0), 2)

    cv2.imshow('Sign Language Collector', frame)
    
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()

# === 저장 ===
if len(all_sequences) > 0:
    # 파일명: 클래스_이름.npy
    filename = f"{TARGET_CLASS}_{USER_NAME}.npy"
    save_path = os.path.join(SAVE_DIR, filename)
    np.save(save_path, np.array(all_sequences))
    print(f"\n🎉 저장 완료: {save_path}")
    print(f"데이터 형태: {np.array(all_sequences).shape}")
else:
    print("\n수집된 데이터가 없습니다.")