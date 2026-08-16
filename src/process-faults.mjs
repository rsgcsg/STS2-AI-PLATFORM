import { spawnSync } from "node:child_process";

function requirePid(pid) {
  if (!Number.isSafeInteger(pid) || pid <= 0) throw new Error("A positive process ID is required.");
  return pid;
}

function invokeWindowsNtProcess(pid, operation) {
  requirePid(pid);
  if (process.platform !== "win32") {
    return { status: "unsupported_platform", operation, pid, code: null, error: null };
  }
  const method = operation === "suspend" ? "NtSuspendProcess" : "NtResumeProcess";
  const script = [
    "$ErrorActionPreference='Stop'",
    "$native=Add-Type -PassThru -MemberDefinition '[System.Runtime.InteropServices.DllImport(\"ntdll.dll\")] public static extern int NtSuspendProcess(System.IntPtr processHandle); [System.Runtime.InteropServices.DllImport(\"ntdll.dll\")] public static extern int NtResumeProcess(System.IntPtr processHandle);' -Name NativeProcess -Namespace STS2Headless",
    `$process=[System.Diagnostics.Process]::GetProcessById(${pid})`,
    `$code=$native::${method}($process.Handle)`,
    "if($code -ne 0){throw \"NTSTATUS=$code\"}",
    "Write-Output $code"
  ].join(";");
  const result = spawnSync(
    "powershell",
    ["-NoProfile", "-NonInteractive", "-Command", script],
    { encoding: "utf8", timeout: 10_000, windowsHide: true }
  );
  return {
    status: result.status === 0 ? "applied" : "failed",
    operation,
    pid,
    code: result.status,
    error: result.status === 0 ? null : (result.stderr.trim() || result.stdout.trim() || "unknown")
  };
}

export function suspendProcess(pid) {
  return invokeWindowsNtProcess(pid, "suspend");
}

export function resumeProcess(pid) {
  return invokeWindowsNtProcess(pid, "resume");
}
