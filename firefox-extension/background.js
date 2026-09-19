const ext = globalThis.browser ?? globalThis.chrome;
const PANEL_WIDTH = 420;
const actionApi = ext.browserAction || ext.action;

actionApi?.onClicked?.addListener(() => {
  openRightPanel();
});

ext.runtime.onInstalled.addListener((details) => {
  if (details.reason === "install") {
    openRightPanel();
  }
});

ext.windows?.onRemoved?.addListener((windowId) => {
  ext.storage.local.get("panelWindowId").then((saved) => {
    if (saved.panelWindowId === windowId) {
      ext.storage.local.remove("panelWindowId");
    }
  });
});

ext.runtime.onConnect.addListener((port) => {
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

ext.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "open-window" || message?.type === "open-viewer") {
    openRightPanel().then(() => sendResponse({ ok: true }));
    return true;
  }
  if (message?.type === "scan") {
    const hosts = Scan.buildCandidates(message.manualHost, message.mode, message.local || []);
    Scan.scan(hosts, () => {}, () => false, message.mode).then((hit) => {
      sendResponse({ hit: hit || null, lastError: Scan.lastError || "" });
    });
    return true;
  }
  return undefined;
});

function attachScan(port) {
  let cancelled = false;
  port.onDisconnect.addListener(() => {
    cancelled = true;
  });
  port.onMessage.addListener((message) => {
    if (message?.type !== "start") return;
    (async () => {
      const hosts = Scan.buildCandidates(message.manualHost, message.mode, message.local || []);
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
        message.mode
      );
      if (cancelled) return;
      try {
        if (hit) port.postMessage({ type: "hit", hit });
        port.postMessage({ type: "done", lastError: Scan.lastError || "" });
      } catch {
        // Viewer closed.
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
          port.postMessage({ type: "binary", data: event.data });
        } catch {
          drop();
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

async function openRightPanel() {
  const anchor = await browserWindow();
  const height = Math.max(680, anchor?.height || 820);
  const top = Math.max(0, anchor?.top ?? 40);
  const left = Math.max(0, (anchor?.left ?? 0) + (anchor?.width ?? 1280) - PANEL_WIDTH);
  const saved = await ext.storage.local.get("panelWindowId");
  if (saved.panelWindowId) {
    try {
      await ext.windows.update(saved.panelWindowId, {
        focused: true,
        left,
        top,
        width: PANEL_WIDTH,
        height
      });
      return;
    } catch {
      await ext.storage.local.remove("panelWindowId");
    }
  }
  const created = await ext.windows.create({
    url: ext.runtime.getURL("viewer.html") + "?mode=window",
    type: "popup",
    width: PANEL_WIDTH,
    height,
    left,
    top,
    focused: true
  });
  if (created?.id) {
    await ext.storage.local.set({ panelWindowId: created.id });
    try {
      await ext.windows.update(created.id, {
        left,
        top,
        width: PANEL_WIDTH,
        height
      });
    } catch {
      // Some Firefox builds ignore a second move.
    }
  }
}

async function browserWindow() {
  try {
    const windows = await ext.windows.getAll({ windowTypes: ["normal"] });
    return windows.find((item) => item.focused) || windows[0] || null;
  } catch {
    try {
      return await ext.windows.getLastFocused();
    } catch {
      return null;
    }
  }
}
