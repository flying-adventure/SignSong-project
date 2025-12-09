import cv2
import mediapipe as mp
import numpy as np
import tensorflow as tf
from PIL import ImageFont, ImageDraw, Image
import os

# ==========================================
# 설정
# ==========================================
SEQ_LEN = 15
NUM_POINTS = 47
FEATURE_DIM = NUM_POINTS * 3

CONF_THRESHOLD = 0.70
MODEL_DIR = "model"

MODEL_PATH = os.path.join(MODEL_DIR, "best_cnn_gru2_model_word_split.keras")
EMBED_MODEL_PATH = os.path.join(MODEL_DIR, "embedding_model_word_split.keras")
CENTROID_PATH = os.path.join(MODEL_DIR, "centroids_word_split.npy")
DIST_THRESHOLD_PATH = os.path.join(MODEL_DIR, "distance_threshold_word_split.npy")
CLASSES_PATH = os.path.join(MODEL_DIR, "classes_word.npy")

FACE_IDXS = [1, 33, 263, 61, 291]

# Voting 파라미터
VOTING_WINDOW = 5
MIN_VOTES = 3
vote_history = []


# ==========================================
# 모델 로드
# ==========================================
print("모델 로드 중...")

model = tf.keras.models.load_model(MODEL_PATH)
embed_model = tf.keras.models.load_model(EMBED_MODEL_PATH)
centroids = np.load(CENTROID_PATH, allow_pickle=True).item()
distance_threshold = float(np.load(DIST_THRESHOLD_PATH))
class_names = np.load(CLASSES_PATH, allow_pickle=True)

print("모델 / 임베딩 / 센트로이드 / 임계값 / 클래스 로딩 완료!")


# ==========================================
# 정규화 함수
# ==========================================
def normalize_features(left, right, face):
    if face is None:
        return np.zeros(FEATURE_DIM, dtype=np.float32)

    nose = face[0]
    face_width = np.linalg.norm(face[1] - face[2])
    if face_width < 1e-6:
        face_width = 1e-6

    def norm(p):
        return (p - nose) / face_width

    left = np.zeros((21, 3)) if left is None else norm(left)
    right = np.zeros((21, 3)) if right is None else norm(right)
    face = norm(face)

    return np.concatenate([left.flatten(), right.flatten(), face.flatten()])


# ==========================================
# OOD 판정
# ==========================================
def predict_with_ood(x):
    soft = model.predict(x, verbose=0)[0]
    prob = float(np.max(soft))
    idx = int(np.argmax(soft))

    if prob < CONF_THRESHOLD:
        return None

    emb = embed_model.predict(x, verbose=0)[0]
    dist = float(np.linalg.norm(emb - centroids[idx]))

    if dist > distance_threshold:
        return None

    return class_names[idx]


# ==========================================
# 한글 출력
# ==========================================
def putText_korean(img, text, position, font_size=30, color=(0,255,0)):
    img_pil = Image.fromarray(img)
    draw = ImageDraw.Draw(img_pil)
    try:
        font = ImageFont.truetype("malgun.ttf", font_size)
    except:
        try: font = ImageFont.truetype("AppleGothic.ttf", font_size)
        except: font = ImageFont.load_default()
    draw.text(position, text, font=font, fill=color)
    return np.array(img_pil)


# ==========================================
# MediaPipe 설정
# ==========================================
mp_holistic = mp.solutions.holistic
holistic = mp_holistic.Holistic(
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)
mp_draw = mp.solutions.drawing_utils


# ==========================================
# 🎥 실시간 테스트
# ==========================================
cap = cv2.VideoCapture(0)
sequence = []
locked = None
last_face = None

print("단어 수어 테스트 시작! (종료: q)")

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    frame = cv2.flip(frame, 1)
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = holistic.process(rgb)

    display = "수어를 보여주세요."
    color = (200, 200, 200)

    left_lms = results.left_hand_landmarks
    right_lms = results.right_hand_landmarks
    face_lms = results.face_landmarks

    # 얼굴 5개 좌표
    if face_lms is not None:
        try:
            face = np.array([[face_lms.landmark[i].x,
                              face_lms.landmark[i].y,
                              face_lms.landmark[i].z]
                              for i in FACE_IDXS])
            last_face = face
        except:
            face = last_face
    else:
        face = last_face

    left = None
    right = None

    if left_lms:
        left = np.array([[lm.x, lm.y, lm.z] for lm in left_lms.landmark])
        mp_draw.draw_landmarks(frame, left_lms, mp_holistic.HAND_CONNECTIONS)

    if right_lms:
        right = np.array([[lm.x, lm.y, lm.z] for lm in right_lms.landmark])
        mp_draw.draw_landmarks(frame, right_lms, mp_holistic.HAND_CONNECTIONS)

    if face_lms:
        mp_draw.draw_landmarks(frame, face_lms, mp_holistic.FACEMESH_TESSELATION)

    # 입력 가능 시
    if face is not None and (left is not None or right is not None):

        vec = normalize_features(left, right, face)
        sequence.append(vec)
        if len(sequence) > SEQ_LEN:
            sequence.pop(0)

        # 🔒 Lock 상태면 무조건 그 단어 표시
        if locked is not None:
            display = locked
            color = (0,255,0)
        else:
            # 15프레임 쌓였으면 모델 입력
            if len(sequence) == SEQ_LEN:

                x = np.expand_dims(np.array(sequence), 0)

                pred = predict_with_ood(x)

                if pred is None:
                    display = "Reject"
                    color = (0,255,255)

                else:
                    # ---------------------------
                    # Voting 적용
                    # ---------------------------
                    vote_history.append(pred)
                    if len(vote_history) > VOTING_WINDOW:
                        vote_history.pop(0)

                    # 최빈값 체크
                    final = max(set(vote_history), key=vote_history.count)
                    votes = vote_history.count(final)

                    # Lock 조건 충족
                    if votes >= MIN_VOTES:
                        locked = final
                        display = locked
                        color = (0,255,0)
                    else:
                        display = f"분석중... ({votes}/{MIN_VOTES})"
                        color = (200,200,0)

    else:
        # 손/얼굴 사라질 때
        sequence = []
        vote_history = []
        locked = None

    # UI 출력
    cv2.rectangle(frame, (0,0), (640,60), (0,0,0), -1)
    frame = putText_korean(frame, display, (20,15), 30, color)

    cv2.imshow("Word Sign Recognition", frame)
    if cv2.waitKey(1) & 0xFF == ord("q"):
        break

cap.release()
cv2.destroyAllWindows()
