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

function serviceCard(service) {
  const root = service.root.path ?? "not configured";
  const reason = service.status.reason ?? service.root.reason ?? "observed";
  return `
    <article class="card">
      <div class="card-head">
        <h2>${escapeHtml(service.name)}</h2>
        <span class="state state-${escapeHtml(service.state)}">${escapeHtml(service.state)}</span>
      </div>
      <p class="path">${escapeHtml(root)}</p>
      <p class="reason">${escapeHtml(reason)}</p>
      <pre>${escapeHtml(statusJson(service))}</pre>
    </article>`;
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
      .state-unknown { color: #8a4b00; background: #ffe3b3; }
      .state-absent { color: #6a3140; background: #f5d9df; }
      .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; }
      .card { background: #fffdf8; border: 1px solid #a9aa9f; border-radius: .75rem; padding: 1rem; box-shadow: 4px 4px 0 #d2cdbf; }
      .card-head { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
      .path, .reason { overflow-wrap: anywhere; font-size: .75rem; }
      .path { color: #53605a; }
      .reason { color: #8a4b00; min-height: 1rem; }
      pre { background: #eef1eb; border-radius: .5rem; padding: .75rem; overflow: auto; max-height: 14rem; font-size: .72rem; }
      details { margin-top: 1.5rem; }
      summary { cursor: pointer; }
    </style>
  </head>
  <body>
    <header>
      <h1>STS2 Platform Workbench</h1>
      <div class="meta">
        <span>read-only filesystem view</span>
        <span class="state state-${escapeHtml(status.overall.state)}">overall: ${escapeHtml(status.overall.state)}</span>
        <span>${escapeHtml(status.generated_at)}</span>
      </div>
    </header>
    <main class="grid">${serviceCards}
    </main>
    <details>
      <summary>Raw service DTO</summary>
      <pre>${serialized}</pre>
    </details>
  </body>
</html>`;
}
