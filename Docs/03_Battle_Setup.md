# 3단계 : 배틀필드 (턴제 + 지형 파괴 + 포탄 3종 + 미니맵)

스크립트 폴더: `Assets/02.Scripts/Battle/`
씬: `scBattleField` / 프리팹: `Assets/Resources/NetTank.prefab`, `Assets/03.Prefabs/Shell.prefab`

> 이 단계는 Unity MCP로 씬/프리팹/UI를 만들고 연결해 두었습니다.
> 수업 때 만든 원래 스크립트(TankMove, TurretCtrl, CannonCtrl, FireCannon, Cannon, TankAnim)는 그대로 두었고,
> 그 방식을 네트워크/턴제에 맞게 옮긴 새 스크립트를 사용합니다. 씬의 원래 `Tank` 는 꺼 두었습니다.

---

## 1. 게임 규칙

| 항목 | 내용 |
|---|---|
| 인원 | 최대 4명, 개인전, **마지막까지 살아남은 1명 승리** |
| 턴 | 들어온 순서(ActorNumber)대로. 제한 시간 **40초** |
| 한 턴에 할 수 있는 것 | **이동(최대 40m)** + **행동 1번** (발사 / 회복 / 정찰). 행동하면 턴 종료 |
| 기본탄 | 포물선. Space 를 **누르고 있으면 파워 충전, 떼면 발사**. 피해 35, 지형 파괴 보통 |
| 레이저 (3회) | 직선으로 빠르게 날아감. 피해 30, **지형을 크게 파냄** |
| 대공탄 (3회) | Space → **큰 지도**가 열림 → 클릭한 곳 하늘에서 떨어짐. 피해 20(넓게), **지형 파괴 없음** |
| 회복 (2회) | HP +30 |
| 정찰 | **내 다음 턴이 올 때까지** 미니맵에 적 위치 표시 |
| 미니맵 | 우측 상단. 평소에는 **내 탱크만** 보임 |
| 카메라 | 누가 쏘든 **모두가 포탄을 3인칭으로 따라감** → 맞은 곳에서 2.5초 멈춤 → 다음 턴 탱크로 |

### 조작
| 키 | 동작 |
|---|---|
| 방향키 / WASD | 이동 (앞뒤 이동만 이동 거리 소모) |
| 마우스 | 포탑 회전 (마우스가 가리키는 지형 쪽) |
| 마우스 휠 | 포신 각도 |
| 1 / 2 / 3 | 기본탄 / 레이저 / 대공탄 선택 |
| H / R | 회복 / 정찰 |
| Space | 발사 |
| 우클릭 / Esc | 큰 지도 닫기 |

수치(피해, 반경, 횟수, 시간 등)는 전부 Inspector에서 바꿀 수 있습니다.
- 포탄 : `BattleManager` → Basic / Laser / Anti Air Shell
- 이동 거리, 사용 횟수, 회복량 : `NetTank` 프리팹 → TankController
- 턴 시간 : `BattleManager` → Turn Time

---

## 2. 하이어라키 (scBattleField)

```
scBattleField
├─ Main Camera        BattleCamera (SmoothFollow는 꺼둠), MinimapIcon 레이어는 안 보이게
├─ Terrain            TerrainDeformer
├─ Directional light
├─ Tank               (꺼둠 : 수업용 원본, NetTank 프리팹의 원본)
├─ MinimapCamera      위에서 내려다보는 직교 카메라 (TERRAIN + MinimapIcon 레이어만)
├─ EventSystem
├─ BattleCanvas       BattleUI
│  ├─ TopPanel        TurnText, TimerText
│  ├─ Notice          알림 (CanvasGroup으로 서서히 사라짐)
│  ├─ StatusPanel     내 HP
│  ├─ Minimap         MinimapController + MinimapImage(RawImage)
│  ├─ RevealLabel     "정찰 중"
│  ├─ ControlPanel    이동/파워 바, 포탄·행동 버튼 (내 턴에만 보임)
│  ├─ LeaveButton
│  ├─ BigMapPanel     대공탄 목표 고르는 큰 지도 (BigMapClick)
│  └─ GameOverPanel   승리/패배, 방으로 돌아가기
└─ BattleManager      PhotonView + BattleManager
```

### NetTank 프리팹 (Resources 폴더 : `PhotonNetwork.Instantiate` 는 Resources 에서 찾음)
```
NetTank   Rigidbody, PhotonView, PhotonTransformView, TankHealth, TankController
├─ (원래 탱크 메쉬들)
├─ Turret / Cannon / FirePos
├─ HUD           World Space Canvas : 닉네임 + 체력바 (항상 카메라를 봄)
└─ MinimapIcon   Quad, MinimapIcon 레이어(9번) → 미니맵 카메라에만 보임
```
PhotonView 의 Observed Components : PhotonTransformView(위치·회전) + TankController(포탑·포신 각도)

---

## 3. 스크립트 역할 (발표용)

| 스크립트 | 역할 |
|---|---|
| `BattleManager` | 탱크 스폰, **턴 순서 결정(방장)**, 포탄 생성, 폭발 처리, 승패, 방으로 복귀 |
| `TankController` | 내 탱크 조작(이동 거리 제한, 조준, 발사/회복/정찰), 포탑 각도 동기화 |
| `TankHealth` | 체력, 머리 위 체력바, 파괴 연출 |
| `Shell` | 날아가는 포탄. 맞은 위치를 쏜 사람에게 알려줌 |
| `TerrainDeformer` | 폭발 지점 지형을 둥글게 파냄 |
| `BattleCamera` | 탱크 따라가기 / 포탄 따라가기 / 맞은 곳 보기 |
| `MinimapController` | 미니맵 RenderTexture, 아이콘 표시(정찰), 큰 지도 클릭 → 월드 좌표 |
| `BigMapClick` | 큰 지도 클릭 위치를 0~1 비율로 변환 |
| `BattleUI` | 턴/시간/체력/버튼/알림/결과 화면 |

### 네트워크 흐름
```
[방장] 모든 탱크 생성 확인 → RPC_StartTurn(다음 사람, 턴 번호, 시작 시각) → 모두
[턴 주인] 이동/조준 → 발사 : TankController.RPC_Fire(종류, 위치, 속도) → 모두
[모두]  같은 위치·속도로 포탄을 각자 생성 → 카메라가 포탄을 따라감
[쏜 사람] 내 포탄이 맞으면 → BattleManager.RPC_Explode(종류, 위치) → 모두
[모두]  폭발 이펙트 + 같은 위치 지형 파기 + "내 탱크"만 피해 계산 → TankHealth.RPC_SetHp → 모두
[쏜 사람] 2.5초 뒤 RPC_EndTurn → 방장
[방장]  살아남은 탱크가 1대면 RPC_GameOver, 아니면 다음 턴
```

### 왜 이렇게 했나
- **포탄은 네트워크 오브젝트가 아님** : 모두가 같은 조건으로 각자 만들어서 날림 → 매 프레임 위치를 보낼 필요가 없음
- **맞은 위치는 쏜 사람 것 하나만 사용** : 물리 계산이 클라이언트마다 아주 조금 다를 수 있어서, 결과는 한 사람이 정함 → 지형/피해가 모두 같아짐
- **피해는 탱크 주인이 계산** : 내 체력은 내가 바꾸고 RPC로 알림 (PhotonView 주인만 값을 바꾸는 원칙)
- **턴은 방장이 결정** : 순서가 꼬이지 않도록 한 사람이 정함. 방장이 나가면 Photon이 새 방장을 정하고 이어서 진행
- **타이머** : 시작 시각을 `PhotonNetwork.Time`(서버 시간)으로 보내서 모두 같은 남은 시간을 표시
- **지형 복사본** : `TerrainData` 를 Play 할 때 복사해서 수정 → 원본 에셋은 안 바뀜.
  원래 지형 높이가 0 근처라 더 팔 수가 없어서, 시작할 때 전체를 20m 올리고 Terrain 을 20m 내림 (보이는 모습은 같음)
- **게임이 끝나면** 방장이 `PhotonNetwork.DestroyAll()` 후 `LoadLevel("scTitle")` → 모두 방 화면으로 (안 지우면 나중에 들어온 사람에게 예전 탱크가 생김)

---

## 4. 테스트

### 혼자 (가장 빠름)
`scBattleField` 를 열고 바로 **Play** → 자동으로 **오프라인 모드**로 혼자 플레이 (서버 접속 없음)
- 혼자일 때는 승패가 나지 않고 계속 내 턴이 돌아옵니다
- 확인할 것 : 이동 거리 바 감소 / 파워 충전 / 포탄 따라가는 카메라 / 지형이 파이는지 / 대공탄 큰 지도 / 정찰 표시 / 회복

### 여러 명
1. `scTitle` 에서 시작 → 방 만들기 → 다른 클라이언트(빌드)가 참가 → 준비 → 게임 시작
2. 확인할 것
   - 각자 고른 색으로 탱크가 보이는지
   - 턴이 번갈아 오는지, 상대 턴에는 조작이 안 되는지
   - 상대가 쏜 포탄을 내 카메라도 따라가는지
   - 파인 지형 모양이 양쪽 화면에서 같은지
   - 체력바가 양쪽에서 같은지
   - 정찰 후 내 미니맵에만 적이 보이는지 (상대 미니맵에는 변화 없음)
   - 한 명이 남으면 승리/패배 → 10초 뒤(또는 방장이 버튼) 방 화면으로 복귀

## 자주 생기는 문제
| 증상 | 원인 / 해결 |
|---|---|
| 탱크가 안 생김 | `NetTank` 가 `Assets/Resources` 에 있는지 확인 |
| 포탑이 안 돌아감 | Terrain 레이어가 8번(TERRAIN)인지 확인 |
| Space 를 누르면 버튼이 눌림 | 버튼 Navigation 이 None 인지 확인 (이미 설정됨) |
| 미니맵에 탱크 아이콘이 메인 화면에도 보임 | Main Camera Culling Mask 에서 MinimapIcon 해제 |
| 나무가 검게 보임 | 원래 지형 에셋의 나무 셰이더 문제 (콘솔 "must use the Nature/Soft Occlusion shader") |
