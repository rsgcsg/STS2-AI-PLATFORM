import http from "node:http";
import { URL } from "node:url";

import { renderHtml } from "./render-html.mjs";

function send(response, statusCode, contentType, body) {
  response.writeHead(statusCode, {
    "content-type": contentType,
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body)
  });
  response.end(body);
}

export function createWorkbenchServer(service) {
  return http.createServer(async (request, response) => {
    if (request.method !== "GET") {
      send(response, 405, "application/json; charset=utf-8", JSON.stringify({
        error: "method_not_allowed"
      }));
      return;
    }

    const url = new URL(request.url ?? "/", "http://localhost");
    if (url.pathname === "/api/status") {
      const status = await service.readStatus();
      send(response, 200, "application/json; charset=utf-8", `${JSON.stringify(status)}\n`);
      return;
    }
    if (url.pathname === "/") {
      const status = await service.readStatus();
      send(response, 200, "text/html; charset=utf-8", renderHtml(status));
      return;
    }
    send(response, 404, "application/json; charset=utf-8", JSON.stringify({
      error: "not_found"
    }));
  });
}
