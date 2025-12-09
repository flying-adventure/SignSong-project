import numpy as np
import matplotlib.pyplot as plt

# 1. 파일 다시 로드
data_ㄱ = np.load("my_data_hb/ㅊ_hb.npy")
data_ㄴ = np.load("my_data_hb/ㅓ_hb.npy")
data_ㄷ = np.load("my_data_hb/ㅕ_hb.npy")

# 손가락 연결 순서
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),   # 엄지
    (0, 5), (5, 6), (6, 7), (7, 8),   # 검지
    (0, 9), (9, 10), (10, 11), (11, 12), # 중지
    (0, 13), (13, 14), (14, 15), (15, 16), # 약지
    (0, 17), (17, 18), (18, 19), (19, 20)  # 소지
]

def plot_random_sample_wide(data, title):
    # 랜덤 샘플 추출
    idx = np.random.randint(0, len(data))
    sequence = data[idx] # (10, 63)
    frame = sequence[-5].reshape(21, 3) # 마지막 프레임 확인
    
    plt.figure(figsize=(6, 6)) # 정사각형 캔버스
    x, y = frame[:, 0], frame[:, 1]
    
    # 점과 선 그리기
    plt.scatter(x, y, c='red', s=30)
    for p1, p2 in HAND_CONNECTIONS:
        plt.plot([x[p1], x[p2]], [y[p1], y[p2]], 'b-', linewidth=1)
        
    # 손목(0,0) 표시
    plt.scatter(x[0], y[0], c='green', s=60, label="Wrist (0,0)")
    
    plt.title(f"{title} (Sample {idx})")
    plt.gca().invert_yaxis() # y축 반전
    plt.grid(True)
    
    # === [수정된 부분] 카메라 줌 아웃 ===
    # 범위를 -3 ~ 3으로 넓게 잡습니다.
    limit = 3.0
    plt.xlim(-limit, limit)
    plt.ylim(limit, -limit) 
    
    # 비율 고정 (손이 찌그러지지 않게)
    plt.gca().set_aspect('equal', adjustable='box')
    plt.legend()
    plt.show()

# 다시 확인
print(f"ㄱ 데이터 개수: {len(data_ㄱ)}")
plot_random_sample_wide(data_ㄱ, "Class 'ㄱ'")

print(f"ㄴ 데이터 개수: {len(data_ㄴ)}")
plot_random_sample_wide(data_ㄴ, "Class 'ㄴ'")

print(f"ㄷ 데이터 개수: {len(data_ㄷ)}")
plot_random_sample_wide(data_ㄷ, "Class 'ㄷ'")