# 2단계 : 방 화면 (플레이어 목록 / 탱크 색상 / 준비 / 게임 시작)

스크립트: `Assets/02.Scripts/RoomPanelUI.cs`, `Assets/02.Scripts/PlayerSlot.cs`
목표: 방에 들어오면 같은 방 사람들의 닉네임·색상·준비 상태가 보이고, 방장이 **게임 시작**을 누르면 모두 `scBattleField` 로 이동

> 이 단계의 UI는 Unity MCP로 미리 만들어 연결해 두었습니다. 아래는 구성 설명과 테스트 방법입니다.

---

## 1. 하이어라키 구성 (`scTitle` → Canvas → RoomPanel)

```
RoomPanel                     (Image, RoomPanelUI 스크립트)
├─ RoomTitleText              "방 : Room_1234   (2 / 4)"
├─ PlayerList
│  ├─ PlayerListTitle         "플레이어"
│  ├─ PlayerSlot1 ~ 4         (Image, PlayerSlot 스크립트)
│  │  ├─ ColorImage           탱크 색상 네모
│  │  ├─ NameText             닉네임 ("(나)" 표시)
│  │  └─ StateText            방장 / 준비 완료 / 대기 중
├─ ColorArea
│  ├─ ColorTitleText          "탱크 색상"
│  ├─ ColorPreview            고른 색 미리보기
│  ├─ RedSlider / GreenSlider / BlueSlider   (0 ~ 1)
├─ LeaveButton                "나가기"
├─ ReadyButton                "준비" / "준비 취소"   ← 방장이 아닐 때만 보임
└─ StartButton                "게임 시작"            ← 방장에게만 보임
```

### RoomPanelUI Inspector
| 칸 | 연결 |
|---|---|
| Min Players To Start | 기본 `2`. **혼자 테스트할 때는 `1`** 로 바꾸기 |
| Room Title Text | RoomTitleText |
| Slots (4) | PlayerSlot1 ~ 4 |
| Red / Green / Blue Slider | 각 슬라이더 |
| Color Preview | ColorPreview |
| Ready Button / Ready Button Text | ReadyButton / 그 안의 Text |
| Start Button / Leave Button | StartButton / LeaveButton |

---

## 2. 동작 원리 (발표용)

### Custom Properties
Photon의 `Player.CustomProperties` 는 **플레이어마다 붙는 Hashtable** 이고,
`SetCustomProperties()` 로 바꾸면 **방 안의 모두에게 자동으로 전달**됩니다.

| 키 | 값 | 의미 |
|---|---|---|
| `"Ready"` | bool | 준비 완료 여부 |
| `"Color"` | Vector3 (r, g, b) | 탱크 색상 (0 ~ 1) |

- 누군가 값을 바꾸면 모든 클라이언트에서 `OnPlayerPropertiesUpdate()` 가 호출 → `Refresh()` 로 목록을 다시 그림
- 들어오기 / 나가기 / 방장 변경도 각각 `OnPlayerEnteredRoom`, `OnPlayerLeftRoom`, `OnMasterClientSwitched` 에서 `Refresh()`

### 흐름
1. `OnEnable()` : 방 화면이 켜지면 준비 해제 + 내 색상을 Custom Properties에 저장
2. 슬라이더를 움직이면 `OnColorChanged()` → 미리보기 색 변경, `PlayerPrefs` 에 저장(다음 실행 때 유지), Custom Properties 갱신
3. **준비** 버튼 → `Ready` 값을 반대로 바꿈
4. 방장은 준비 대신 **게임 시작** 버튼이 보임
   - `CanStart()` : 인원이 `minPlayersToStart` 이상이고, 방장을 뺀 **모두가 준비 완료**일 때만 버튼 활성화
5. **게임 시작** → `CurrentRoom.IsOpen = false` (로비 목록에 "게임 중", 중간 참가 막기) → `PhotonNetwork.LoadLevel("scBattleField")`
   - 1단계에서 `AutomaticallySyncScene = true` 로 해뒀기 때문에 방 안의 **모두가 같이** 씬 이동
6. **나가기** → `LeaveRoom()` → `LobbyManager.OnConnectedToMaster()` → 로비로 자동 복귀
7. 방장이 나가면 Photon이 자동으로 다른 사람을 방장으로 바꿈 → 새 방장에게 시작 버튼이 나타남

### 배틀필드에서 색상 쓰기
`RoomPanelUI.GetColor(player)` 를 `static` 으로 만들어 두어서, 배틀필드의 탱크가 자기 주인의 색을 바로 읽을 수 있습니다.

---

## 3. 테스트

### 혼자
1. `RoomPanel` 선택 → RoomPanelUI → **Min Players To Start = 1**
2. Play → 닉네임 → 게임 시작 → 방 만들기
3. 내 칸이 노란색, 상태 "방장", 시작 버튼 활성화 확인
4. 슬라이더 → 미리보기와 내 칸 네모 색이 같이 바뀌는지 확인
5. 게임 시작 → `scBattleField` 로 이동되면 성공
6. 테스트 후 **Min Players To Start 를 2로 되돌리기**

### 두 명 (에디터 + 빌드)
1. 빌드 쪽에서 방을 만들고, 에디터에서 그 방에 참가
2. 양쪽 목록에 두 명이 보이고, 색을 바꾸면 **상대 화면에서도** 색이 바뀌는지 확인
3. 참가자가 **준비** → 방장 화면의 상태가 "준비 완료", 시작 버튼 활성화
4. 방장이 **나가기** → 남은 사람이 방장이 되고 시작 버튼이 보이는지 확인
5. 게임 시작 → 두 클라이언트 모두 배틀필드로 이동

## 자주 생기는 문제
| 증상 | 원인 / 해결 |
|---|---|
| 시작 버튼이 계속 회색 | 인원 부족(Min Players To Start), 또는 준비 안 한 사람이 있음 |
| 상대 색이 흰색으로 보임 | 상대가 아직 Custom Properties를 보내기 전. 잠시 후 갱신됨 |
| 게임 시작해도 상대는 이동 안 함 | `LobbyManager.Awake` 의 `AutomaticallySyncScene = true` 확인, 씬이 Build Settings에 있는지 확인 |
| 한글 일부가 공백으로 나옴 | 폰트 Atlas Population Mode가 `Dynamic` 인지 확인 |
