# Sign Song Unity Project
<img width="1289" height="751" alt="image" src="https://github.com/user-attachments/assets/fb9bff73-2be3-4351-a38e-530c9a92f86f" />


---

# Project Information
### Topic : 청각에 어려움이 있는 사람들도 음악을 즐길 수 있다, 수어 학습을 돕는 모바일 리듬 게임
#### 기획 과정 영상   
[![Video Thumbnail](https://img.youtube.com/vi/ZoLThXx7v3g/0.jpg)](https://youtu.be/ZoLThXx7v3g)

#### 최종 영상
[![Video Thumbnail](https://img.youtube.com/vi/IHCQXCFa4m8/0.jpg)](https://youtu.be/IHCQXCFa4m8)

#### 수어로 변환된 노래가사를 박자에 맞춰 화면에 나오는 가이드 수화를 따라합니다.      
#### 카메라를 통해 실시간으로 동작을 평가하여 점수가 부여됩니다.    

---

### 제작 과정 Flow Chart
<img width="1361" height="306" alt="image" src="https://github.com/user-attachments/assets/a8678227-d2e6-42f8-aa01-d5aeea3d7c8b" />

### 구현 기능 Flow Chart
<img width="1167" height="525" alt="image" src="https://github.com/user-attachments/assets/7632fd64-0a6c-4dea-8904-5c930ecd851f" />

---

## 📌 프로젝트 개요

**Sign Song**은 한국 수어 동작을 게임 입력으로 활용하여, **청각 정보 없이도 시각적 수어 가이드와 촉각적 진동 피드백**을 통해 누구나 음악을 즐길 수 있는 포용적 수어 기반 음악 리듬게임입니다.

### 🎯 핵심 개념
- **시각 입력**: 화면에 제시되는 수어 가이드 애니메이션을 따라 동작 수행
- **카메라 인식**: 웹캠 기반 실시간 손/얼굴 랜드마크 추출
- **AI 판정**: TCN 기반 수어 분류 모델 + OOD 이상치 탐지
- **촉각 피드백**: ESP32 기반 진동장갑으로 음악 리듬을 촉각적으로 전달
- **게임 판정**: Perfect/Good/Miss 3단계 판정 시스템 (타이밍 + 클래스 일치도 기반)

사용자는 곡의 가사와 박자에 맞춰 수어를 수행하면, 시스템이 동작을 실시간으로 분석하여 점수와 판정을 제공합니다. 점수, 콤보, 판정 횟수는 게임 결과에 반영되어 반복 플레이가 자연스럽게 **수어 학습**으로 이어집니다.

---

## 🛠 기술 스택

| 분류 | 내용 |
|------|------|
| 엔진 | Unity 6 (6000.3.10f1) |
| 렌더 파이프라인 | Universal Render Pipeline (URP 17.3.0) |
| 언어 | C# |
| AI 추론 엔진 | Unity AI Inference Engine (Sentis) 2.5.0 |
| 손/얼굴 인식 | MediaPipe Unity Plugin |
| ML 모델 | TCN (Temporal Convolutional Network) |
| 모델 형식 | ONNX / Sentis |
| JSON 파싱 | Newtonsoft.Json 3.2.2 |
| 입력 처리 | Unity Input System 1.18.0 |
| 하드웨어 | ESP32 + 진동모터 (촉각 피드백) |

---

## ✨ 주요 기능

### 1️⃣ 수어 인식 파이프라인

#### 두 가지 입력 모드

| 모드 | 특징 | 차원 | 사용 부위 |
|------|------|------|---------|
| **Spell 모드** | 지화(낱글자) 중심 | 63차원 | 오른손만 (21포인트 × 3) |
| **Word 모드** | 단어형 수어 중심 | 141차원 | 양손(126차원) + 얼굴(15차원) |

#### 인식 프로세스
1. **MediaPipe**로 양손(21포인트 × 2) + 얼굴(5포인트) 랜드마크 실시간 추출
2. 랜드마크를 **141차원 특징 벡터**로 정규화
3. 15프레임 단위로 시퀀스 누적 후 **TCN 모델**에 입력
4. **2단계 OOD(Out-of-Distribution) 필터링**으로 오인식 방지
   - 1단계: 예측 신뢰도(`confidence_threshold`) 미만 시 거부
   - 2단계: 임베딩과 클래스 중심점 간 L2 거리(`distance_threshold`) 초과 시 거부

#### 모델 성능
- **선정 모델**: TCN (Temporal Convolutional Network)
- **Accuracy**: 0.8936
- **Macro F1-Score**: 0.9044
- **모델 파일**: `best_tcn.sentis`, `tcn_embedding_model.sentis`

---

### 2️⃣ 리듬 게임 판정 시스템

#### 노트 기반 채보 시스템
- CSV 차트 파일 기반 노트 타이밍 관리
- 각 노트: `[발생시각, 목표수어, 난이도]` 포함
- 현재 곡: "벚꽃엔딩" (버스커버스커)

#### 3단계 판정 기준
| 판정 | 시간 허용범위 | 점수 |
|------|-------------|------|
| **PERFECT** | ±0.25초 이내 | 100점 |
| **GOOD** | ±0.50초 이내 | 50점 |
| **MISS** | ±0.80초 이내 또는 미인식 | 0점 |

#### 판정 로직
- 예측된 수어 클래스 = 노트 정답 클래스 **AND**
- 예측 시점과 노트 시점의 시간 차이 ≤ 허용범위

---

### 3️⃣ 촉각 피드백 (진동장갑)

#### 하드웨어 구성
- **MCU**: ESP32 Dev Module
- **구동**: 코인형 진동모터 (양손 분산 배치)
- **제어**: MOSFET 스위칭 방식 (안정적 다중 모터 제어)
- **전원**: 3.7V Li-Po 배터리 + TP4056 충전/보호 모듈
- **진동 패턴**: 「벚꽃엔딩」 드럼 리듬 기반 (librosa 분석)

#### 특징
- 배터리 기반 독립 동작 (사용자 움직임 제약 없음)
- 음악의 드럼 리듬을 실시간으로 촉각 정보로 변환
- 좌우 손에 분산된 진동으로 리듬의 강약 및 구간 변화 구분 가능

---

### 4️⃣ 씬 구성 및 게임 흐름

| 씬 이름 | 설명 | 주요 기능 |
|---------|------|---------|
| `Start_main` | 앱 시작 화면 | 게임 진입 포인트 |
| `Login_page` | 로그인 화면 | 사용자 인증 |
| `Sign_list` | 수어 곡 목록 (드롭다운 0번) | 지화 모드 곡 선택 |
| `Dongyo_list` | 동요 곡 목록 (드롭다운 1번) | 동요 모드 곡 선택 |
| `Gayo_list` | 가요 곡 목록 (드롭다운 2번) | 가요 모드 곡 선택 |
| `Gayo_game` | 리듬 게임 플레이 | 수어 가이드, 카메라, 판정 UI |
| `result2` | 게임 결과 화면 | Perfect/Good/Miss 통계 및 등급 |
| `ProfileScene` | 프로필 화면 | 사용자 정보 및 통계 |
| `SettingsScene` | 설정 화면 | 진동 강도(Hard/Weak), 음소거 설정 |

#### 게임 플레이 화면 구성
- **수어 가이드**: 목표 수어 동작 애니메이션
- **사용자 카메라**: 웹캠 실시간 피드
- **판정 피드백**: PERFECT/GOOD/MISS 표시
- **가사 노트**: 현재 가사 키워드 표시
- **정지 패널**: 일시정지 기능

---

## 🚀 설치 및 실행 방법

### 요구 사항
- Unity 6 (6000.3.10f1) 이상
- Android Build Support (모바일 빌드 시)
- 웹캠 또는 카메라 장치

### 실행 절차
1. 저장소 클론
   ```
   git clone https://github.com/flying-adventure/SignSong-project.git
   cd SignSong-project
   ```
2. Unity Hub에서 프로젝트 폴더 열기 (프로젝트 에디터 버전: Unity 6.0.3)
3. `Assets/StreamingAssets/Models/` 경로 확인
   - `best_tcn.sentis` — 수어 분류 모델
   - `tcn_embedding_model.sentis` — OOD 탐지용 임베딩 모델
   - `ood_metadata.json` — 임계값 및 클래스 정보
   - `centroids.json` — 클래스별 임베딩 중심점
4. `Assets/StreamingAssets/Charts/` 경로 확인
   - `final_cherryblossom_mapping_table_auto.csv` — 벚꽃엔딩 노트 차트
5. Play Mode 실행 또는 Android 빌드
6. 앱 실행 후 곡 선택 → 게임 플레이

---

## 📁 디렉토리 구조

```
SignSong-project/
├── Assets/
│   ├── Audio/
│   │   └── cherryblossom.wav                  # 벚꽃엔딩 배경음악
│   ├── Models/                                # ONNX 원본 모델 (에디터 빌드용)
│   │   ├── best_tcn.onnx
│   │   └── tcn_embedding_model.onnx
│   ├── Prefabs/
│   │   ├── CategoryDropdown.prefab            # 카테고리 드롭다운 UI
│   │   └── MediaPipe/
│   │       └── HolisticMediaPipeRoot.prefab  # MediaPipe 루트 프리팹
│   ├── Scenes/
│   │   ├── Start_main.unity                   # 시작 화면
│   │   ├── Login_page.unity                   # 로그인 화면
│   │   ├── Sign_list.unity                    # 수어 곡 목록
│   │   ├── Dongyo_list.unity                  # 동요 곡 목록
│   │   ├── Gayo_list.unity                    # 가요 곡 목록
│   │   ├── Gayo_game.unity                    # 리듬 게임 플레이
│   │   ├── result2.unity                      # 게임 결과 화면
│   │   ├── ProfileScene.unity                 # 프로필 화면
│   │   └── SettingsScene.unity                # 설정 화면
│   ├── Scripts/
│   │   ├── SignRecognition/                   # 수어 인식 핵심 로직
│   │   │   ├── SignTcnRecognizer.cs           # TCN 모델 추론 및 OOD 판정
│   │   │   ├── SignFeatureExtractor.cs        # 랜드마크 → 141차원 특징 추출
│   │   │   ├── SignNoteManager.cs             # CSV 차트 로드 및 타이밍 판정
│   │   │   ├── SignGameBridge.cs              # 인식 결과 ↔ 게임 판정 연결
│   │   │   ├── SignPredictionProvider.cs      # 예측 이벤트 프로바이더
│   │   │   ├── SignSequenceBuffer.cs          # 프레임 시퀀스 버퍼
│   │   │   ├── SignNoteData.cs                # 노트 데이터 모델
│   │   │   ├── SignRecognitionResult.cs       # 인식 결과 모델
│   │   │   ├── OodMetadata.cs                 # OOD 메타데이터 모델
│   │   │   └── SignRecognizerTest.cs          # 테스트용 스크립트
│   │   ├── CameraFeed.cs                      # 카메라 입력 처리
│   │   ├── UIController.cs                    # 게임 UI 제어
│   │   ├── ResultSceneController.cs           # 결과 화면 씬 전환
│   │   ├── SceneNavigationManager.cs          # 씬 간 이동 관리
│   │   ├── DropdownSceneLoader.cs             # 카테고리 드롭다운 씬 전환
│   │   ├── PauseController.cs                 # 일시정지 기능
│   │   ├── LoadingController.cs               # 로딩 화면 제어
│   │   └── SettingsUIController.cs            # 설정 UI 제어
│   ├── StreamingAssets/
│   │   ├── Models/                            # 런타임 모델 파일 (Sentis 형식)
│   │   │   ├── best_tcn.sentis
│   │   │   ├── tcn_embedding_model.sentis
│   │   │   ├── ood_metadata.json              # 임계값 및 클래스 정보
│   │   │   └── centroids.json                 # 클래스별 임베딩 중심점
│   │   └── Charts/
│   │       └── final_cherryblossom_mapping_table_auto.csv  # 벚꽃엔딩 노트 차트
│   └── UI/                                    # UI 이미지 리소스
│       ├── Fonts/                             # 커스텀 폰트 (April16th, Jersey10)
│       └── *.png                              # 각 화면별 UI 이미지
├── Packages/
│   └── manifest.json                          # Unity 패키지 목록
└── ProjectSettings/                           # Unity 프로젝트 설정
```

---

## 🎮 게임 특징 및 기대효과

### 혁신성
- **접근성**: 청각장애인도 음악을 능동적으로 즐길 수 있는 포용적 경험 제공
- **교육성**: 점수, 콤보, 판정을 통해 게임 플레이 과정에서 자연스럽게 수어 학습
- **기술성**: 실시간 동작 인식 기반 게임의 엔드투엔드 파이프라인 구현

### 향후 확장 가능성
- 새로운 곡 및 난이도 추가 (채보 데이터 교체만으로 가능)
- 수어 교육 플랫폼, 에듀테크, 전시·체험형 콘텐츠로 확장
- 사용자 학습 분석, 개인별 난이도 조정, 추천 기능 추가 가능

