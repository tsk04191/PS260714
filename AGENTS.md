# PS260714 통합 작업 지침

이 파일은 저장소 전체에 적용되는 단일 에이전트 작업 지침이다. 프로젝트 규칙을 변경할 때는 별도 `AGENTS.md`를 추가하지 말고 이 문서를 최신화한다.

## 1. 프로젝트 기준

- Unity 버전은 `6000.3.11f1`이다.
- 렌더링은 URP `17.3.0`, UI는 uGUI와 TextMeshPro를 사용한다.
- 입력은 Input System `1.19.0`, 비동기 처리는 UniTask를 사용한다.
- Odin Inspector와 Wingman은 `Assets/01_Plugins`에 포함된 에디터 도구다. 라이선스와 설치 상태를 확인하지 않고 복사, 교체 또는 업그레이드하지 않는다.
- 현재 Build Settings의 진입 씬은 `Assets/04_Scenes/ClientScene.unity` 하나다.
- 런타임 ScriptableObject는 `Assets/06_Runtime/Resources`에 있다.
- UI Canvas 기준 해상도는 `1920x1080`이며 화면 크기 대응은 루트 Canvas Scaler를 기준으로 한다.
- 스크립트는 `Assets/10_Scripts`, 로컬라이제이션 원본은 `Assets/11_LocalizationSource`에 둔다.
- 런타임, 에디터, EditMode 테스트, PlayMode 테스트는 각각 asmdef로 분리되어 있다.
- `Assets`, `Packages`, `ProjectSettings`의 실제 구조를 확인하고 작업한다.
- `Library`, `Temp`, `Logs`, `obj`, `bin`, 생성된 `.csproj`와 IDE 임시 파일은 소스 파일로 취급하거나 직접 수정하지 않는다.
- `.meta` 파일은 에셋 생성·이동·삭제로 인해 명확히 필요한 경우가 아니면 수정하지 않는다.

## 2. 변경 작업 공통 원칙

- 사용자의 기존 변경사항과 관계없는 파일을 되돌리거나 정리하지 않는다.
- 요청 범위 밖의 대규모 리팩터링, 이름 변경, 에셋 이동, 패키지 교체를 함께 수행하지 않는다.
- 검토, 설명 또는 진단만 요청받은 경우 코드, 씬, 프리팹과 에셋을 수정하지 않는다.
- 수정 전 관련 씬, 프리팹, ScriptableObject, 호출부와 테스트를 검색해 직렬화와 참조 영향을 확인한다.
- Scene 또는 Prefab은 사용자가 명시적으로 수정을 요청한 경우에만 변경한다.
- 기존 요소 삭제, 계층 이동, 컴포넌트 교체, 대량 이름 변경처럼 Inspector 연결에 영향을 주는 작업은 영향과 필요성을 먼저 설명한다.
- 변경 후 수정 파일, 동작 변화, 검증 결과와 Unity Editor에서 추가 확인할 항목을 요약한다.
- 기능의 구현·부분 구현·미구현 상태가 바뀌면 단일 게임 위키인 `GAME_STRUCTURE.md`도 함께 갱신할지 확인한다.

## 3. 아키텍처

### 전역 시스템

- `GameManager`는 전역 진입점이며 `DataManager`, `AudioManager`, `GameEventManager`, 전투 관리자를 연결한다.
- 시스템 간 결합이 필요할 때는 가능한 한 `GameEventManager`의 C# 이벤트를 사용한다.
- 이벤트 메서드는 요청을 나타내는 `Request...`와 완료·변경 알림을 나타내는 `Notify...` 의미를 구분한다.
- 이벤트 구독 객체는 `OnDisable`, `OnDestroy` 또는 별도 `Teardown`에서 반드시 구독을 해제한다.
- 초기화 순서가 필요한 화면은 `GameEventManager.IsDataReady` 또는 `DataReady` 이벤트를 사용하고 임의 프레임 지연에 의존하지 않는다.
- UniTask 비동기 작업은 GameObject 생명주기와 연결된 `CancellationToken`을 사용한다.

### 데이터 계층

- 정적 원본 데이터는 `*SO` ScriptableObject에 저장한다.
- 레벨, 경험치, 카드 위력처럼 실행 중 변경되는 값은 `*Data` 일반 C# 객체에 저장한다.
- 씬 표시와 Unity 생명주기가 필요한 객체만 `*Runtime` 또는 MonoBehaviour로 작성한다.
- ScriptableObject를 전투 중 상태 저장소로 사용하거나 원본 에셋 값을 런타임에서 직접 변경하지 않는다.
- ScriptableObject에서 런타임 데이터를 만들 때 필요한 값을 복사하여 인스턴스 사이에 상태가 공유되지 않도록 한다.
- `DataManager`의 정의 목록은 런타임 레지스트리 역할을 한다. ID 조회는 직접 목록을 반복하기보다 기존 `TryGet...SO` API를 우선 사용한다.
- 저장 스키마 변경은 버전, 마이그레이션, 손상·미지원 버전 처리와 기존 저장 데이터 영향을 함께 검토한다.

### 페이지와 진행 컨텍스트

- 페이지와 탭은 `IPage`의 `Open(PageOpenMode)`, `Close()`, `Init()` 규약을 따른다.
- 새로 진입하면 `Fresh`, 이전 상태를 유지하며 돌아오면 `Resume`를 사용한다.
- 최상위 페이지 전환은 GameObject 활성화·비활성화와 `PageControl.PagToPag` 같은 공통 경로를 사용한다.
- `Open`은 페이지 활성화와 초기화를 한 번만 수행하고 호출부에서 `Init`을 중복 호출하지 않는다.
- `Close`는 임시 UI, 이벤트 구독, 비동기 작업과 드래그 상태를 정리한 뒤 페이지를 비활성화한다.
- 전환 대상이 null이거나 공통 페이지 인터페이스를 구현하지 않으면 현재 페이지 상태를 바꾸기 전에 전환을 중단한다.

## 4. 전투 능력 공통 규칙

- 전투 능력과 공통 효과를 수정할 때 카드 한 종류만 수정하지 않는다. 다음 소유자를 한 작업 단위로 모두 검토하고, 적용 가능하면 같은 코드 경로로 수정한다.
  - 캐릭터 공격, 액티브 스킬, 패시브와 역할·아키타입 능력
  - 적 능력
  - 전투 아이템
  - 전투 카드
  - 상태 효과의 트리거 블록
- 던전 이벤트와 휴식 능력은 Run 도메인이다. 전투 실행기에 억지로 연결하지 않고, 공통 변경의 의미가 있으면 Run 실행기와 검증기에 별도로 반영한다. 지원하지 않으면 검증 오류로 명시한다.
- `BattleEffectType`, 대상 필요 여부와 실행 전제조건은 `BattleEffectRules`를 단일 기준으로 사용한다.
- 소유자별 지원 여부 `switch`나 `owner is BattleCardSO` 같은 분기를 새로 만들지 않는다.
- 능력 전체의 행동 대상 필요 여부는 `BattleAbilityRules.RequiresActionTargets`를 사용한다.
- 대상이 필요 없는 효과에 수동 대상 선택을 요구하지 않는다.
- 공통 효과 입력 UI는 `BattleAbilityEditorGUI.DrawEffectList`를 사용한다.
- 효과 종류, 기본값, 빠른 추가 버튼, 순서 이동과 검증 표시를 소유자별 에디터에 복제하지 않는다.

공통 효과를 추가하거나 변경할 때 다음 항목을 같은 변경에서 처리한다.

1. 런타임 효과 타입과 직렬화 투영
2. `BattleEffectRules`와 공통 실행기
3. 모든 능력 소유자의 실행 컨텍스트와 필요한 서비스
4. 공통 에디터 입력과 기본값
5. 공통 검증과 소유자별 추가 제약
6. 로컬라이제이션 변수와 설명
7. 소유자별 회귀 테스트

- 카드 드로우처럼 전투 서비스가 필요한 효과는 특정 소유자가 서비스를 직접 가진다고 가정하지 않는다. 전투 보드의 공통 서비스 공급 경로를 통해 모든 능력 컨텍스트에 전달한다.
- 월드 범위 지정은 `BattleManualAreaPlacementMode`로 배치 제약을 명시한다.
- 카드처럼 플레이어가 지점을 직접 지정하는 효과는 `FreePointer`를 사용하며 클릭 지점을 시전자 사거리나 맵 중심 반지름으로 다시 보정하지 않는다.
- 캐릭터 능력처럼 사거리 제약이 필요한 경우에만 `AbilityConstrained`를 사용한다.
- `OriginMode.DesignatedPoint` 범위는 지점을 지정하기 전까지 시전자 위치에 미리보기를 표시하지 않는다.
- 지정 후 범위는 월드 지점에 고정하고 시전자 Transform을 따라가지 않는다. 시전자 위치를 계속 따르는 동작은 `OriginMode.Caster`에만 허용한다.
- 전투 서비스는 특정 카드·캐릭터·아이템 소유자에 종속시키지 않고 공통 컨텍스트에서 공급한다.

## 5. 로컬라이제이션 규칙

- 능력 수치 변수는 `BattleAbilityLocalizationArguments`의 공통 의미를 따른다.
- `attack`은 시전자 공격력, `damage`는 계산·표시할 피해량, `radius`는 원·부채꼴의 월드 반지름이다.
- 공통 변수 이름 `damage`, `armor`, `heal`, `stacks`, `seconds`, `duration`, `drawCount`, `radius`, `targetCount`를 소유자마다 다른 의미로 재사용하지 않는다.
- 에셋에는 언어별 fallback 필드를 추가하지 않는다.
- 이름과 설명에는 각각 언어 중립 fallback 하나만 유지하고 실제 한국어·영어 문구는 로컬라이제이션 테이블에서 관리한다.
- CSV 수정 후 생성 코드를 갱신하고 로컬라이제이션 검증을 통과시킨다.
- 생성된 `Assets/10_Scripts/Localization/Generated` 코드는 직접 수정하지 않는다.
- 고정 UI와 동적 UI 모두 `LocalizationKeys`와 `LocalizationService`를 사용하고 하드코딩 문구 추가를 피한다.

## 6. 코드 스타일

- 들여쓰기는 공백 4칸, 중괄호는 Allman 스타일을 사용한다.
- 클래스, 구조체, 메서드와 프로퍼티는 `PascalCase`를 사용한다.
- 인터페이스는 `I` 접두사, 기존 프로젝트 열거형은 `E` 접두사를 유지한다.
- ScriptableObject는 `*SO`, 실행 데이터는 `*Data`, UI 타입은 `*Page`, `*Tab`, `*Card`, `*Slot` 역할 접미사를 사용한다.
- 새 클래스명에 `pagBattleSetup` 같은 소문자 시작을 사용하지 않는다.
- `Page`, `Character`, `Parent`, `Enemy` 등의 철자를 일관되게 사용한다.
- 새 Inspector 참조는 원칙적으로 `[SerializeField] private`로 선언한다.
- Inspector 노출 private 필드는 `camelCase`, 비노출 private 상태 필드는 `_camelCase`를 사용한다.
- 외부 읽기가 필요한 상태는 public 필드보다 읽기 전용 또는 private setter 프로퍼티를 우선한다.
- Inspector 연결과 직렬화 호환성을 위해 기존 public 필드명과 `[SerializeField]` 필드명을 임의로 바꾸지 않는다.
- 직렬화 필드 이름 변경이 필요하면 영향 범위를 먼저 확인하고 필요 시 `FormerlySerializedAs`를 사용한다.
- 불변 컬렉션 참조는 가능한 경우 `readonly`로 선언하고 외부에는 `IReadOnlyList<T>`로 노출한다.
- public API와 Unity 이벤트 진입점에서 null, 인덱스와 범위를 방어적으로 확인한다.
- 빈 `catch`는 취소 예외처럼 의도적으로 무시하는 경우에만 사용하고 일반 예외를 숨기지 않는다.
- 파일이 과도하게 커지면 역할별 클래스로 분리하되 Unity 직렬화와 에디터 연결 영향을 먼저 확인한다.

## 7. UI 구조와 배치

### Canvas와 레이어

- 일반 화면, 팝업, 툴팁 레이어를 분리하고 일반 화면 < 팝업 < 툴팁 순서로 렌더링한다.
- 페이지, 팝업과 툴팁은 각각 전용 레이어 아래에 배치한다.
- 전체 페이지와 레이어 루트는 부모 영역을 채우는 Stretch 앵커를 사용한다.
- 페이지마다 별도 Canvas Scaler를 만들지 않고 루트 Canvas의 기준 해상도와 스케일 정책을 따른다.
- 전체 화면 배경과 입력 차단 오버레이는 Stretch, AnchoredPosition `(0,0)`, SizeDelta `(0,0)`을 기본으로 한다.
- 형제 UI 렌더 순서는 Hierarchy Sibling 순서로 관리하고 추가 Canvas와 Sorting Order 사용은 필요한 경우로 제한한다.

### RectTransform

- 모든 UI에 같은 앵커를 강제하지 않고 요소 역할에 맞는 앵커를 선택한다.
- 좌상단 수동 배치 요소는 Anchor Min/Max와 Pivot을 `(0,1)`로 설정한다.
- 좌상단 기준에서 오른쪽은 X 양수, 아래는 Y 음수를 사용한다.
- 전체 페이지·배경·반응형 패널은 Anchor Min `(0,0)`, Anchor Max `(1,1)`로 Stretch한다.
- 상단 가로 영역은 X축 Stretch와 상단 앵커를 사용하고 높이만 고정한다.
- 중앙 카드·모달은 중앙 앵커와 Pivot, 우측 정렬 요소는 우측 앵커와 음수 X 여백을 사용한다.
- Stretch 요소의 음수 SizeDelta는 부모 가장자리 안쪽 여백으로만 사용한다.
- Anchor, Pivot, AnchoredPosition과 SizeDelta를 한 세트로 검토한다.
- 부모 크기가 달라지는 UI를 절대 좌표만으로 배치하지 않는다.

### 목록과 동적 UI

- 일반 세로 목록과 그리드는 LayoutGroup과 ContentSizeFitter를 우선 사용한다.
- 목록과 그리드 시작 방향은 기본적으로 좌측 상단으로 한다.
- 항목 크기는 반복 항목 Prefab의 RectTransform 또는 LayoutElement에서 정의한다.
- 동적 항목은 지정된 Content 자식으로 생성하고 재구성 전 기존 자식과 리스너를 정리한다.
- LayoutGroup이 위치를 관리하는 자식의 AnchoredPosition을 직접 변경하지 않는다.
- Hover 확대, 자유 드래그, 겹침처럼 자동 레이아웃으로 표현할 수 없을 때만 수동 배치를 사용한다.
- 수동 배치로 전환하면 충돌하는 LayoutGroup과 ContentSizeFitter를 비활성화한다.
- 드래그 고스트는 전용 Drag Layer에서 표시하고 Raycast를 차단하지 않으며 종료 시 제거한다.
- 레이아웃에서 제외할 오버레이에는 `LayoutElement.ignoreLayout`을 사용한다.
- ScrollRect의 Viewport는 부모에 Stretch하고 Content는 스크롤 방향에 맞는 Pivot을 사용한다.

### 페이지와 탭

- 같은 그룹의 탭은 하나만 활성화한다.
- 팝업은 현재 페이지를 닫지 않고 Popup Layer에서 연다.
- 페이지를 열기 전에 필요한 Context와 표시 데이터를 전달한다.
- 탭 버튼과 탭 페이지의 개수·인덱스 대응을 전환 전에 검증한다.
- 기본 탭도 일반 전환 경로를 사용하여 선택 색상, Sibling 순서와 페이지 상태를 일치시킨다.

### UI GameObject 명명

- 레이어 `lay`, 페이지 `pag`, 탭 `tab`, 팝업 `pop` 접두사를 사용한다.
- 위치 컨테이너 `pos`, 논리 그룹 `grp`를 사용한다.
- 버튼 `btn`, 이미지 `img`, 텍스트 `txt`, 입력 필드 `inp`, 슬라이더 `sld`, 스크롤 뷰 `scv`를 사용한다.
- C# 타입 접미사와 Scene GameObject 접두사 규칙을 구분한다.

## 8. UI 생성 원칙

- 페이지, 패널, 배경, 제목, 고정 버튼, 토글과 고정 정보 영역은 Unity Editor에서 Scene에 직접 생성하고 직렬화한다.
- 공지, 출석, 설정, 뒤로가기, 닫기와 페이지 전환 버튼처럼 항상 존재하는 UI는 런타임 생성 대상으로 돌리지 않는다.
- Scroll View, Viewport, Content와 LayoutGroup은 Scene에 배치하고 수량이 변하는 반복 항목만 Prefab으로 생성한다.
- Prefab은 목록 행, 카드, 인벤토리 슬롯, 생성 적 표시처럼 실행 중 개수나 존재 여부가 달라지는 반복 단위에 사용한다.
- 한 화면에서 한 번만 사용하는 페이지, 패널, 버튼과 고정 정보 영역은 Prefab으로 분리하지 않는다.
- 정적 UI를 런타임에 조립하는 스크립트만 작성한 상태는 UI 생성 완료로 보지 않는다.
- `Awake`, `Start`, `Open`에서 `new GameObject`, `AddComponent`와 RectTransform 하드코딩으로 고정 화면을 만들지 않는다.
- Scene에 있어야 할 UI가 누락되었을 때 런타임 fallback으로 자동 복구하지 않고 누락을 보고해 Scene을 수정한다.
- 고정 UI는 `[SerializeField]` 참조 또는 검증 가능한 바인딩으로 연결한다.
- 런타임 코드는 디자이너가 저장한 Anchor, Pivot, 위치, 크기, Sibling 순서와 스타일을 덮어쓰지 않는다.
- 동적 UI는 미리 만든 Prefab을 Instantiate하고 `Setup` 또는 `Initialize` API로 데이터와 소유자를 전달한다.
- 동적 Prefab과 Content는 `[SerializeField]`로 연결하며 `Resources.Load`나 이름·계층 검색에 의존하지 않는다.
- 기존에 같은 역할의 Prefab이 있으면 재사용 가능성을 먼저 확인한다.
- 일회성 Editor 생성 도구를 사용하면 결과 UI가 Scene 또는 Prefab에 직렬화되어 저장되어야 한다.
- Editor 생성·마이그레이션 도구는 사용자가 명시적으로 실행할 때만 동작한다.
- Domain Reload, Play Mode, Scene 열기·저장을 계기로 디자이너 배치와 스타일을 자동 재적용하지 않는다.
- 도구 재실행 시 기존 디자이너 소유 UI를 기본값으로 되돌리지 않는다. 자동 수정은 누락 참조나 명시된 마이그레이션 범위로 제한한다.
- Unity Editor를 실행할 수 없어 실제 배치하지 못하면 스크립트만으로 완료 처리하지 않고 미배치 상태와 수동 작업을 명시한다.
- UI 생성 요청은 요청 범위의 Scene과 반복 요소 Prefab 수정을 허용한 것으로 보되 관계없는 기존 계층은 변경하지 않는다.

## 9. 씬·프리팹·에셋 스키마

- Editor Script 변경 전 관련 씬과 프리팹을 확인하고 기존 요소를 유지한 채 요청 범위만 반영한다.
- 동적 UI 제거 시 등록 리스너, 드래그 고스트와 임시 오브젝트를 정리한다.
- UI 추가 후 1920x1080과 다른 화면 비율에서 Canvas Scaler와 앵커 동작을 확인한다.
- 에셋 스키마 변경은 명시적인 마이그레이션 도구와 검증 결과를 사용한다.
- `OnValidate`에서 디자이너가 작성한 대상, 수치, 목록 순서와 프레젠테이션 값을 자동 변환하거나 덮어쓰지 않는다.
- 기존 직렬화 값을 변경하는 마이그레이션은 대상 에셋 수와 변경 결과를 보고한다.

## 10. 패키지와 의존성

- 불필요한 새 패키지를 추가하지 않는다.
- 패키지 추가·제거·버전 변경 전 `Packages/manifest.json`과 `packages-lock.json` 영향을 설명한다.
- Odin 전용 구현을 추가하기 전에 런타임 빌드가 아닌 에디터 기능에만 필요한지 확인한다.
- 에디터 전용 코드는 `Assets/10_Scripts/Editor` 아래 또는 Editor 전용 asmdef에 둔다.
- 기존 플러그인을 새 버전으로 교체하기 전에 라이선스, API 호환성과 직렬화 영향을 확인한다.

## 11. 검증과 완료 조건

### 코드와 콘텐츠 검증

- C# 변경 후 `dotnet build PS260714.slnx`로 스크립트 빌드를 확인한다.
- 관련 EditMode 테스트를 실행하고 실제 씬·입력·페이지 흐름 변경에는 PlayMode 테스트를 우선한다.
- Unity Editor 또는 Test Runner를 실행하지 못하면 미실행 사실과 Inspector·직렬화 영향을 명시한다.
- 전체 Unity 생성 프로젝트 그래프의 패키지 경고와 프로젝트 코드 경고를 구분해 보고한다.
- 현재 테스트 코드에는 EditMode 표식 약 560개와 PlayMode 표식 1개가 있다. 표식 수를 실제 통과 수로 보고하지 않는다.
- 순수 로직 확장 시 ID, 대상 선택, 조건, 효과 계산 회귀 테스트를 추가한다.
- 페이지 이동, 저장·불러오기와 전투 시작 흐름에는 PlayMode 테스트를 우선한다.

### 공통 능력 완료 조건

- 캐릭터, 적, 전투 아이템, 전투 카드, 상태 효과 경로를 모두 검색해 누락 여부를 확인한다.
- 관련 능력 소유자별 EditMode 회귀 테스트를 실행한다.
- 한 소유자에서만 성공하는 테스트로 공통 변경을 완료 처리하지 않는다.
- 공통 에디터, 기본값, 검증, 로컬라이제이션 변수와 설명도 같은 변경에서 확인한다.

### UI 완료 조건

- 기준 해상도와 다른 화면 비율에서 앵커와 Stretch 동작을 확인한다.
- 페이지 전환 후 이전 페이지 비활성화와 `Fresh`·`Resume` 상태를 확인한다.
- 팝업이 일반 UI보다 앞에, 툴팁이 팝업보다 앞에 표시되는지 확인한다.
- ScrollRect의 Content 크기, Mask, LayoutGroup과 ContentSizeFitter 조합을 확인한다.
- 동적 목록을 반복해 열어도 항목과 Button 리스너가 중복되지 않는지 확인한다.
- 드래그 종료와 페이지 닫기 시 임시 오브젝트와 Raycast 상태가 복구되는지 확인한다.

### 최종 보고

- 프로젝트 코드 컴파일 결과와 신규 프로젝트 경고 여부를 보고한다.
- 테스트 실행 범위와 미실행 범위를 구분한다.
- ID, null 참조, 직렬화와 Inspector 연결 영향을 확인한다.
- 수정 파일과 사용자 확인이 필요한 Unity Editor 절차를 요약한다.

