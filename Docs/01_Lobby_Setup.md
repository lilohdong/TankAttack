# 1단계 : 로비 UI 세팅 가이드

스크립트: `Assets/02.Scripts/LobbyManager.cs`, `Assets/02.Scripts/RoomListItem.cs`
목표: 타이틀(닉네임 + 게임 시작) → 로비(방 목록 / 방 만들기 / 랜덤 참가) → 방 패널로 전환

---

## 0. 사전 준비

### 0-1. 백업
작업 전에 `TankAttack` 폴더 전체를 복사해두거나 `git init`으로 저장소를 만들어 두세요.

### 0-2. Photon AppId 확인
1. 메뉴 **Window → Photon Unity Networking → Highlight Server Settings**
2. Inspector의 **App Id PUN** 칸에 값이 있는지 확인
   - 비어 있으면 https://dashboard.photonengine.com 에서 **Photon PUN** 타입 앱을 만들고 AppId를 붙여넣기
3. **Fixed Region** 에 `kr` 입력 (같은 지역 서버로 모여야 서로 방이 보임)

### 0-3. TextMeshPro 준비
1. 메뉴 **Window → TextMeshPro → Import TMP Essential Resources** (처음 TMP 오브젝트를 만들 때 뜨는 창에서 눌러도 됨)

### 0-4. 한글 폰트 (중요!)
TMP 기본 폰트는 한글이 없어서 글자가 □□로 나옵니다.
1. 무료 한글 폰트(.ttf) 준비 — 예: 나눔고딕, Noto Sans KR
2. `Assets/04.Images/Fonts` 폴더를 만들고 ttf 파일을 넣기
3. ttf 파일 우클릭 → **Create → TextMeshPro → Font Asset → SDF**
   - 생성된 Font Asset의 **Atlas Population Mode** 가 `Dynamic` 인지 확인 (입력하는 글자가 자동으로 추가됨)
4. 메뉴 **Edit → Project Settings → TextMesh Pro → Settings** 의 **Default Font Asset** 에 방금 만든 폰트 지정
   → 이후 만드는 모든 TMP 텍스트가 한글 폰트를 사용

---

## 1. 씬 만들기
1. `Assets/01.Scenes` 에서 우클릭 → **Create → Scene** → 이름 `scTitle`
2. **File → Build Profiles (Build Settings)** 의 Scene List에 순서대로 추가
   - 0 : `scTitle`
   - 1 : `scBattleField`

---
## 2. 화면 흐름

```
TitlePanel   닉네임 입력 + [게임 시작]
   │  닉네임이 비어 있으면 "닉네임을 입력해주세요." 표시
   │  입력했으면 서버 접속 → 로비 입장
   ▼
LobbyPanel   방 목록 + [방 만들기] [랜덤 참가] [뒤로]
   │  방 목록 한 줄 클릭 → 그 방에 참가
   │  [방 만들기] → CreateRoomPopup (방 이름 입력)
   ▼
RoomPanel    방 안 화면 (2단계에서 채움)
```

---

## 3. 하이어라키 구성

UI는 **우클릭 → UI** 메뉴에서 생성하고, 텍스트·입력·버튼은 **TextMeshPro 버전**을 사용하세요.
이미 만든 타이틀 화면은 `TitlePanel` 아래로 넣으면 됩니다.

```
scTitle
├─ Main Camera
├─ EventSystem
├─ LobbyManager                    (빈 오브젝트 + LobbyManager 스크립트)
└─ Canvas
   ├─ ServerStatusText             (Text)  좌측 상단 "서버 상태 : "   ← 패널 밖! 항상 보임
   ├─ NoticeText                   (Text)  하단 가운데, 안내/오류 메시지 ← 패널 밖! 항상 보임
   │
   ├─ TitlePanel                   (Panel 또는 빈 오브젝트)
   │  ├─ TitleText                 "Tank Attack"
   │  ├─ NicknameLabel             "닉네임 :"
   │  ├─ NicknameInput             (Input Field)
   │  └─ StartButton               (Button) "게임 시작"
   │
   ├─ LobbyPanel                   (Panel)
   │  ├─ RoomListScrollView        (Scroll View)
   │  │  └─ Viewport
   │  │     └─ Content             ← 방 목록이 여기에 생성됨
   │  ├─ EmptyListText             (Text) "열린 방이 없습니다"  (Scroll View 가운데 위에 겹치게)
   │  ├─ CreateRoomButton          (Button) "방 만들기"
   │  ├─ RandomJoinButton          (Button) "랜덤 참가"
   │  └─ BackButton                (Button) "뒤로"
   │
   ├─ RoomPanel                    (Panel)  ← 2단계에서 채움
   │  └─ RoomTitleText             (Text) "방 화면 (준비 중)"
   │
   └─ CreateRoomPopup              (Panel)  ← 하이어라키 맨 아래 = 화면 맨 앞에 그려짐
      ├─ Dim                       (Image) 화면 전체를 덮는 반투명 검정 (뒤쪽 클릭 막기)
      └─ Window                    (Image) 가운데 작은 창
         ├─ PopupTitleText         "방 만들기"
         ├─ RoomNameInput          (Input Field) Placeholder "방 이름 (비우면 자동 생성)"
         ├─ ConfirmCreateButton    (Button) "만들기"
         └─ CancelCreateButton     (Button) "취소"
```

### 세부 설정
| 오브젝트 | 설정 |
|---|---|
| Canvas | Canvas Scaler → **Scale With Screen Size**, Reference Resolution `1920 x 1080`, Match `0.5` |
| ServerStatusText, NoticeText | 패널 **밖** (Canvas 바로 아래)에 두기. 어느 화면에서도 보여야 함 |
| Dim | Anchor를 Stretch-Stretch(전체)로, Color `(0, 0, 0, 150)` 정도. Raycast Target 체크 유지 → 팝업 뒤 버튼이 눌리지 않음 |
| 각 패널 | 처음에 켜져 있어도 됨 (스크립트가 시작할 때 알맞게 켜고 끔) |

### Scroll View의 Content 설정 (방 목록이 세로로 쌓이도록)
1. `Content` 선택 → **Add Component → Vertical Layout Group**
   - Spacing `10`, Padding 상하좌우 `10`
   - Child Alignment `Upper Center`
   - **Control Child Size** : Width ✅, Height ✅
   - **Child Force Expand** : Width ✅, Height ❌
2. `Content` 에 **Add Component → Content Size Fitter**
   - Vertical Fit : `Preferred Size` (방이 많아지면 Content가 길어져서 스크롤 가능)
3. Scroll View의 **Horizontal** 체크 해제 (세로 스크롤만)

---

## 4. 방 목록 한 줄 프리팹 (RoomItem)

1. 하이어라키에서 `Content` 아래에 **UI → Button - TextMeshPro** 생성 → 이름 `RoomItem`
2. 버튼 안의 기본 Text는 이름을 `RoomNameText` 로 바꾸고 왼쪽 정렬
3. `RoomItem` 아래에 **UI → Text - TextMeshPro** 추가 → 이름 `PlayerCountText`, 오른쪽 정렬
4. `RoomItem` 에 **Add Component → Layout Element** → Preferred Height `80` (한 줄 높이)
5. `RoomItem` 에 `RoomListItem.cs` 붙이고 연결

| Inspector 칸 | 연결 |
|---|---|
| Room Name Text | RoomNameText |
| Player Count Text | PlayerCountText |
| Join Button | RoomItem (자기 자신의 Button) |

6. `Assets/03.Prefabs` 로 `RoomItem` 을 드래그해서 **프리팹으로 만들기**
7. 하이어라키의 `RoomItem` 은 **삭제** (스크립트가 실행 중에 만들어줌)

> 가득 찬 방이나 게임 중인 방은 버튼이 비활성화되어 눌리지 않습니다.
> Button의 **Disabled Color** 를 회색 계열로 해두면 구분이 잘 됩니다.

---

## 5. LobbyManager 연결

1. 하이어라키 **우클릭 → Create Empty** → 이름 `LobbyManager`
2. `LobbyManager.cs` 붙이기
3. Inspector 칸에 연결

| 그룹 | Inspector 칸 | 연결할 오브젝트 |
|---|---|---|
| 패널 | Title Panel | TitlePanel |
| | Lobby Panel | LobbyPanel |
| | Room Panel | RoomPanel |
| | Create Room Popup | CreateRoomPopup |
| 공통 | Server Status Text | ServerStatusText |
| | Notice Text | NoticeText |
| 타이틀 | Nickname Input | NicknameInput |
| | Start Button | StartButton |
| 로비 | Create Room Button | CreateRoomButton |
| | Random Join Button | RandomJoinButton |
| | Back Button | BackButton |
| | Room List Content | Scroll View 안의 **Content** |
| | Room Item Prefab | `Assets/03.Prefabs/RoomItem` (**프리팹**을 연결) |
| | Empty List Text | EmptyListText |
| 방 만들기 팝업 | Room Name Input | RoomNameInput |
| | Confirm Create Button | ConfirmCreateButton |
| | Cancel Create Button | CancelCreateButton |

> 버튼의 OnClick() 은 Inspector에서 연결하지 마세요. 스크립트가 `AddListener`로 직접 연결합니다. (둘 다 하면 두 번 실행됨)

---

## 6. 테스트

### 혼자 테스트
1. `scTitle` 에서 Play → 서버 상태 : `접속 안 됨`
2. 닉네임 비우고 **게임 시작** → "닉네임을 입력해주세요."
3. 닉네임 입력 후 **게임 시작** → `접속 중...` → LobbyPanel로 전환, 서버 상태 `로비`, "열린 방이 없습니다" 표시
4. **방 만들기** → 팝업 → **취소** 하면 닫히는지 확인
5. **방 만들기** → **만들기** → RoomPanel로 전환, 서버 상태 `방 : Room_xxxx`
6. **뒤로** → 타이틀로 돌아가고, 다시 **게임 시작** 하면 로비로 들어가는지 확인

### 두 명 테스트 (방 목록 확인)
같은 PC에서 클라이언트 두 개를 띄워야 합니다.
1. **File → Build Profiles** → Windows 빌드 → `Build And Run`
   - Player Settings → Resolution → **Fullscreen Mode : Windowed** 로 해두면 편함
   - 에디터와 exe가 같은 닉네임을 불러오니, 둘 중 하나는 닉네임을 바꿔서 테스트
2. 에디터 : 게임 시작 → 로비에서 대기
3. exe : 게임 시작 → 방 만들기 `test`
4. 에디터의 방 목록에 `test  1 / 4` 가 **자동으로 나타나는지** 확인 → 클릭 → RoomPanel로 전환되면 성공
5. **랜덤 참가** 도 확인 : 한쪽이 방을 만든 상태에서 다른 쪽이 랜덤 참가 → 같은 방에 들어가야 함

### 실패 메시지 확인
| 상황 | 기대 메시지 |
|---|---|
| 닉네임 비우고 게임 시작 | 닉네임을 입력해주세요. |
| 같은 이름으로 방 두 번 만들기 | 이미 같은 이름의 방이 있습니다. |
| 4명 찬 방 | 목록에서 버튼이 비활성화됨 |

---

## 자주 생기는 문제
| 증상 | 원인 / 해결 |
|---|---|
| 글자가 □□□ 로 나옴 | 0-4 한글 폰트 설정 확인 |
| "접속 중..." 에서 멈춤 | AppId 확인, 인터넷 연결 확인 |
| 서로 방이 안 보임 | 두 클라이언트의 Fixed Region이 같은지 확인 (`kr`) |
| 방 목록이 겹쳐서 나옴 | Content의 Vertical Layout Group 설정 확인 |
| 방 목록 줄이 너무 얇음 | RoomItem의 Layout Element → Preferred Height 확인 |
| 팝업이 패널 뒤에 가려짐 | CreateRoomPopup을 하이어라키 맨 아래로 이동 |
| NullReferenceException | Inspector 칸 중 비어 있는 것이 있음 |
| `TMPro` 관련 컴파일 에러 | Legacy Text/InputField가 아닌 TMP 버전을 연결했는지 확인 |

---

## 코드 흐름 요약 (발표용)
1. **게임 시작** → `PhotonNetwork.NickName` 설정 → `ConnectUsingSettings()` 로 Photon 서버 접속
2. `OnConnectedToMaster()` → `JoinLobby()` (로비에 들어가야 방 목록을 받을 수 있음)
3. `OnJoinedLobby()` → 로비 화면 표시
4. `OnRoomListUpdate(roomList)` → **바뀐 방만** 전달되므로 `Dictionary`에 모아두고, `RemovedFromList` 인 방은 지운 뒤 목록 UI를 다시 그림
5. 방 참가 방법 3가지
   - 목록 클릭 : `JoinRoom(방 이름)`
   - 방 만들기 : `CreateRoom(이름, 옵션)` — 최대 4명
   - 랜덤 참가 : `JoinRandomOrCreateRoom()` — 들어갈 방이 없으면 새로 만듦
6. `OnJoinedRoom()` → 방 화면으로 전환
7. 실패하면 `OnCreateRoomFailed / OnJoinRoomFailed` 에서 오류 코드별 메시지 표시
8. 방에서 나오면 다시 `OnConnectedToMaster()` → 로비로 자동 복귀
