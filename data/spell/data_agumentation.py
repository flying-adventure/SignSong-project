import os
import glob
import numpy as np
import math
from scipy.interpolate import interp1d

# ==========================================
# ⚙️ 설정
# ==========================================
INPUT_FOLDER = "./collected_data" 
OUTPUT_FOLDER = "./final_data"

# 증강 비율 (전체 데이터 대비 %)
RATIO_NOISE = 0.3   # 노이즈 (30%)
RATIO_ROTATE = 0.3  # 회전 (30%)
RATIO_SCALE = 0.2   # 스케일링 (20%)
RATIO_FAST = 0.2    # 고속 (20%)
RATIO_SLOW = 0.2    # 저속 (20%)
RATIO_MASK = 0.1    # 마스킹 (10%)

# 클래스 목록
CLASS_NAMES = [
    "ㄱ", "ㄴ", "ㄷ", "ㄹ", "ㅁ", "ㅂ", "ㅅ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ",
    "ㄲ", "ㄸ", "ㅃ", "ㅆ", "ㅉ",
    "ㅏ", "ㅐ", "ㅑ", "ㅒ", "ㅓ", "ㅔ", "ㅕ", "ㅖ", 
    "ㅗ", "ㅘ", "ㅙ", "ㅚ", "ㅛ", 
    "ㅜ", "ㅝ", "ㅞ", "ㅟ", "ㅠ", 
    "ㅡ", "ㅢ", "ㅣ"
]

if not os.path.exists(OUTPUT_FOLDER):
    os.makedirs(OUTPUT_FOLDER)

class_to_idx = {c: i for i, c in enumerate(CLASS_NAMES)}

# ==========================================
# 🔧 증강 함수들 (이전과 동일)
# ==========================================
def rotate_landmarks(landmarks, angle_degrees):
    angle_radians = math.radians(angle_degrees)
    cos_val = math.cos(angle_radians)
    sin_val = math.sin(angle_radians)
    reshaped = landmarks.reshape(-1, 21, 3)
    rotated = reshaped.copy()
    rotated[:, :, 0] = reshaped[:, :, 0] * cos_val - reshaped[:, :, 1] * sin_val
    rotated[:, :, 1] = reshaped[:, :, 0] * sin_val + reshaped[:, :, 1] * cos_val
    return rotated.reshape(landmarks.shape)

def modify_speed(sequence, rate):
    seq_len = len(sequence)
    old_time = np.arange(seq_len)
    new_time = np.linspace(0, seq_len - 1, num=int(seq_len / rate))
    f = interp1d(old_time, sequence, kind='linear', axis=0, fill_value="extrapolate")
    warped_sequence = f(new_time)
    if len(warped_sequence) > seq_len:
        return warped_sequence[:seq_len]
    elif len(warped_sequence) < seq_len:
        padding = np.tile(warped_sequence[-1], (seq_len - len(warped_sequence), 1))
        return np.vstack((warped_sequence, padding))
    else:
        return warped_sequence

def scale_landmarks(landmarks, scale_factor):
    mean = np.mean(landmarks, axis=1, keepdims=True)
    return (landmarks - mean) * scale_factor + mean

def mask_landmarks(landmarks, num_mask=1):
    masked = landmarks.copy()
    seq_len, num_feats = masked.shape
    masked_reshaped = masked.reshape(seq_len, 21, 3)
    mask_indices = np.random.choice(21, num_mask, replace=False)
    masked_reshaped[:, mask_indices, :] = 0
    return masked_reshaped.reshape(seq_len, num_feats)

# ==========================================
# 🎲 클래스별 일괄 증강 함수
# ==========================================
def apply_augmentation_per_class(class_name, X_data):
    n_samples = len(X_data)
    aug_X_list = []
    
    print(f"   👉 Class '{class_name}': 원본 {n_samples}개 -> 증강 시작...", end="")

    # 1. 노이즈
    n = int(n_samples * RATIO_NOISE)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            noise = np.random.normal(0, 0.01, X_data[i].shape)
            aug_X_list.append(X_data[i] + noise)

    # 2. 회전
    n = int(n_samples * RATIO_ROTATE)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            angle = np.random.uniform(-5, 5)
            aug_X_list.append(rotate_landmarks(X_data[i], angle))
            
    # 3. 스케일링
    n = int(n_samples * RATIO_SCALE)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            scale = np.random.uniform(0.9, 1.1)
            aug_X_list.append(scale_landmarks(X_data[i], scale))

    # 4. 고속
    n = int(n_samples * RATIO_FAST)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            aug_X_list.append(modify_speed(X_data[i], 1.1))

    # 5. 저속
    n = int(n_samples * RATIO_SLOW)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            aug_X_list.append(modify_speed(X_data[i], 0.9))
            
    # 6. 마스킹
    n = int(n_samples * RATIO_MASK)
    if n > 0:
        idx = np.random.choice(n_samples, n, replace=False)
        for i in idx:
            aug_X_list.append(mask_landmarks(X_data[i], 1))

    print(f" +{len(aug_X_list)}개 추가됨")
    
    if len(aug_X_list) == 0:
        return np.array([])
    return np.array(aug_X_list)

# ==========================================
# 🚀 메인 실행 로직
# ==========================================
# 1. 데이터를 클래스별로 모으기
class_data_storage = {name: [] for name in CLASS_NAMES}

npy_files = glob.glob(os.path.join(INPUT_FOLDER, "*.npy"))
print(f"📂 파일 로딩 시작 ({len(npy_files)}개 파일)...")

for f in npy_files:
    filename = os.path.basename(f)
    try:
        label_name = filename.split('_')[0]
    except IndexError:
        continue
    
    if label_name in class_to_idx:
        data = np.load(f)
        class_data_storage[label_name].extend(data)

print("✅ 모든 파일 로드 완료! 이제 클래스별로 증강합니다.\n")

# 2. 클래스별 증강 및 통합
final_X = []
final_y = []

total_original = 0
total_augmented = 0

for class_name in CLASS_NAMES:
    X_origin = np.array(class_data_storage[class_name])
    
    if len(X_origin) == 0:
        print(f"⚠️ Class '{class_name}' 데이터가 없습니다. 건너뜁니다.")
        continue
        
    # 원본 데이터 추가
    label_idx = class_to_idx[class_name]
    final_X.extend(X_origin)
    final_y.extend([label_idx] * len(X_origin))
    total_original += len(X_origin)
    
    # 증강 데이터 생성 및 추가
    X_aug = apply_augmentation_per_class(class_name, X_origin)
    
    if len(X_aug) > 0:
        final_X.extend(X_aug)
        final_y.extend([label_idx] * len(X_aug))
        total_augmented += len(X_aug)

# 3. 최종 저장
X_final = np.array(final_X)
y_final = np.array(final_y)

if len(X_final) > 0:
    print("\n" + "="*40)
    print("📊 최종 데이터셋 통계")
    print("="*40)
    print(f"1. 순수 원본 데이터: {total_original}개")
    print(f"2. 증강된 데이터   : {total_augmented}개")
    print(f"3. 최종 합계       : {len(X_final)}개")
    
    multiplier = len(X_final) / total_original
    print(f"👉 총 배율: 원본의 {multiplier:.1f}배")
    
    np.save(os.path.join(OUTPUT_FOLDER, 'X_data_seq.npy'), X_final)
    np.save(os.path.join(OUTPUT_FOLDER, 'y_data_seq.npy'), y_final)
    np.save(os.path.join(OUTPUT_FOLDER, 'classes.npy'), np.array(CLASS_NAMES))
    
    print(f"\n🎉 저장 완료: {OUTPUT_FOLDER}")
else:
    print("\n❌ 데이터가 없습니다.")