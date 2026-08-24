using System.Runtime.InteropServices;

namespace CommonServiceProject;

/// <summary>
/// Windows 콘솔의 "빠른 편집 모드(QuickEdit Mode)"를 비활성화합니다.
/// QuickEdit 모드가 켜져 있으면 사용자가 콘솔 창을 클릭하거나 텍스트를 드래그하는 순간
/// 콘솔 입력 버퍼가 선택 대기 상태로 멈추면서, 그 프로세스의 워커 스케줄 루프(Task.Delay, 로그 출력 등)가
/// 클릭을 해제할 때까지 정지되는 현상이 발생합니다. 프로세스 시작 시 이 메서드를 호출하면
/// 클릭/드래그로 인한 정지 없이 워커가 계속 동작합니다.
/// </summary>
public static class ConsoleQuickEditGuard
{
    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    public static void Disable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = GetStdHandle(STD_INPUT_HANDLE);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return;
        }

        if (!GetConsoleMode(handle, out var mode))
        {
            return;
        }

        mode &= ~ENABLE_QUICK_EDIT_MODE;
        mode |= ENABLE_EXTENDED_FLAGS;

        SetConsoleMode(handle, mode);
    }
}
