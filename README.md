# CommonServiceProject

.NET 8 기반 **공용 워커 서비스 프레임워크**입니다. 다른 프로젝트의 개발자가 이 라이브러리를 참조하여
`BaseWorkerService`(또는 `StepWorkerService`)를 상속받는 것만으로 스케줄 기반 백그라운드 워커를 손쉽게
만들 수 있도록 설계했습니다.

## 주요 기능

- **스케줄링** (`WorkerSchedule`)
  - `AlignedMinuteInterval`: 매시 정각 기준 N분 간격 (예: 10분 → 00, 10, 20, 30, 40, 50분 00초)
  - `SpecificTimes`: 특정 시각 리스트 (예: `"09:00:00"`, `"18:00:00"`)
  - `Interval`: 고정 간격
- **중복 실행 방지 / 타임아웃 / 재시도** (`WorkerOptions`)
  - `AllowConcurrentExecution`, `ExecutionTimeout`, `RetryCountOnFailure`, `RetryInterval`
  - `GracefulShutdownTimeout`: 서비스 종료 시 진행 중인 작업을 최대 얼마나 기다릴지 설정
- **설정 파일 기반 오버라이드** (`appsettings.json`)
  - `Workers:{워커 클래스명}:Schedule`, `Workers:{워커 클래스명}:Options` 섹션으로
    코드의 `ConfigureSchedule()`/`ConfigureOptions()` 기본값을 부분적으로 재정의 가능
  - 설정 변경 시 재배포 없이 즉시 반영(hot reload)
- **스텝 기반 워커** (`StepWorkerService`)
  - 실행 로직을 여러 스텝으로 나누고, 스텝별로 독립된 재시도 횟수/간격 적용
  - 한 스텝이 재시도를 모두 소진하면 이후 스텝은 실행하지 않고 즉시 최종 실패 처리
- **라이프사이클 훅**
  - `OnBeforeRunAsync`, `OnAfterRunAsync`, `OnErrorAsync`(재시도마다 호출),
    `OnFinalFailureAsync`(재시도를 모두 소진한 최종 실패 시 1회만 호출)
- **헬스체크 연동** (`WorkerHealthCheck` / `WorkerHealthCheckStatus`)
  - `IsRunning`, `LastRunTime`, `LastSuccessTime`, `FailureCount`, `CurrentRetryAttempt` 등 상태 추적
  - `Healthy` / `Degraded`(재시도 중) / `Unhealthy`(최종 실패) 3단계로 판정
- **그레이스풀 셧다운**
  - 동시 실행 모드에서도 진행 중인 작업을 추적하여 종료 시 완료를 대기하고, 관찰되지 않은 예외를 로깅

## 프로젝트 구조

```
CommonServiceProject/
├── CommonServiceProject.csproj      # 공용 워커 프레임워크 (Class Library, net8.0)
├── BaseWorkerService.cs             # 부모 추상 클래스 (스케줄/재시도/타임아웃/헬스체크 엔진)
├── StepWorkerService.cs             # 스텝 기반 실행을 지원하는 중간 추상 클래스
├── WorkerStep.cs                    # 스텝 정의 모델
├── StepExecutionException.cs        # 스텝 최종 실패 시 던져지는 예외
├── WorkerSchedule.cs                # 스케줄 계산기
├── WorkerScheduleOptions.cs         # 설정 파일 기반 스케줄 오버라이드 모델
├── WorkerOptions.cs                 # 중복방지/타임아웃/재시도/셧다운 옵션 모델
├── WorkerHealthCheck.cs             # IHealthCheck 연동
├── ServiceCollectionExtensions.cs   # AddCommonWorker<TWorker> DI 확장 메서드
│
└── Samples/
    ├── SingleServiceApp/            # 단일 워커 등록 예제 (StepWorkerService 사용)
    └── MultiServiceApp/             # 다중 워커(3~4개) 동시 등록 예제
```

## 빠른 시작

```csharp
public class MyWorker : BaseWorkerService
{
    public MyWorker(ILogger<MyWorker> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider) { }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(10);

    protected override WorkerOptions ConfigureOptions() => new()
    {
        AllowConcurrentExecution = false,
        ExecutionTimeout = TimeSpan.FromMinutes(30),
        RetryCountOnFailure = 2,
        RetryInterval = TimeSpan.FromSeconds(5)
    };

    protected override Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        // 비즈니스 로직
        return Task.CompletedTask;
    }
}
```

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCommonWorker<MyWorker>();
// 여러 번 호출하면 여러 워커를 동시에 등록할 수 있습니다.
var host = builder.Build();
host.Run();
```

### 스텝 기반 워커

```csharp
public class MyStepWorker : StepWorkerService
{
    public MyStepWorker(ILogger<MyStepWorker> logger, IServiceProvider sp) : base(logger, sp) { }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(10);

    protected override IReadOnlyList<WorkerStep> ConfigureSteps() =>
    [
        new WorkerStep("Fetch", async (sp, ct) => { /* ... */ })
        {
            RetryCountOnFailure = 3,
            RetryInterval = TimeSpan.FromSeconds(5)
        },
        new WorkerStep("Process", async (sp, ct) => { /* ... */ }),
        new WorkerStep("Save", async (sp, ct) => { /* ... */ })
    ];
}
```

### 설정 파일로 오버라이드 (appsettings.json)

```json
{
  "Workers": {
    "MyWorker": {
      "Schedule": { "Type": "AlignedMinuteInterval", "AlignedMinutes": 5 },
      "Options": { "RetryCountOnFailure": 3, "RetryInterval": "00:00:10" }
    }
  }
}
```

## 빌드 및 실행

```bash
# 1. 라이브러리 빌드 검증
dotnet build CommonServiceProject.csproj

# 2. 단일 서비스 테스트 실행
dotnet run --project Samples/SingleServiceApp/SingleServiceApp.csproj

# 3. 다중 서비스 테스트 실행
dotnet run --project Samples/MultiServiceApp/MultiServiceApp.csproj
```

## 요구 사항

- .NET 8.0 SDK
