# 인수인계

## 프로젝트 조사

- 작업 Unity 프로젝트: `digi/`. `My project/`도 동일 버전의 기본 템플릿이며 변경하지 않는다.
- Unity: 6000.6.0f1. URP 17.6.0, Input System 1.20.0, Test Framework 1.8.0, NGO 2.13.2, Unity Transport 6.6.0. 정확한 종속성은 `digi/Packages/packages-lock.json`.
- 기존 씬: `digi/Assets/Scenes/SampleScene.unity` (카메라, 빛, 볼륨만 있음). 기존 스크립트는 Unity 템플릿 Readme뿐. 사용할 캐릭터/애니메이션 에셋 없음.
- 최초 Git 상태: `.gitignore`만 추적, 두 Unity 프로젝트는 미추적. 기존 파일 삭제/초기화하지 않음.

## 실행

1. Unity Hub에서 저장소 아래 `digi/`를 Unity 6000.6.0f1로 연다. 루트 또는 `My project/`를 열지 않는다. 처음에는 Unity 패키지 복원 필요.
2. `Assets/Prototype/Prototype.unity`를 열고 Play. 규칙은 `Assets/Prototype/Rules.asset`의 Inspector에서 변경.
3. 한 인스턴스에서 HOST. 나머지는 호스트의 LAN IPv4와 같은 UDP 포트(기본 7777)를 입력하고 JOIN. 같은 PC는 127.0.0.1 사용. 호스트 bind는 모든 인터페이스. LAN 방화벽에서 해당 실행 파일/UDP 포트 허용 필요. Relay/공개 매칭은 없음.
4. 호스트는 악당, 접속자는 최대 3명 다수. 전원 접속 후 START ROUND. 빈자리 봇 옵션을 끄면 실제 접속자만 참가한다. 켜면 빈 슬롯을 연습 봇으로 채운다. 시작 후 중도 참가 불가.
5. 혼자서는 HOST → 봇 옵션 유지 → START ROUND로 악당 플레이 시험. 다수 측 혼자 시험하려면 두 번째 실행 파일로 접속하고 나머지 2슬롯을 봇으로 채운다.
6. Build Profiles에 Prototype이 첫 씬으로 등록되어 있다. 자동 빌드는 `scripts/Verify.ps1 -UnityEditor <설치된 Unity 실행 파일> -Build`. 출력 `digi/Builds/DigiPrototype.exe`. 실행 중인 동일 프로젝트 에디터는 배치 검증 전에 닫는다.
7. 한 판 종료 후 DISCONNECT / RETURN TO LOBBY로 각 인스턴스의 연결을 종료하고 새 호스트/접속을 시작한다.

**D005 현재 로컬 테스트 빌드:** `digi/Builds/BalancePreview/DigiPrototype.exe`. 이전 `digi/Builds/DigiPrototype.exe`가 실행 중이어서 덮어쓰거나 종료하지 않고 별도로 제공했다. 확대 맵을 시험하려면 모든 참가자가 새 빌드로 새 세션을 시작한다. 소스에서 다시 빌드할 때의 표준 출력은 여전히 `digi/Builds/DigiPrototype.exe`이다. 로컬 BalancePreview는 Git 제외.

## 조작과 목표

- WASD 이동, 마우스 오른쪽 버튼을 누른 채 시점/공격 방향 회전, 왼쪽 버튼 기본 공격. 공격 시 노랑, 피격 시 흰색으로 순간 표시.
- E 누르고 유지: 녹색 거점에서 대피, 조건 충족 후 중앙 파란 구조 장치에서 요청. 악당이 근처에 있으면 진행 중단.
- Q: 다수 측 개인 진화. F: 악당 센서 배치. Esc: 마우스 잠금 해제.
- 악당은 녹색 야생 개체/주황색 스캐빈저를 사냥해 성장. 다수는 대피/스캐빈저/보라색 센서 제거로 공유 XP를 얻는다. 같은 팀을 공격할 수 없다.
- 악당은 전멸 또는 임시 제한시간 경과로 승리. 다수는 악당 HP 0 또는 모든 야생 개체 해결 후 중앙 구조 20초로 승리.
- 악당 HUD에는 대피 거점 진행이 없으며, 독립 통신병 초상/자막/비방향성 신호음이 대피 진행 사실만 알린다. 센서의 CONTACT 표식은 별도로 악당에게만 표시.
- UI는 폰트 의존을 줄이기 위한 임시 영문. 세계관/규칙 문서는 한국어. 파워드라몬 표시명 Machinedramon은 영문 명칭.

## 구조

- `digi/Assets/Prototype/PrototypeConfig.cs`: 단계별 데이터와 조정 수치.
- `digi/Assets/Prototype/MatchSimulation.cs`: 서버 전용 판정, 입력 소유자 검증·속도 제한·유효성 검사·입력 만료, 수신자별 스냅샷.
- `digi/Assets/Prototype/PrototypeSession.cs`: NGO CustomMessagingManager, 승인/로비/연결, 입력, 카메라, 도형 표현, HUD/보고. 상태는 ReliableFragmentedSequenced, 입력은 ReliableSequenced. 프리팹 NetworkObject 대신 작은 월드 시뮬레이션을 직렬화하는 구조.
- `digi/Assets/Prototype/Editor/PrototypeTools.cs`: 씬 생성/Windows 빌드/검증 진입점. 기존 Prototype 씬이 있으면 재생성하지 않는다.
- `digi/Assets/Prototype/Editor/PrototypeVerification.cs`: 실제 서버 시뮬레이션을 사용하는 결정적 통합 검증.
- `scripts/Verify.ps1`, `scripts/NetworkSmoke.ps1`: 재현 가능한 검증. 빌드·로그·검증 복사본은 Git 제외.

## 검증 절차

규칙 검증: `scripts/Verify.ps1 -UnityEditor <Unity 실행 파일>` 또는 배치 `-executeMethod Digi.Prototype.Editor.PrototypeTools.Verify`. 성공 로그는 `DIGI_VERIFICATION_PASSED`. Build 진입점도 먼저 규칙 검증 실행.

다중 프로세스: 빌드 후 `scripts/NetworkSmoke.ps1`. 호스트+클라이언트 3개를 숨긴 창으로 실행한다. 자동 입력은 실제 입력 메시지 경로로 이동/대피/진화를 요청한다. 약 150초 후 공유 경험치 210, 해금 2, 성장기 복귀, 4명 실제 접속, 구조 승리, 악당 보고 격리, 액터 HP/단계/에너지 일치를 검사한다. 결과 `.verification/network/`. `-Player <빌드 실행 파일>`, `-Port <빈 UDP 포트>`로 변경 가능. 현재 별도 로컬 빌드에는 `-Player digi/Builds/BalancePreview/DigiPrototype.exe`를 명시한다.

수동 플레이 체크:

1. 호스트와 3접속자로 봇 없이 시작. 첫 거점 E 진행/중단/재개/악당 경합. 다수 3화면 XP·진행 일치, 악당 보고에 장소/방향/거리/인원/진행률 부재 확인.
2. 대피 1곳 완료 XP70과 Q 성숙기 확인. 약 12.5초 뒤 퇴화, XP·해금 유지. 나머지 대피 후 중앙 E 20초로 승리 확인.
3. 새 판에서 악당으로 야생 처치. 15초 뒤 해당 사냥터에 스캐빈저 2마리. 양측 처치 보상 1회, 이후 15초 이상 기다려 재생성 없음 확인.
4. 악당 F로 센서 배치. 다수 접근 시 악당에만 순간 위치 표식, 범위 이탈 후 3초 만료. 센서 파괴 XP20, 최대 3개 제한 확인.
5. 전투로 악당 3단계와 공격 형태 변화, 다수 협공 토벌, 전멸, 시간 종료, 연결 끊김을 각각 확인. 원격 LAN 4인 체감·카메라·음량을 검토.

## 현재 검증 결과

### D005 확대 맵 / 초반 악당 조정

- Unity 컴파일, 서버 통합 단언 **42개**, Windows Development 빌드 성공. 기존33개에 시작 분리, 맵3거점/확대, 속도 관계, 차폐·충돌·경계, 초기 사냥 경로, 야생2마리 후 진화, 확대 맵 봇 완주 검증 추가.
- 확대 맵에서 실제 호스트1+클라이언트3 자동 경기 **통과**. 게임 시간 약65.4초 구조 승리, 모든 클라이언트 XP210/해금2/3거점 완료·구조20초 이상/퇴화 상태와 액터HP·에너지 일치, 악당 보고3회·거점 배열0/다수 전용 정보 유출 없음. 이 경기는 무저항 호스트를 상대로 한 흐름 검증이며 악당 승률 검증이 아니다.
- 맵은120×120, 큰 차폐물9개, 북서 악당/남쪽 다수 시작, 일반 액터 시야32·이름표18. 초기 악당 속도6.2·첫 진화80. 확정 밸런스가 아니며 실제 플레이 평가 필요.
- 새 맵/시야는 같은 Rules.asset을 가진 빌드끼리 시험한다. 변경 전 빌드와 섞어 접속하지 않는다.
- Direct3D 11 렌더 캡처 확인: 북서 시작점에서 가까운 야생 거점이 보이고 다른 구역은 큰 차폐물/거리 제한으로 가려진다. 증빙 `docs/images/balance-world.png`. HUD/실제 마우스 조작 검증은 이전과 같이 별도 수동 확인 필요.
- 네트워크 스모크 결과는 이제 `.verification/network/<실행별 ID>/`에 분리해 이전 성공 파일을 잘못 재사용하지 않는다. 이번 실행은 스크립트 보강 전 시작하여 `.verification/network/` 직하에 기록됨.
- 후속 우선 확인: 초기 악당이 북서 거점 사냥 후 첫 진화까지 생존하는지, 성장기3인의 조기 추격이 여전히 쉬운지, 엄폐물 우회와 센서 가치, 진화 이후 느린 이동속도에 대한 체감. 실제 승률/교전 밸런스는 자동 완주 테스트로 판단할 수 없다.

### 이전 D004 프로토타입 검증 기록

- Unity 2.7.0 패키지 EntityId 오류 재현 → NGO 2.13.2에서 해결.
- Unity 에디터 배치 컴파일 및 서버 통합 검증 **33개 통과**. 입력 소유자/NaN/속도/입력 만료, 인원/중도 참가, 대피 경합/보상/보고, 팀 XP/해금/개인 진화/퇴화, 센서 수신자/만료/파괴/보상/상한, 야생·스캐빈저 중복 방지, 악당 3단계 유지, 구조·전멸·토벌·시간 종료를 검증.
- Windows Development 빌드 성공. 첫 무그래픽 실행의 셰이더 초기화 오류는 D004로 수정.
- **실제 NGO/Transport 호스트 1 + 클라이언트 3** 로컬 다중 프로세스 검증 성공 (`DIGI_NETWORK_SMOKE_PASSED`). 봇 없이 게임 시간 약 54.45초에 구조 승리. 모든 클라이언트 최종 XP210/해금2/성장기, 호스트와 HP·단계·에너지 일치. 악당 보고3회/거점 진행 배열0, 다수 보고0/센서 표식0. 결과 상태가 종료 시까지 유지됨.
- 센서의 **양성 감지/만료/파괴**는 서버 통합 검증에서 확인. 실제 다중 프로세스 스모크는 대피·구조 흐름이며 사냥/센서 전투를 수행하지 않았다. 원격 LAN, 지연/패킷 손실, 4명 수동 조작, 음향 청취는 아직 미검증.
- 기존 AppUI 패키지가 AppUISettings 부재로 기본 설정을 생성한다는 경고가 있으나 게임 예외는 없음. 그래픽 출력 검증은 아래 최종 기록 참조.
- 최종 빌드에서 Direct3D 11 카메라를 RenderTexture로 명시적으로 렌더링하고 결과 이미지를 확인했다. 악당/야생 도형, 대피 거점, 장애물, 바닥, 색상/그림자 정상. 증빙 `docs/images/prototype-world.png`는 **3D 장면만** 포함한다.
- 숨긴 Windows 창의 ScreenCapture는 검은 프레임을 반환했다. 따라서 HUD/버튼 배치와 실제 마우스 조작의 시각 검증을 통과했다고 주장하지 않는다. `-digiScreenshot <출력 파일>`은 일반 캡처와 `.world.png` 카메라 전용 출력을 함께 생성한다. 예: 빌드에 `-digiHost -digiAuto -digiPort 18890 -digiQuit 32 -digiScreenshot preview.png` 전달. 카메라 캡처는 25초 시점.
- 최종 카메라/캡처 수정 후에도 서버 통합 단언33개 및 Windows 빌드 재통과. 실제 다중 접속 성공은 동일 서버/전송 로직으로 수행했으며 이후 변경은 카메라 초기 방향과 캡처 진단뿐이다.
- 로컬 최종 실행 파일은 `digi/Builds/DigiPrototype.exe`. Builds는 Git 제외이므로 다른 PC에서는 다시 빌드해야 한다. 원격 push는 하지 않았다.
- 원본 프로젝트가 열린 상태여서 `.verification/`에 Assets/Packages/ProjectSettings를 복사해 검증. 기존 에디터/프로젝트를 강제 종료하지 않음. 생성된 씬·규칙·meta·lock을 원본에 반영.

## 알려진 제한 / 다음 작업

- 전용 아트/애니메이션/정식 사운드 없음. 맵은 런타임 생성 평면과 기둥, 점프 없음. 이동은 서버 계산 및 클라이언트 보간이며 예측/지연 보정 없음.
- 스캐빈저/연습 봇은 단순 조향이라 장애물 주변에 정체될 수 있다. NavMesh/전술 AI는 후속 작업.
- 일반 월드 액터 위치는 모든 클라이언트에 공유한다. 악당 전용 센서 표식/대피 보고는 별도 필터링하나 경쟁 출시용 관심영역 은닉·안티치트는 미구현.
- 스냅샷 JSON을 20Hz로 전송하므로 소규모 LAN 프로토타입 용도. 증분 상태/대역폭 최적화 필요.
- 호스트 이전/재접속 복구/다운·부활/공개 매칭/계정/상점/운영 서버 없음.
- 다음: 실제 LAN 4인 수동 플레이로 추격 속도·진화 보상·구조 시간 조정 → 결정을 기록 → 서버/네트워크 회귀 검증. 그 후 상위 진화/부활·구조 상세 기획 확정.
- 외부 의존: 설치된 Unity 및 manifest의 Unity 패키지뿐. 외부 캐릭터 에셋 다운로드/구매 없음. 비밀키 불필요.
