# Claude Code 개발 지시서: C# 공용 워커 서비스 프레임워크 (CLAUDE.md)

이 문서는 **Claude Code**가 요구사항과 아키텍처 명세를 바탕으로 C# 공용 워커 서비스 프레임워크 및 테스트 프로젝트를 **직접 코딩하고 구축**하도록 안내하는 지시서입니다.

---

## 📌 1. 프로젝트 개요

C# .NET 환경에서 타 프로젝트 개발자가 상속받아 사용할 수 있는 **공용 워커 서비스 프레임워크 라이브러리(`CommonServiceProject`)**와 이를 검증하는 **2개의 테스트 애플리케이션(`SingleServiceApp`, `MultiServiceApp`)**을 개발합니다.

---

## 📂 2. 솔루션 및 디렉터리 구조

Claude Code는 아래 구조로 프로젝트 파일 및 C# 소스 코드를 생성해야 합니다.

```
d:\Project\CommonServiceProject\
├── CLAUDE.md                               # 본 개발 지시서 문서
├── CommonServiceProject.csproj             # 공용 워커 프레임워크 Class Library (.NET 8.0)
├── BaseWorkerService.cs                    # 부모 추상 클래스 (개발자가 상속받아 RunAsync 구현)
├── WorkerSchedule.cs                       # 스케줄 계산기 (정분 간격 & 특정 시각 리스트)
├── WorkerOptions.cs                        # 중복방지, 타임아웃, 예외 재시도 설정 모델
├── WorkerHealthCheck.cs                    # 상태 모니터링 & .NET IHealthCheck 연동
├── ServiceCollectionExtensions.cs          # AddCommonWorker<TWorker> DI 확장 메서드
│
└── Samples/
    ├── SingleServiceApp/                   # [테스트 1] 단일 서비스 전용 앱
    │   ├── SingleServiceApp.csproj
    │   ├── MySingleWorker.cs               (단일 워커 구현체)
    │   └── Program.cs                      (단일 등록 및 실행)
    │
    └── MultiServiceApp/                    # [테스트 2] 다중 서비스 동시 구동 앱
        ├── MultiServiceApp.csproj
        ├── OrderProcessingWorker.cs        (매시 10분 정각 간격 워커)
        ├── DailyReportWorker.cs            (매일 09:00, 18:00 실행 워커)
        ├── DatabaseCleanupWorker.cs        (매시 30분 정각 간격 워커)
        └── Program.cs                      (1개 프로세스에서 3개 워커 동시 등록)
```

---

## 📋 3. 컴포넌트별 상세 개발 명세

### 3.1 `CommonServiceProject.csproj` (라이브러리 프로젝트)
- Target Framework: `net8.0`
- NuGet 패키지 의존성:
  - `Microsoft.Extensions.Hosting.Abstractions` (v8.0.0)
  - `Microsoft.Extensions.Logging.Abstractions` (v8.0.0)
  - `Microsoft.Extensions.Options` (v8.0.0)
  - `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` (v8.0.0)

---

### 3.2 `WorkerSchedule.cs` (스케줄링 계산 엔진)
- **목적**: 작업 실행 시각과 남은 대기 시간(`TimeSpan GetNextDelay(DateTime now)`)을 정확히 계산.
- **지원할 스케줄 타입 (`ScheduleType` enum)**:
  1. `AlignedMinuteInterval` (정각 기준 분 간격):
     - 분 간격 `N` (1~60)을 입력받음.
     - 현재 시간과 무관하게 매시 **00분, N분, 2N분...** 정각 00초에 맞추어 다음 실행 시각 계산.
     - 예: 10분 간격 ➡️ 00, 10, 20, 30, 40, 50분 00초. (시각 계산 시 60분 도달 시 다음 시 정각으로 처리)
  2. `SpecificTimes` (특정 시각 리스트):
     - 문자열 시각 리스트 (예: `["09:00:00", "12:30:00", "18:00:00"]`)를 입력받아 `List<TimeOnly>`로 파싱.
     - 현재 시각 이후 가장 가까운 목표 시각을 계산하며, 오늘의 모든 시각이 지난 경우 내일의 첫 번째 시각으로 계산.
  3. `Interval` (고정 간격):
     - `TimeSpan` 주기로 일반 대기.
- **팩토리 메서드 구현**: `FromAlignedMinutes(int minutes)`, `FromSpecificTimes(params string[] times)`, `FromInterval(TimeSpan interval)`

---

### 3.3 `WorkerOptions.cs` (동작 옵션 모델)
- `bool AllowConcurrentExecution`: 이전 차수 작업이 진행 중일 때 다음 차수 중복 실행 허용 여부 (기본값: `false`)
- `TimeSpan? ExecutionTimeout`: 1회 실행 시 최대 허용 타임아웃 (기본값: `30분`)
- `int RetryCountOnFailure`: 작업 예외 발생 시 최대 재시도 횟수 (기본값: `2회`)
- `TimeSpan RetryInterval`: 재시도 간격 (기본값: `5초`)

---

### 3.4 `BaseWorkerService.cs` (핵심 부모 추상 클래스)
- **클래스 정의**: `public abstract class BaseWorkerService : BackgroundService`
- **의존성 주입**: `ILogger`, `IServiceProvider` 받음
- **개발자가 오버라이드할 메서드**:
  - `protected abstract Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken);` (비즈니스 로직)
  - `protected virtual WorkerSchedule ConfigureSchedule()` (기본값: 정각 10분 간격)
  - `protected virtual WorkerOptions ConfigureOptions()` (기본값: 디폴트 옵션)
- **라이프사이클 & 후크 메서드**:
  - `protected virtual Task OnBeforeRunAsync(CancellationToken ct)`
  - `protected virtual Task OnAfterRunAsync(TimeSpan elapsedTime, CancellationToken ct)`
  - `protected virtual Task OnErrorAsync(Exception ex, int retryAttempt, CancellationToken ct)`
- **구동 로직 (`ExecuteAsync`) 필수 구현사항**:
  1. `ConfigureSchedule()`로 스케줄 계산 후 `Task.Delay` 수행.
  2. **중복 실행 방지**: `AllowConcurrentExecution == false`일 때 이전 작업 실행 중이면 스킵 및 경고 로그.
  3. **Scoped DI 자동 생성**: 실행 시마다 `_serviceProvider.CreateScope()`를 생성하여 `RunAsync`에 `scope.ServiceProvider` 전달.
  4. **타임아웃 & 재시도 로직**: `ExecutionTimeout` 초과 시 `CancellationTokenSource` 취소, 실패 시 `RetryCountOnFailure`만큼 재시도 후 `OnErrorAsync` 호출.
  5. **상태 추적**: `WorkerHealthCheckStatus` 객체에 마지막 실행 시간, 성공 시간, 수행 소요시간, 실패 횟수 기록.

---

### 3.5 `WorkerHealthCheck.cs` (모니터링 연동)
- `IHealthCheck` 구현.
- 주입받은 `IEnumerable<BaseWorkerService>`의 `HealthStatus` 상태를 수집하여 헬스체크 결과(`HealthCheckResult`) 반환.

---

### 3.6 `ServiceCollectionExtensions.cs` (DI 확장 메서드)
- `public static IServiceCollection AddCommonWorker<TWorker>(this IServiceCollection services) where TWorker : BaseWorkerService`
- `TWorker`를 `Singleton`으로 등록하고, `BaseWorkerService` 및 `IHostedService`로 함께 바인딩하여 단일 및 다중 워커 등록 모두 지원.

---

## 🧪 4. 테스트 애플리케이션 명세 (Samples)

### 4.1 `Samples/SingleServiceApp` (단일 서비스 테스트)
- `MySingleWorker` (`BaseWorkerService` 상속): 정각 10분 간격 스케줄 설정 및 로그 출력 로직 작성.
- `Program.cs`: `builder.Services.AddCommonWorker<MySingleWorker>()` 단일 등록 후 실행.

### 4.2 `Samples/MultiServiceApp` (다중 서비스 테스트)
- 3개 워커 작성:
  1. `OrderProcessingWorker`: 정각 10분 간격 (`FromAlignedMinutes(10)`)
  2. `DailyReportWorker`: 특정 시각 리스트 (`FromSpecificTimes("09:00:00", "18:00:00")`)
  3. `DatabaseCleanupWorker`: 정각 30분 간격 (`FromAlignedMinutes(30)`)
- `Program.cs`: 한 프로그램 내에서 `AddCommonWorker`를 3번 호출하여 3개 워커를 동시 구동.

---

## ✅ 5. 최종 검증 방법

Claude Code는 코딩 작성 후 아래 명령어로 빌드를 검증하고 실행할 수 있습니다.

```bash
# 1. 라이브러리 빌드 검증
dotnet build d:/Project/CommonServiceProject/CommonServiceProject.csproj

# 2. 단일 서비스 테스트 실행
dotnet run --project d:/Project/CommonServiceProject/Samples/SingleServiceApp/SingleServiceApp.csproj

# 3. 다중 서비스 테스트 실행
dotnet run --project d:/Project/CommonServiceProject/Samples/MultiServiceApp/MultiServiceApp.csproj
```
