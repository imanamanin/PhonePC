export function normalizeHost(value) {
  return String(value || "")
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/\/.*$/, "")
    .replace(/:\d+$/, "");
}

export async function findAgent(manualHost, options = {}) {
  const mode = options.mode === "wifi" ? "wifi" : "usb";
  options.onProgress?.({
    host: "در حال خواندن شبکه…",
    scanned: 0,
    total: 0
  });
  return scanThroughPort(manualHost, mode, options);
}

function scanThroughPort(manualHost, mode, options) {
  const port = chrome.runtime.connect({ name: "scan" });
  return new Promise((resolve) => {
    const found = [];
    let settled = false;
    let started = false;
    let lastError = "";
    let localIps = "";
    let wifiReady = false;
    const finish = () => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      try {
        port.disconnect();
      } catch {
        // Already gone.
      }
      resolve({ found, lastError, localIps, wifiReady });
    };
    const timer = setTimeout(finish, 25000);
    const start = () => {
      if (started || settled) return;
      started = true;
      try {
        port.postMessage({
          type: "start",
          mode,
          manualHost: normalizeHost(manualHost)
        });
      } catch {
        finish();
      }
    };
    port.onMessage.addListener((message) => {
      if (message?.type === "ready") {
        start();
        return;
      }
      if (message?.type === "progress") {
        if (message.localIps) localIps = message.localIps;
        if (typeof message.wifiReady === "boolean") wifiReady = message.wifiReady;
        options.onProgress?.(message);
      }
      if (message?.type === "hit" && message.hit) {
        found.push(message.hit);
      }
      if (message?.type === "done") {
        lastError = message.lastError || "";
        if (message.localIps) localIps = message.localIps;
        if (typeof message.wifiReady === "boolean") wifiReady = message.wifiReady;
        finish();
      }
    });
    port.onDisconnect.addListener(finish);
    setTimeout(start, 200);
  });
}
