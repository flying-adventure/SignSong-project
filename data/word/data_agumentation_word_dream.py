import os
import glob
import numpy as np
import math
from scipy.interpolate import interp1d
import unicodedata
from sklearn.model_selection import train_test_split
import random

# ==========================================
# 설정
# ==========================================
INPUT_FOLDER = "./collected_data_word"
OUTPUT_FOLDER = "./final_data_word"

RATIO_NOISE = 0.3
RATIO_ROTATE = 0.3
RATIO_SCALE = 0.2
RATIO_FAST = 0.2
RATIO_SLOW = 0.2
RATIO_MASK = 0.1

SEQ_LEN = 15
NUM_POINTS = 47
FEATURE_DIM = NUM_POINTS * 3

# ==========================================
# 단어 클래스
# ==========================================
CLASS_NAMES = [
    "내게", "꿈", "사랑", "희망", "맑다", "공기", "꽃", "곧다", "좋다",
    "나무", "세상", "새", "노래", "신나다", "온통"
]

CLASS_NAMES = [unicodedata.normalize("NFC", c) for c in CLASS_NAMES]
CLASS_NAMES = sorted(list(dict.fromkeys(CLASS_NAMES)))
class_to_idx = {c: i for i, c in enumerate(CLASS_NAMES)}

if not os.path.exists(OUTPUT_FOLDER):
    os.makedirs(OUTPUT_FOLDER)

# ==========================================
# 증강 함수들
# ==========================================
def rotate_landmarks(l, angle):
    rad = math.radians(angle)
    c, s = math.cos(rad), math.sin(rad)
    r = l.reshape(SEQ_LEN, NUM_POINTS, 3)
    x = r[:, :, 0]; y = r[:, :, 1]
    r[:, :, 0] = x * c - y * s
    r[:, :, 1] = x * s + y * c
    return r.reshape(SEQ_LEN, FEATURE_DIM)

def modify_speed(seq, rate):
    old = np.arange(len(seq))
    new = np.linspace(0, len(seq)-1, num=int(len(seq)/rate))
    f = interp1d(old, seq, axis=0, fill_value="extrapolate")
    warped = f(new)
    if len(warped) >= len(seq):
        return warped[:len(seq)]
    pad = np.tile(warped[-1], (len(seq)-len(warped), 1))
    return np.vstack([warped, pad])

def scale_landmarks(l, s):
    r = l.reshape(SEQ_LEN, NUM_POINTS, 3)
    c = np.mean(r, axis=1, keepdims=True)
    return ((r - c) * s + c).reshape(SEQ_LEN, FEATURE_DIM)

def mask_landmarks(l, num_mask=2):
    r = l.reshape(SEQ_LEN, NUM_POINTS, 3).copy()
    idx = np.random.choice(NUM_POINTS, num_mask, replace=False)
    r[:, idx, :] = 0
    return r.reshape(SEQ_LEN, FEATURE_DIM)

def apply_augmentation(label, X):
    n = len(X)
    aug = []

    for i in np.random.choice(n, int(n * RATIO_NOISE), replace=False):
        aug.append(X[i] + np.random.normal(0, 0.01, X[i].shape))

    for i in np.random.choice(n, int(n * RATIO_ROTATE), replace=False):
        aug.append(rotate_landmarks(X[i], np.random.uniform(-5, 5)))

    for i in np.random.choice(n, int(n * RATIO_SCALE), replace=False):
        aug.append(scale_landmarks(X[i], np.random.uniform(0.9, 1.1)))

    for i in np.random.choice(n, int(n * RATIO_FAST), replace=False):
        aug.append(modify_speed(X[i], 1.1))

    for i in np.random.choice(n, int(n * RATIO_SLOW), replace=False):
        aug.append(modify_speed(X[i], 0.9))

    for i in np.random.choice(n, int(n * RATIO_MASK), replace=False):
        aug.append(mask_landmarks(X[i], 2))

    return np.array(aug)


# ==========================================
# 파일 로딩: 클래스별 → 사용자별 정리
# ==========================================
class_user_files = {c: {} for c in CLASS_NAMES}

npy_files = glob.glob(os.path.join(INPUT_FOLDER, "*.npy"))

for f in npy_files:
    fname = os.path.basename(f)
    label_raw = fname.split("_")[0]
    label = unicodedata.normalize("NFC", label_raw)

    if label not in class_to_idx:
        continue

    user_id = fname.split("_")[1].split(".")[0]

    if user_id not in class_user_files[label]:
        class_user_files[label][user_id] = []

    class_user_files[label][user_id].append(f)

# ==========================================
# 클래스별로 사용자 랜덤 선택하여 split 수행
# ==========================================
X_train, y_train = [], []
X_val, y_val = [], []
X_test, y_test = [], []


for cls in CLASS_NAMES:
    users = list(class_user_files[cls].keys())

    if len(users) < 2:
        print(f"'{cls}' 클래스는 사용자 수 부족 → 전체 TRAIN 사용")
        # 원본만 넣음
        for u in users:
            for fp in class_user_files[cls][u]:
                data = np.load(fp)
                label_idx = class_to_idx[cls]
                X_train.extend(list(data))
                y_train.extend([label_idx]*len(data))
        continue

    # -------------------------------
    # ✔ 해당 클래스에서 1명 랜덤 선택 → val/test 전용
    # -------------------------------
    val_test_user = random.choice(users)
    train_users = [u for u in users if u != val_test_user]

    print(f"[{cls}] → 검증/테스트 사용자: {val_test_user}, 학습 사용자: {train_users}")

    label_idx = class_to_idx[cls]

    # -------------------------------
    # TRAIN USERS → 원본 + 증강
    # -------------------------------
    for u in train_users:
        for fp in class_user_files[cls][u]:
            data = np.load(fp)
            X_train.extend(list(data))
            y_train.extend([label_idx]*len(data))

            aug = apply_augmentation(cls, data)
            X_train.extend(list(aug))
            y_train.extend([label_idx]*len(aug))

    # -------------------------------
    # VAL/TEST USER → 원본만
    # -------------------------------
    val_test_data = []
    for fp in class_user_files[cls][val_test_user]:
        data = np.load(fp)
        val_test_data.extend(list(data))

    val_test_data = np.array(val_test_data)
    y_vals = np.array([label_idx]*len(val_test_data))

    # VAL / TEST split
    Xv, Xt, yv, yt = train_test_split(
        val_test_data,
        y_vals,
        test_size=0.5,
        random_state=42,
        stratify=y_vals
    )

    X_val.extend(list(Xv))
    y_val.extend(list(yv))
    X_test.extend(list(Xt))
    y_test.extend(list(yt))

# ==========================================
# 배열 변환 후 저장
# ==========================================
X_train = np.array(X_train)
y_train = np.array(y_train)
X_val = np.array(X_val)
y_val = np.array(y_val)
X_test = np.array(X_test)
y_test = np.array(y_test)

np.save(os.path.join(OUTPUT_FOLDER, "X_train_dream.npy"), X_train)
np.save(os.path.join(OUTPUT_FOLDER, "y_train_dream.npy"), y_train)
np.save(os.path.join(OUTPUT_FOLDER, "X_val_dream.npy"), X_val)
np.save(os.path.join(OUTPUT_FOLDER, "y_val_dream.npy"), y_val)
np.save(os.path.join(OUTPUT_FOLDER, "X_test_dream.npy"), X_test)
np.save(os.path.join(OUTPUT_FOLDER, "y_test_dream.npy"), y_test)
np.save(os.path.join(OUTPUT_FOLDER, "classes_dream.npy"), np.array(CLASS_NAMES, dtype=object))

print("\n모든 저장 완료!")
print("TRAIN:", X_train.shape)
print("VAL:", X_val.shape)
print("TEST:", X_test.shape)
