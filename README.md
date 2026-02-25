# typenG (MVP)

투명 오버레이(Always on top)에서 한 줄씩 타자 연습을 진행하는 .NET 8 WPF 앱입니다.

## 주요 기능
- 투명 배경 + 상단 고정(WindowStyle=None, AllowsTransparency=True, Topmost=True)
- 현재 타이핑 줄 1줄만 표시
- 문자 단위 상태 표시
- 세로 깜빡임 커서로 현재 입력 위치 표시
- 영문 키 입력을 한글 두벌식으로 조합 입력 지원(IME 우회 입력)
- 한글 조합 중 자모(IME composition) 미리보기 표시
  - 미입력: 반투명(Opacity 0.35)
  - 정답: 불투명
  - 오타 위치: 빨간색
- Backspace 시 상태 복원
- 줄 완전 정답 입력 후 Space/Enter로 다음 줄 이동
- 줄/문장/결과 전환 시 위로 빠른 스크롤 애니메이션
- 문장 완료 시 `CPM / WPM / ACC` 단일 행 표시
- 결과 화면 Enter 또는 우클릭 `다른 문장 하기`로 다음 문장 전환
- 우클릭 메뉴
  - 화면 조정 시작/끝 (기본 잠금, 조정 시 우하단 리사이즈 그립 표시)
  - 다른 문장 하기
  - 글자 색상 흰색/검정 토글
  - 프로그램 종료
- 좌클릭 드래그로 창 이동

## 빌드/실행
> WPF는 Windows 데스크톱에서 실행됩니다.

```bash
dotnet restore
dotnet build typenG.csproj -c Release
```

Visual Studio 또는 Rider에서 `typenG.csproj`를 열고 실행하면 됩니다.

## 프로젝트 구조
- `App.xaml`, `App.xaml.cs`
- `MainWindow.xaml`, `MainWindow.xaml.cs`
- `TypingEngine.cs` (입력 판정/줄 상태/통계)
- `PassageProvider.cs` (내장 문장 순환)
- `Models/TypingStats.cs` (CPM/WPM/ACC 계산)
