import { Scan } from "./scan.js";

export function normalizeHost(value) {
  return Scan.normalizeHost(value);
}

export async function findAgent(manualHost, options = {}) {
  const mode = options.mode === "wifi" ? "wifi" : "usb";
  let lastError = "";
  options.onProgress?.({
    host: "در حال خواندن شبکه…",
    scanned: 0,
    total: 0
  });
  try {
    await ensureAccess();
    const local = await Scan.collectLocal([], ["pcphone.local", "pc-phone.local"]);
    const wifiIfaces = local.filter((item) => Scan.isWifiIface(item));
    const shown = mode === "wifi"
      ? wifiIfaces.map((item) => item.address)
      : local.map((item) => item.address);
    const localIps = shown.join(", ");
    const wifiReady = wifiIfaces.length > 0;
    options.onProgress?.({
      host: localIps || (mode === "wifi" ? "وای‌فای کامپیوتر خاموش است" : "IP محلی پیدا نشد"),
      scanned: 0,
      total: 0,
      localIps
    });
    const hosts = Scan.buildCandidates(manualHost, mode, local);
    const hit = await Scan.scan(
      hosts,
      (progress) => options.onProgress?.(progress),
      () => false,
      mode
    );
    lastError = Scan.lastError || "";
    if (hit) {
      return { found: [hit], lastError, localIps, wifiReady };
    }
    const viaSw = await scanThroughPort(manualHost, mode, local, options);
    if (viaSw.found.length) return { ...viaSw, localIps: viaSw.localIps || localIps, wifiReady };
    return {
      found: [],
      lastError: viaSw.lastError || lastError,
      localIps,
      wifiReady
    };
  } catch (error) {
    lastError = error.message || String(error);
    const viaSw = await scanThroughPort(manualHost, mode, [], options);
    if (viaSw.found.length) return viaSw;
    return { found: [], lastError, localIps: viaSw.localIps || "", wifiReady: viaSw.wifiReady };
  }
}

async function ensureAccess() {
  const origins = ["<all_urls>", "http://*/*", "ws://*/*"];
  try {
    if (!chrome.permissions?.contains) return true;
    if (await chrome.permissions.contains({ origins: ["<all_urls>"] })) return true;
    if (await chrome.permissions.contains({ origins: ["http://*/*"] })) return true;
    if (!chrome.permissions.request) return true;
    return await chrome.permissions.request({ origins: ["<all_urls>"] });
  } catch {
    return true;
  }
}

function scanThroughPort(manualHost, mode, local, options) {
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
    const timer = setTimeout(finish, 20000);
    const start = () => {
      if (started || settled) return;
      started = true;
      try {
        port.postMessage({
          type: "start",
          mode,
          manualHost: normalizeHost(manualHost),
          local
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
