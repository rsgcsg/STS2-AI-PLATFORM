const defaultSleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export async function waitForLoadedReadiness(
  probe,
  {
    timeoutMs = 15_000,
    intervalMs = 250,
    now = Date.now,
    sleep = defaultSleep
  } = {}
) {
  const deadline = now() + timeoutMs;
  let latest;
  let latestError;

  while (true) {
    try {
      latest = await probe();
      latestError = undefined;
      if (latest.ready) return latest;
    } catch (error) {
      latestError = error;
    }

    if (now() >= deadline) {
      if (latest !== undefined) return latest;
      throw latestError ?? new Error("Loaded readiness probe timed out without a result.");
    }
    await sleep(intervalMs);
  }
}
