import cv2
import mediapipe as mp
import numpy as np
import os

# ==========================================
# 설정
# ==========================================
TARGET_CLASS = "집"
USER_NAME = "hb"
SAVE_DIR = "word_data"

SEQ_LEN = 15
VALID_Y_LIMIT = 0.9

FACE_LMS = [1, 33, 263, 61, 291]  # 얼굴 5포인트

# ==========================================
# 디렉토리 생성
# ==========================================
os.makedirs(SAVE_DIR, exist_ok=True)

# ==========================================
# MediaPipe Holistic
# ==========================================
mp_holistic = mp.solutions.holistic
holistic = mp_holistic.Holistic(
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)
mp_drawing = mp.solutions.drawing_utils

# ==========================================
# 정규화 함수
# ==========================================
def normalize_features(left_hand, right_hand, face):
    """left_hand / right_hand 가 None이면 자동으로 0으로 채움."""
    if face is None:
        return np.zeros(141, dtype=np.float32)

    nose = face[0]
    face_width = np.linalg.norm(face[1] - face[2])
    if face_width < 1e-6:
        face_width = 1e-6

    def norm(x):
        return (x - nose) / face_width

    # 손이 None이면 0채움
    if left_hand is None:
        left_hand = np.zeros((21, 3))
    else:
        left_hand = norm(left_hand)

    if right_hand is None:
        right_hand = np.zeros((21, 3))
    else:
        right_hand = norm(right_hand)

    face = norm(face)

    return np.concatenate([
        left_hand.flatten(),
        right_hand.flatten(),
        face.flatten()
    ], axis=0)


# ==========================================
# 웹캠 준비
# ==========================================
cap = cv2.VideoCapture(0)

data_buffer = []
all_sequences = []
is_recording = False

last_face = None  # 얼굴이 가려질 경우 대비 저장

print(f"=== 수어 단어 [{TARGET_CLASS}] 데이터 수집 시작 ===")
print("✔ 한 손만 있어도 녹화됩니다.")
print("✔ 두 손이 모두 사라지면 시퀀스가 저장됩니다.")
print("✔ 얼굴이 잠깐 가려져도 녹화가 끊기지 않습니다.")
print("종료: q")

# ==========================================
# 메인 루프
# ==========================================
while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    frame = cv2.flip(frame, 1)
    h, w, _ = frame.shape

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = holistic.process(rgb)

    # 녹화 라인
    cv2.line(frame, (0, int(h * VALID_Y_LIMIT)), (w, int(h * VALID_Y_LIMIT)), (0, 255, 0), 2)

    # 랜드마크 참조
    left_lms = results.left_hand_landmarks
    right_lms = results.right_hand_landmarks
    face_lms = results.face_landmarks

    # ===== 얼굴 처리 (가려져도 끊기지 않도록 last_face 유지) =====
    if face_lms is not None:
        try:
            face = np.array([
                [face_lms.landmark[i].x,
                 face_lms.landmark[i].y,
                 face_lms.landmark[i].z] 
                for i in FACE_LMS
            ])
            last_face = face  # 정상 업데이트
        except:
            face = last_face if last_face is not None else None
    else:
        face = last_face if last_face is not None else None

    # ===== 손 인식 여부 =====
    left_present = left_lms is not None
    right_present = right_lms is not None
    hand_present = left_present or right_present

    # ===== 녹화 가능한 프레임인지 =====
    valid_frame = False

    if face is not None and hand_present:

        # 손 좌표 추출
        left_hand = np.array([[lm.x, lm.y, lm.z] for lm in left_lms.landmark]) if left_present else None
        right_hand = np.array([[lm.x, lm.y, lm.z] for lm in right_lms.landmark]) if right_present else None

        # 기준 손목 y좌표 (왼손 > 오른손 순)
        if left_present:
            wrist_y = left_hand[0, 1]
        else:
            wrist_y = right_hand[0, 1]

        # ---- 녹화 조건 ----
        if wrist_y < VALID_Y_LIMIT:
            valid_frame = True
            vec = normalize_features(left_hand, right_hand, face)
            data_buffer.append(vec)
            is_recording = True

            cv2.circle(frame, (30, 30), 15, (0, 0, 255), -1)
            cv2.putText(frame, f"REC ({len(data_buffer)})", (55, 40),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0,0,255), 2)

        # ---- 랜드마크 표시 ----
        if left_present:
            mp_drawing.draw_landmarks(frame, left_lms, mp_holistic.HAND_CONNECTIONS)
        if right_present:
            mp_drawing.draw_landmarks(frame, right_lms, mp_holistic.HAND_CONNECTIONS)
        if face_lms:
            mp_drawing.draw_landmarks(frame, face_lms, mp_holistic.FACEMESH_TESSELATION)

    # ===== 저장 조건: 녹화 중 + 두 손 모두 없음 =====
    if is_recording and not hand_present:

        if len(data_buffer) >= SEQ_LEN:
            for i in range(len(data_buffer) - SEQ_LEN + 1):
                all_sequences.append(data_buffer[i:i+SEQ_LEN])
            print(f"시퀀스 저장됨 (누적: {len(all_sequences)})")
        else:
            print("동작 너무 짧아서 버려짐")

        data_buffer = []
        is_recording = False

    # UI 표시
    cv2.putText(frame, f"Class: {TARGET_CLASS}", (10, h - 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255,255,255), 2)
    cv2.putText(frame, f"Collected: {len(all_sequences)}", (w - 200, h - 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255,255,0), 2)

    cv2.imshow("Collector", frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()

# ==========================================
# 저장
# ==========================================
if all_sequences:
    filename = f"{TARGET_CLASS}_{USER_NAME}.npy"
    np.save(os.path.join(SAVE_DIR, filename), np.array(all_sequences))
    print(f"\n저장 완료 → {SAVE_DIR}/{filename}")
else:
    print("\n수집된 데이터 없음")
