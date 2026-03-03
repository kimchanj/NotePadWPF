# NotePadWPF (WinMemo)

WPF 기반 메모장 프로젝트입니다. `.txt` 파일 편집을 중심으로 멀티 탭, 찾기/바꾸기, 상태바, 저장 확인 흐름을 제공합니다.

## 개발 환경
- .NET 8
- C# / WPF
- Windows 10/11

## 실행 방법
```powershell
dotnet build .\NotePadWPF.sln
dotnet run --project .\NotePadWPF\NotePadWPF.csproj
```

## 스프린트 진행 현황 (Sprint 1~3)

### Sprint 1 - 기본 편집기 골격
- 완료
- WPF 메인 윈도우/메뉴/상태바 구성
- 문서 모델(`DocumentModel`) 기반 편집 상태 관리
- 새 문서 탭 생성, 멀티 탭 UI, 탭 닫기 버튼
- 단축키(`Ctrl+N/O/S`, `Ctrl+Shift+S`, `Ctrl+F/H`, `F3`) 연결

### Sprint 2 - 파일 입출력/문서 수명주기
- 완료
- 파일 열기/저장/다른 이름으로 저장
- UTF-8 우선 + ANSI 폴백 읽기
- 저장 시 CRLF 정규화
- Dirty 상태 표시(탭 헤더 `●`)
- 탭 닫기/앱 종료 시 저장 여부 확인
- 동일 파일 중복 열기 방지

### Sprint 3 - 편집 생산성 기능
- 완료
- 실행 취소/다시 실행
- 잘라내기/복사/붙여넣기/모두 선택
- 찾기/바꾸기 패널
- 다음 찾기, 1건 바꾸기, 전체 바꾸기
- Match Case 옵션
- 캐럿 기준 줄/열 상태바 표시
- 자동 줄바꿈 토글, 상태바 표시 토글

## 다음 스프린트 후보
- 최근 파일 목록 저장/복원
- 자동 저장/복구
- 인코딩/줄바꿈 선택 저장 UX 확장
- Markdown 미리보기

## 운영 규칙
- 푸시 전 `README.md`의 진행 내역(스프린트/기능 상태)을 함께 업데이트합니다.
- 로컬 `pre-push` 훅으로 코드 변경 대비 `README.md` 미반영 푸시를 차단합니다.

