function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function statusJson(service) {
  const value = service.status.value;
  if (value === undefined) return "No known status file was read.";
  return JSON.stringify(value, null, 2);
}

const DOMAIN_LABELS = Object.freeze({
  environment: "Environment",
  policy: "Policy",
  annotator: "Human Data",
  human_data: "Human Data Store",
  evidence: "Evidence",
  transfer: "Transfer",
  diagnostics: "Diagnostics"
});

function serviceCard(service) {
  const root = service.root.path ?? "not configured";
  const reason = service.status.reason ?? service.root.reason ?? "observed";
  return `
    <article class="card">
      <div class="card-head">
        <h2>${escapeHtml(DOMAIN_LABELS[service.name] ?? service.name)}</h2>
        <span class="state state-${escapeHtml(service.state)}">${escapeHtml(service.state)}</span>
      </div>
      <p class="path">${escapeHtml(root)}</p>
      <p class="source">source=${escapeHtml(service.source)}; freshness=${escapeHtml(service.freshness)}; partial=${escapeHtml(service.partial)}</p>
      <p class="reason">${escapeHtml(reason)}</p>
      <pre>${escapeHtml(statusJson(service))}</pre>
    </article>`;
}

function policyControls(status) {
  const policy = status.services.find((service) => service.name === "policy");
  const enabled = !status.read_only && policy?.source === "policy_runtime" && policy.state === "available";
  const modes = ["human", "shadow", "one_step", "auto"];
  const buttons = modes.map((mode) => `<button type="button" data-policy-mode="${mode}"${enabled ? "" : " disabled"}>${mode}</button>`).join("\n");
  return `<section class="card policy-controls">
    <div class="card-head"><h2>Policy controls</h2><span>${status.read_only ? "read-only: non-loopback bind" : enabled ? "live runtime" : "disabled: live runtime unavailable"}</span></div>
    <p>Only runtime mode is forwarded. No gameplay action, model, or human evidence is handled here.</p>
    <div class="buttons">${buttons}</div>
    <output id="policy-command-result" aria-live="polite"></output>
  </section>`;
}

export function renderHtml(status) {
  const serviceCards = status.services.map(serviceCard).join("\n");
  const serialized = escapeHtml(JSON.stringify(status, null, 2));
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>STS2 Platform Workbench</title>
    <style>
      :root { color-scheme: light; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; background: #f3f0e8; color: #1d241f; }
      body { margin: 0; padding: 2rem; max-width: 1100px; margin-inline: auto; }
      header { border-bottom: 2px solid #1d241f; margin-bottom: 1.5rem; padding-bottom: 1rem; }
      h1 { margin: 0 0 .5rem; font-size: clamp(1.5rem, 4vw, 2.75rem); }
      h2 { margin: 0; text-transform: capitalize; font-size: 1rem; }
      .meta { display: flex; gap: 1rem; flex-wrap: wrap; align-items: center; }
      .state { border: 1px solid currentColor; border-radius: 999px; padding: .2rem .6rem; font-size: .75rem; text-transform: uppercase; }
      .state-available { color: #126b42; background: #d8f2df; }
      .state-partial, .state-unknown { color: #8a4b00; background: #ffe3b3; }
      .state-absent, .state-unavailable { color: #6a3140; background: #f5d9df; }
      .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; }
      .card { background: #fffdf8; border: 1px solid #a9aa9f; border-radius: .75rem; padding: 1rem; box-shadow: 4px 4px 0 #d2cdbf; }
      .card-head { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
      .path, .reason, .source { overflow-wrap: anywhere; font-size: .75rem; }
      .path, .source { color: #53605a; }
      .reason { color: #8a4b00; min-height: 1rem; }
      pre { background: #eef1eb; border-radius: .5rem; padding: .75rem; overflow: auto; max-height: 14rem; font-size: .72rem; }
      .policy-controls { grid-column: 1 / -1; }
      .buttons { display: flex; gap: .5rem; flex-wrap: wrap; }
      button { border: 1px solid #1d241f; border-radius: .4rem; background: #f3f0e8; color: #1d241f; padding: .45rem .75rem; cursor: pointer; font: inherit; }
      button:disabled { cursor: not-allowed; opacity: .45; }
      output { display: block; margin-top: .75rem; min-height: 1.2rem; overflow-wrap: anywhere; }
      details { margin-top: 1.5rem; }
      summary { cursor: pointer; }
    </style>
  </head>
  <body>
    <header>
      <h1>STS2 Platform Workbench</h1>
      <div class="meta">
        <span>typed status view with explicit filesystem fallback</span>
        <span class="state state-${escapeHtml(status.overall.state)}">overall: ${escapeHtml(status.overall.state)}</span>
        <span>${escapeHtml(status.generated_at)}</span>
      </div>
    </header>
    <main class="grid">${serviceCards}
      ${policyControls(status)}
    </main>
    <details>
      <summary>Raw service DTO</summary>
      <pre>${serialized}</pre>
    </details>
    <script>
      document.querySelectorAll("[data-policy-mode]").forEach((button) => {
        button.addEventListener("click", async () => {
          const output = document.querySelector("#policy-command-result");
          output.textContent = "sending...";
          try {
            const response = await fetch("/api/policy/mode", {
              method: "POST",
              headers: { "content-type": "application/json" },
              body: JSON.stringify({ mode: button.dataset.policyMode })
            });
            const body = await response.json();
            output.textContent = response.ok ? "mode changed to " + body.mode : body.error + ": " + body.message;
          } catch (error) {
            output.textContent = "workbench command failed: " + error.message;
          }
        });
      });
    </script>
  </body>
</html>`;
}
