import cv2
import mediapipe as mp
import numpy as np
import tensorflow as tf
from PIL import ImageFont, ImageDraw, Image

# ==========================================
# 설정
# ==========================================
SEQ_LEN = 15
CONF_THRESHOLD = 0.60   # softmax 기준
DIST_THRESHOLD_PATH = "model/distance_threshold.npy"
CENTROID_PATH = "model/centroids.npy"
MODEL_PATH = "model/best_cnn_gru_model.keras"
EMBED_MODEL_PATH = "model/embedding_model.keras"

CLASS_NAMES = [
    "ㄱ","ㄴ","ㄷ","ㄹ","ㅁ","ㅂ","ㅅ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ",
    "ㄲ","ㄸ","ㅃ","ㅆ","ㅉ",
    "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ",
    "ㅗ","ㅘ","ㅙ","ㅚ","ㅛ",
    "ㅜ","ㅝ","ㅞ","ㅟ","ㅠ",
    "ㅡ","ㅢ","ㅣ"
]

# ==========================================
# 파일 로드
# ==========================================
print("모델 로드 중...")
model = tf.keras.models.load_model(MODEL_PATH)
embed_model = tf.keras.models.load_model(EMBED_MODEL_PATH)
centroids = np.load(CENTROID_PATH, allow_pickle=True).item()
distance_threshold = float(np.load(DIST_THRESHOLD_PATH))

print("모델 / 임베딩 / 센트로이드 / 임계값 로드 완료!")

# ==========================================
# 미디어파이프 설정
# ==========================================
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(max_num_hands=1,
                       min_detection_confidence=0.5,
                       min_tracking_confidence=0.5)
mp_drawing = mp.solutions.drawing_utils

# ==========================================
# 전처리 함수
# ==========================================
def normalize_landmarks(landmarks):
    data = np.array(landmarks)
    wrist = data[0]
    data = data - wrist
    scale = np.linalg.norm(data[9])
    if scale < 1e-6: scale = 1e-6
    return (data / scale).flatten()

# ==========================================
# 한글 출력 함수
# ==========================================
def putText_korean(img, text, position, font_size=30, color=(0,255,0)):
    img_pil = Image.fromarray(img)
    draw = ImageDraw.Draw(img_pil)
    try:
        font = ImageFont.truetype("malgun.ttf", font_size)
    except:
        try:
            font = ImageFont.truetype("AppleGothic.ttf", font_size)
        except:
            font = ImageFont.load_default()
    draw.text(position, text, font=font, fill=color)
    return np.array(img_pil)

# ==========================================
# OOD Reject 함수
# ==========================================
def predict_with_ood(x):
    """
    x: (1, SEQ_LEN, 63)
    return: dict
    """
    softmax = model.predict(x, verbose=0)[0]
    max_prob = np.max(softmax)
    pred_idx = np.argmax(softmax)

    # 1) Softmax 기반 Reject
    if max_prob < CONF_THRESHOLD:
        return {"result": "Reject (Low confidence)", "prob": float(max_prob)}

    # 2) Embedding 기반 Reject
    emb = embed_model.predict(x, verbose=0)[0]
    dist = np.linalg.norm(emb - centroids[pred_idx])

    if dist > distance_threshold:
        return {
            "result": "Reject (Far from centroid)",
            "prob": float(max_prob),
            "distance": float(dist)
        }

    return {
        "result": CLASS_NAMES[pred_idx],
        "prob": float(max_prob),
        "distance": float(dist)
    }

# ==========================================
# 메인 루프
# ==========================================
cap = cv2.VideoCapture(0)
sequence = []
locked_result = None

print("웹캠 시작 (종료: q)")

while cap.isOpened():
    ret, frame = cap.read()
    if not ret: break

    frame = cv2.flip(frame, 1)
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(rgb)

    display_text = "손을 올려주세요."
    text_color = (200,200,200)

    if results.multi_hand_landmarks:
        hand = results.multi_hand_landmarks[0]
        mp_drawing.draw_landmarks(frame, hand, mp_hands.HAND_CONNECTIONS)

        # 좌표 추출
        lm = [[lm.x, lm.y, lm.z] for lm in hand.landmark]
        normalized = normalize_landmarks(lm)

        sequence.append(normalized)
        if len(sequence) > SEQ_LEN:
            sequence.pop(0)

        # ★ 이미 Lock 되어 있으면 계속 동일한 결과 유지
        if locked_result is not None:
            display_text = f"{locked_result}"
            text_color = (0,255,0)

        elif len(sequence) == SEQ_LEN:
            x = np.expand_dims(np.array(sequence), 0)

            result = predict_with_ood(x)

            # Reject이면 화면 표시만 하고 lock 안함
            if result["result"].startswith("Reject"):
                display_text = result["result"]
                text_color = (0,255,255)

            else:
                # 정상 지화 → Lock
                locked_result = result["result"]
                display_text = locked_result
                text_color = (0,255,0)

    else:
        sequence = []
        locked_result = None

    # 상단 배경
    cv2.rectangle(frame, (0,0), (640,60), (0,0,0), -1)
    frame = putText_korean(frame, display_text, (20,15), 30, text_color)

    cv2.imshow("Real-Time Sign Recognition", frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
