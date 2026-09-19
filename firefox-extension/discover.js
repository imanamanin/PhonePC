import { ext } from "./api.js";

export function normalizeHost(value) {
  return String(value || "")
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/\/.*$/, "")
    .replace(/:\d+$/, "");
}

export function isUsbHost(host) {
  return /^(192\.168\.42\.|192\.168\.137\.)/.test(host) ||
    (/^10\./.test(host) && !/^10\.0\.[01]\./.test(host));
}

export async function findAgent(manualHost, options = {}) {
  const mode = options.mode === "wifi" ? "wifi" : "usb";
  let local = [];
  let lastError = "";
  options.onProgress?.({
    host: "در حال خواندن شبکه…",
    scanned: 0,
    total: 0
  });

  try {
    const background = await ext.runtime.getBackgroundPage();
    if (background?.Scan?.collectLocal) {
      local = await background.Scan.collectLocal([], ["pcphone.local", "pc-phone.local"]);
    }
    const wifiIfaces = local.filter((item) => background?.Scan?.isWifiIface?.(item));
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
    if (background?.Scan) {
      const hosts = background.Scan.buildCandidates(manualHost, mode, local);
      const hit = await background.Scan.scan(
        hosts,
        (progress) => options.onProgress?.(progress),
        () => false,
        mode
      );
      lastError = background.Scan.lastError || "";
      return { found: hit ? [hit] : [], lastError, localIps, wifiReady };
    }
  } catch (error) {
    lastError = error.message || String(error);
  }

  const viaPort = await scanThroughPort(manualHost, mode, local, options);
  return {
    found: viaPort,
    lastError,
    localIps: local.map((item) => item.address).join(", "),
    wifiReady: local.some((item) => /wi-?fi|wlan|wireless/i.test(item?.name || ""))
  };
}

function scanThroughPort(manualHost, mode, local, options) {
  const port = ext.runtime.connect({ name: "scan" });
  return new Promise((resolve) => {
    const found = [];
    let settled = false;
    let started = false;
    const finish = (result) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      try {
        port.disconnect();
      } catch {
        // Already gone.
      }
      resolve(result);
    };
    const timer = setTimeout(() => finish(found), 25000);
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
        finish(found);
      }
    };
    port.onMessage.addListener((message) => {
      if (message?.type === "ready") {
        start();
        return;
      }
      if (message?.type === "progress") {
        options.onProgress?.(message);
      }
      if (message?.type === "hit" && message.hit) {
        found.push(message.hit);
      }
      if (message?.type === "done") {
        finish(found);
      }
    });
    port.onDisconnect.addListener(() => finish(found));
    setTimeout(start, 300);
  });
}
