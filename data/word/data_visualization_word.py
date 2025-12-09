import numpy as np
import matplotlib.pyplot as plt
import os

# ------------------------------
# 손가락 연결 정보
# ------------------------------
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (0, 9), (9, 10), (10, 11), (11, 12),
    (0, 13), (13, 14), (14, 15), (15, 16),
    (0, 17), (17, 18), (18, 19), (19, 20),
]


# ------------------------------
# 전체(왼손+오른손+얼굴) 한 프레임 그리기
# ------------------------------
def draw_full_pose(ax, left, right, face):
    # 왼손
    x, y = left[:,0], left[:,1]
    ax.scatter(x, y, c='red', s=20, label="Left Hand")
    for p1, p2 in HAND_CONNECTIONS:
        ax.plot([x[p1], x[p2]], [y[p1], y[p2]], 'r-', linewidth=1)

    # 오른손
    x2, y2 = right[:,0], right[:,1]
    ax.scatter(x2, y2, c='blue', s=20, label="Right Hand")
    for p1, p2 in HAND_CONNECTIONS:
        ax.plot([x2[p1], x2[p2]], [y2[p1], y2[p2]], 'b-', linewidth=1)

    # 얼굴 (점만)
    xf, yf = face[:,0], face[:,1]
    ax.scatter(xf, yf, c='purple', s=40, marker='o', label="Face")

    # 전체 영역 설정
    ax.set_xlim(-3, 3)
    ax.set_ylim(3, -3)
    ax.set_aspect('equal')
    ax.grid(True)
    ax.legend(loc="upper right")


# ------------------------------
# 시퀀스 애니메이션
# ------------------------------
def visualize_sequence(sequence, title="Word Sequence", fps=6):
    delay = 1 / fps
    seq_len = len(sequence)

    plt.ion()
    fig, ax = plt.subplots(figsize=(6, 6))

    for t in range(seq_len):
        frame = sequence[t]  # shape = (141,)

        # ------------------
        # 141차원 → 분리
        # ------------------
        left  = frame[0:63].reshape(21,3)
        right = frame[63:126].reshape(21,3)
        face  = frame[126:141].reshape(5,3)

        ax.clear()
        draw_full_pose(ax, left, right, face)
        plt.title(f"{title} — Frame {t+1}/{seq_len}")

        plt.pause(delay)

    plt.ioff()
    plt.show()


# ------------------------------
# 파일 선택 및 실행
# ------------------------------
DATA_DIR = "word_data"

files = [f for f in os.listdir(DATA_DIR) if f.endswith(".npy")]

print("\n사용할 데이터 선택:")
for i, f in enumerate(files):
    print(f"{i}. {f}")

choice = int(input("\n▶ 파일 번호 입력: "))
filename = os.path.join(DATA_DIR, files[choice])

print(f"\n선택된 파일: {filename}")

data = np.load(filename)
print(f"데이터 개수: {len(data)}, shape: {data.shape}")

idx = np.random.randint(0, len(data))
sequence = data[idx]

print(f"\n▶ 시퀀스 {idx} 재생...\n")

visualize_sequence(sequence, title=files[choice])
