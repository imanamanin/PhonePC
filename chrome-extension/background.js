import { Scan } from "./scan.js";

chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {
  // Older Chrome without side panel still loads the viewer from the action.
});

chrome.runtime.onInstalled.addListener(() => {
  chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
});

chrome.runtime.onConnect.addListener((port) => {
  if (port.name === "scan") {
    attachScan(port);
  } else if (port.name === "phone") {
    attachPhone(port);
  }
  try {
    port.postMessage({ type: "ready" });
  } catch {
    // Port closed immediately.
  }
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "open-window") {
    chrome.windows.create({
      url: chrome.runtime.getURL("viewer.html") + "?mode=window",
      type: "popup",
      width: 420,
      height: 820,
      focused: true
    });
    sendResponse({ ok: true });
    return true;
  }
  if (message?.type === "open-viewer") {
    chrome.tabs.create({ url: chrome.runtime.getURL("viewer.html") });
    sendResponse({ ok: true });
    return true;
  }
  return false;
});

function attachScan(port) {
  let cancelled = false;
  port.onDisconnect.addListener(() => {
    cancelled = true;
  });
  port.onMessage.addListener((message) => {
    if (message?.type !== "start") return;
    (async () => {
      try {
      const mode = message.mode === "wifi" ? "wifi" : "usb";
      const local = await Scan.collectLocal(message.local || [], ["pcphone.local", "pc-phone.local"]);
      const wifiIfaces = local.filter((item) => Scan.isWifiIface(item));
      const shown = mode === "wifi"
        ? wifiIfaces.map((item) => item.address)
        : local.map((item) => item.address);
      const localIps = shown.join(", ");
      try {
        port.postMessage({
          type: "progress",
          host: localIps || (mode === "wifi" ? "وای‌فای کامپیوتر خاموش است" : "IP محلی پیدا نشد"),
          scanned: 0,
          total: 0,
          localIps,
          wifiReady: wifiIfaces.length > 0
        });
      } catch {
        cancelled = true;
        return;
      }
      const hosts = Scan.buildCandidates(message.manualHost, mode, local);
      const hit = await Scan.scan(
        hosts,
        (progress) => {
          if (cancelled) return;
          try {
            port.postMessage({ type: "progress", ...progress, lastError: Scan.lastError || "" });
          } catch {
            cancelled = true;
          }
        },
        () => cancelled,
        mode
      );
      if (cancelled) return;
      try {
        if (hit) port.postMessage({ type: "hit", hit });
        port.postMessage({
          type: "done",
          lastError: Scan.lastError || "",
          localIps,
          wifiReady: wifiIfaces.length > 0
        });
      } catch {
        // Viewer closed.
      }
      } catch (error) {
        try {
          port.postMessage({
            type: "done",
            lastError: error.message || String(error)
          });
        } catch {
          // Viewer closed.
        }
      }
    })();
  });
}

function attachPhone(port) {
  let socket = null;
  const drop = () => {
    try {
      socket?.close();
    } catch {
      // Already closed.
    }
    socket = null;
  };
  port.onMessage.addListener((message) => {
    if (message?.type === "connect" && message.host) {
      drop();
      const ws = new WebSocket(`ws://${message.host}:${Scan.PORT}/ws`);
      ws.binaryType = "arraybuffer";
      socket = ws;
      ws.addEventListener("open", () => {
        try {
          port.postMessage({ type: "open" });
        } catch {
          drop();
        }
      });
      ws.addEventListener("message", (event) => {
        try {
          if (typeof event.data === "string") {
            port.postMessage({ type: "text", data: event.data });
            return;
          }
          const src = new Uint8Array(event.data);
          const copy = new Uint8Array(src.byteLength);
          copy.set(src);
          port.postMessage({ type: "binary", data: copy.buffer });
        } catch {
          // Keep the socket; skip a bad media frame.
        }
      });
      ws.addEventListener("close", () => {
        try {
          port.postMessage({ type: "close" });
        } catch {
          // Port gone.
        }
        socket = null;
      });
      ws.addEventListener("error", () => {
        try {
          port.postMessage({ type: "error" });
        } catch {
          // Port gone.
        }
      });
      return;
    }
    if (message?.type === "send-text" && socket?.readyState === 1) {
      socket.send(message.data);
      return;
    }
    if (message?.type === "disconnect") {
      drop();
    }
  });
  port.onDisconnect.addListener(drop);
}
