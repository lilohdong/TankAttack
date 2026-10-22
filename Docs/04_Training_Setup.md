# 4단계 : 연습장 (튜토리얼 씬)

스크립트 폴더: `Assets/02.Scripts/Training/`
씬: `scTraining` / 프리팹: `Assets/03.Prefabs/Training/TrainingTarget.prefab`, `TrainingWall.prefab`

> 이 단계도 Unity MCP로 씬/프리팹/UI를 만들고 연결해 두었습니다.
> 타이틀 화면의 **연습하기** 버튼 → 서버 접속 없이 혼자 조작법을 단계별로 배우는 씬입니다.
> 발표 때 클라이언트를 여러 개 띄우지 않아도 게임의 기능을 전부 보여줄 수 있습니다.

---

## 1. 진행 순서

| 단계 | 할 일 | 완료 조건 |
|---|---|---|
| 1. 이동하기 | 방향키 / WASD 로 움직이기 | 이동 거리 8m 이상 사용 |
| 2. 기본탄 쏘기 | 앞쪽(약 45m)의 빨간 표적 탱크를 기본탄으로 | 표적이 **기본탄**에 부서짐 |
| 3. 레이저로 벽 부수기 | 앞쪽(약 25m)의 회색 벽을 레이저로 | 벽이 **레이저**에 부서짐 |
| 4. 정찰하기 | 맵 반대편에 숨은 표적을 R 키로 찾기 | 정찰 사용 |
| 5. 대공탄으로 공격 | 큰 지도에서 빨간 아이콘 클릭 | 표적이 **대공탄**에 부서짐 |
| 6. 회복하기 | 포격을 맞은 뒤 H 키로 회복 | 체력이 올라감 |
| 완료 | "연습 완료!" 결과 화면 → **타이틀로** 버튼 | |

- 다른 포탄으로 표적을 부수면 "OO(으)로 맞혀야 해요!" 알림 후 **표적을 다시 세움**
- 왼쪽 안내 패널의 **건너뛰기** 버튼으로 다음 단계로 넘어갈 수 있음
- 레이저 / 대공탄 / 회복은 다 쓰면 다시 채워줌 (연습이므로)
- 시간 제한 없음, 행동 후에는 다시 내 턴이 옴
- 내 탱크가 파괴되면 "연습 실패" → 10초 뒤 타이틀로

---

## 2. 어떻게 만들었나 (발표용)

**배틀 코드를 그대로 재사용**하는 것이 핵심입니다.

```
scTraining = scBattleField 복사본
           + BattleManager 의 Training Mode 체크
           + TrainingManager (단계 안내 / 표적 배치 / 완료 확인)
           + GuidePanel (왼쪽 안내 UI)
```

1. `scTraining` 은 서버에 접속하지 않은 상태에서 열림 → `BattleManager.Start()` 가 **Photon 오프라인 모드**로 혼자 방을 만듦
   (배틀필드를 바로 Play 해서 혼자 테스트하던 방식과 같음)
2. 혼자라서 턴이 끝나면 다시 내 턴 → 탱크 조작, 포탄, 지형 파괴, 카메라, 미니맵은 **배틀과 똑같은 코드**로 동작
3. `TrainingManager` 는 게임 상태를 **지켜보기만** 함
   - 이동 : `TankController.MoveLeft` 가 줄었는지
   - 포탄 : `BattleManager.Exploded` 이벤트로 **마지막에 터진 포탄 종류**를 기억 → 표적이 부서질 때 종류 비교
   - 정찰 : `MinimapController.IsRevealing`
   - 회복 : 체력이 이전 프레임보다 올라갔는지
4. 표적은 배틀 맵의 바위와 같은 `Destructible` 사용 → `RPC_Explode` 가 바위처럼 피해를 줌. 부서지면 `Broken` 이벤트로 알림

### 표적 위치
시작할 때 **내 탱크 위치와 방향**을 기준으로 계산 (맵을 다시 생성해도 자동으로 맞음)
- 기본탄 표적 : 앞 45m, 오른쪽 12m
- 레이저 벽 : 앞 25m, 왼쪽 8m
- 대공탄 표적 : 맵 가운데를 기준으로 **내 반대편** (가운데 큰 산에 가려 보이지 않음 → 정찰 + 대공탄이 필요)
- 표적 자리 10m 안의 바위는 치움

### 기존 스크립트에서 바꾼 부분 (배틀에는 영향 없음)
| 파일 | 변경 |
|---|---|
| `BattleManager` | `trainingMode` 체크박스 : 시간 제한 끔, 내 턴이 와도 정찰 표시 유지, 실패 문구. `Exploded` 이벤트, `EndTraining()` 추가 |
| `BattleUI` | 연습장에서는 타이머 숨김 |
| `Destructible` | 부서질 때 `Broken` 이벤트 |
| `LobbyManager` | **연습하기** 버튼 : 서버에 접속돼 있으면 먼저 끊고(`Disconnect`) 씬 이동 (접속 중에는 오프라인 모드를 켤 수 없음) |

정찰 표시를 유지하는 이유 : 원래는 "내 다음 턴이 오면" 정찰이 꺼지는데, 연습장은 혼자라서 다음 턴이 바로 내 턴 → 정찰하자마자 꺼져버림.
그래서 연습장에서는 `TrainingManager` 가 대공탄 단계를 끝낼 때 직접 끕니다.

---

## 3. 하이어라키 (scTraining, scBattleField 와 다른 부분만)

```
scTraining
├─ BattleManager      Training Mode ✔
├─ TrainingManager    TrainingManager (표적 프리팹, 표적 체력/위치, 안내 UI 연결)
└─ BattleCanvas
   ├─ GuidePanel      StepText("연습 1 / 6"), TitleText, DescText, SkipButton
   └─ GameOverPanel   ReturnButton 글자 "타이틀로"
```

### 프리팹
| 프리팹 | 구성 |
|---|---|
| `TrainingTarget` | NetTank 에서 네트워크/조작 부품을 뺀 탱크 모양 + `Destructible` + `TrainingTarget`(빨간색, 미니맵 아이콘은 정찰 중에만) |
| `TrainingWall` | 14 x 9 x 2 m 큐브 + `Destructible` + `TrainingTarget`(회색) |

수치는 `TrainingManager` Inspector 에서 바꿀 수 있습니다 (표적 체력, 위치, 이동 목표 거리, 회복 단계 피해량).
표적 체력 기본값은 "가까이 맞히면 한 방" 기준 : 기본탄 표적 25, 벽 30, 대공탄 표적 15

---

## 4. 테스트
- `scTraining` 을 열고 바로 Play 해도 되고, `scTitle` → **연습하기** 로 들어가도 됨
- 로비까지 들어갔다가 **뒤로** → **연습하기** 를 눌러도 됨 (접속을 끊고 이동)
- 확인할 것 : 단계마다 표적이 생기는지 / 다른 포탄으로 부수면 다시 세워지는지 / 정찰 후 큰 지도에 빨간 아이콘 / 완료 화면 → 타이틀

## 자주 생기는 문제
| 증상 | 원인 / 해결 |
|---|---|
| 배틀 맵을 다시 생성했더니 연습장 바위가 그대로 | 맵 생성기는 `scBattleField` 만 바꿈. 지형(`BattleTerrain`)은 같이 바뀌지만 바위는 씬마다 따로 있음 → scBattleField 를 다시 복사하거나 그대로 사용 |
| 표적이 땅에 묻히거나 뜸 | 경사가 심한 곳. `TrainingManager` 의 표적 위치 값을 조금 바꿔보기 |
