import { PcmPlayer } from "./audio.js";
import { findAgent } from "./discover.js";
import { bindFileDrop } from "./files.js";
import { PointerInput } from "./input.js";
import {
  decodeAudio,
  decodeVideo,
  envelope,
  isAudio,
  JPEG
} from "./protocol.js";

const hostInput = document.getElementById("host");
const statusEl = document.getElementById("status");
const hintEl = document.getElementById("hint");
const canvas = document.getElementById("screen");
const ctx = canvas.getContext("2d");
const stage = document.getElementById("stage");
const settingsEl = document.getElementById("settings");
const settingsToggle = document.getElementById("settings-toggle");
const popoutBtn = document.getElementById("popout");
const usbBtn = document.getElementById("connect-usb");
const wifiBtn = document.getElementById("connect-wifi");
const scanProgress = document.getElementById("scan-progress");
const detached = new URLSearchParams(location.search).get("mode") === "window";
const player = new PcmPlayer();
const input = new PointerInput(sendCommand);
const files = bindFileDrop({
  root: document.querySelector(".phone"),
  tray: document.getElementById("file-tray"),
  send: sendCommand,
  isConnected: () => agentOpen,
  setStatus
});
let agentPort = null;
let agentOpen = false;
let objectUrl = null;
let pingTimer = 0;
let statusTimer = 0;
let pairing = { pairingId: null, sessionToken: null };
let frameSize = { width: 0, height: 0 };
let connecting = false;

restore().then((mode) => {
  if (mode) connect(mode);
});
if (detached) {
  document.body.classList.add("window");
  popoutBtn.hidden = true;
}

settingsToggle.addEventListener("click", () => setSettingsOpen(!settingsEl.classList.contains("open")));
popoutBtn.addEventListener("click", () => chrome.runtime.sendMessage({ type: "open-window" }));
usbBtn.addEventListener("click", () => connect("usb"));
wifiBtn.addEventListener("click", () => connect("wifi"));

canvas.addEventListener("contextmenu", (event) => event.preventDefault());
canvas.addEventListener("pointerdown", (event) => {
  canvas.focus();
  input.onPointerDown(event, canvas);
});
canvas.addEventListener("pointermove", (event) => input.onPointerMove(event, canvas));
canvas.addEventListener("pointerup", (event) => input.onPointerUp(event, canvas));
canvas.addEventListener("wheel", (event) => {
  event.preventDefault();
  input.onWheel(event, canvas);
}, { passive: false });
canvas.addEventListener("keydown", (event) => {
  if (input.onKeyDown(event)) return;
  if (event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
    event.preventDefault();
    input.onText(event.key);
  }
});

async function restore() {
  const saved = await chrome.storage.local.get(["host", "pairingId", "sessionToken", "connectMode"]);
  if (saved.host) hostInput.value = saved.host;
  pairing = {
    pairingId: saved.pairingId || null,
    sessionToken: saved.sessionToken || null
  };
  return saved.connectMode === "wifi" || saved.connectMode === "usb" ? saved.connectMode : null;
}

async function connect(mode) {
  if (connecting) return;
  connecting = true;
  usbBtn.disabled = true;
  wifiBtn.disabled = true;
  const via = mode === "wifi" ? "wifi" : "usb";
  try {
    await player.unlock();
    setStatus(via === "wifi" ? "جستجو روی وای‌فای…" : "جستجو روی USB tether…", true);
    scanProgress.textContent = via === "wifi" ? "شبکه وای‌فای کامپیوتر اسکن می‌شود…" : "";
    const result = await findAgent(hostInput.value, {
      mode: via,
      onProgress: ({ host, scanned, total, localIps }) => {
        if (localIps) {
          scanProgress.textContent = `شبکه این کامپیوتر: ${localIps}`;
          return;
        }
        scanProgress.textContent = total ? `اسکن ${host} (${scanned}/${total})` : host;
        setStatus(via === "wifi" ? `وای‌فای: ${host}` : `USB: ${host}`);
      }
    });
    const found = result.found || result;
    if (!found.length) {
      const extra = result.lastError ? ` (${result.lastError})` : "";
      const ips = result.localIps ? ` شبکه PC: ${result.localIps}.` : "";
      scanProgress.textContent = "";
      if (via === "wifi") {
        setStatus(
          result.wifiReady
            ? `گوشی روی این وای‌فای پیدا نشد.${ips} گوشی را به همان روتر کامپیوتر وصل کنید.`
            : "وای‌فای کامپیوتر را روشن کنید و به همان روتر گوشی وصل شوید.",
          true
        );
      } else {
        setStatus(`USB پیدا نشد.${ips}${extra} تترینگ را روشن کنید.`, true);
      }
      return;
    }
    const host = found[0].host;
    hostInput.value = host;
    closeSocket();
    await chrome.storage.local.set({ host, connectMode: via });
    scanProgress.textContent = "";
    setStatus(`اتصال ${via === "wifi" ? "Wi-Fi" : "USB"} به ${host}…`);
    connectAgent(host, via);
  } finally {
    connecting = false;
    usbBtn.disabled = false;
    wifiBtn.disabled = false;
  }
}

function onJson(msg) {
  if (files.onMessage(msg)) {
    return;
  }
  if (msg.error) {
    if (msg.error.code === "UNPAIRED") {
      setStatus("اپ گوشی را به‌روز کنید تا اتصال بدون PIN کار کند.", true);
      return;
    }
    if (msg.error.code === "PERMISSION_DENIED") {
      setStatus(msg.error.message || "مجوز Capture یا Accessibility روی گوشی لازم است.");
      return;
    }
    setStatus(msg.error.message || msg.error.code);
    return;
  }
  if (msg.type === "session.hello_ack") {
    if (msg.payload?.pairingId && msg.payload?.sessionToken) {
      pairing = {
        pairingId: msg.payload.pairingId,
        sessionToken: msg.payload.sessionToken
      };
      chrome.storage.local.set({
        pairingId: pairing.pairingId,
        sessionToken: pairing.sessionToken
      });
    }
    afterPaired();
    return;
  }
  if (msg.type === "pairing.accepted") {
    pairing = {
      pairingId: msg.payload?.pairingId || null,
      sessionToken: msg.payload?.sessionToken || null
    };
    chrome.storage.local.set({
      pairingId: pairing.pairingId,
      sessionToken: pairing.sessionToken
    });
    afterPaired();
    return;
  }
  if (msg.type === "device.status" || msg.type === "state.update") {
    const width = msg.payload?.screenWidth;
    const height = msg.payload?.screenHeight;
    input.setScreen(width, height, msg.payload?.rotation || 0);
    if (msg.payload?.mediaProjection === false) {
      setStatus("روی گوشی دکمه Capture را بزنید و دیالوگ سیستم را بپذیرید.");
    }
  }
}

function afterPaired() {
  sendCommand("video.start", { maxFps: 24, maxWidth: 720, bitrateKbps: 2000 });
  sendCommand("device.status", {});
  setStatus("روی تصویر کلیک کنید یا فایل را روی گوشی رها کنید.");
  setSettingsOpen(false);
}

function onBinary(bytes) {
  if (isAudio(bytes)) {
    const packet = decodeAudio(bytes);
    if (!packet) return;
    const pcm = new Int16Array(
      packet.payload.buffer,
      packet.payload.byteOffset,
      Math.floor(packet.payload.byteLength / 2)
    );
    player.play(pcm, packet.sampleRate, packet.channels);
    return;
  }
  const packet = decodeVideo(bytes);
  if (!packet || packet.codec !== JPEG || packet.payload.length < 4) return;
  if (packet.width > 0 && packet.height > 0) {
    input.setScreen(input.screen.width || packet.width, input.screen.height || packet.height);
  }
  const blob = new Blob([packet.payload], { type: "image/jpeg" });
  const url = URL.createObjectURL(blob);
  const image = new Image();
  image.onload = () => {
    frameSize = { width: image.naturalWidth, height: image.naturalHeight };
    if (canvas.width !== image.naturalWidth || canvas.height !== image.naturalHeight) {
      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;
    }
    fitCanvas();
    ctx.drawImage(image, 0, 0);
    URL.revokeObjectURL(url);
    hintEl.classList.add("hidden");
  };
  image.onerror = () => URL.revokeObjectURL(url);
  if (objectUrl) URL.revokeObjectURL(objectUrl);
  objectUrl = url;
  image.src = url;
}

function connectAgent(host, via) {
  const port = chrome.runtime.connect({ name: "phone" });
  agentPort = port;
  agentOpen = false;
  let sent = false;
  const sendConnect = () => {
    if (sent || agentPort !== port) return;
    sent = true;
    port.postMessage({ type: "connect", host });
  };
  port.onMessage.addListener((message) => {
    if (agentPort !== port) return;
    if (message?.type === "ready") {
      sendConnect();
      return;
    }
    if (message?.type === "open") {
      agentOpen = true;
      setStatus(via === "wifi" ? `وای‌فای وصل شد · ${host}` : `USB وصل شد · ${host}`);
      sendCommand("session.hello", {
        pairingId: pairing.pairingId,
        sessionToken: pairing.sessionToken
      });
      pingTimer = window.setInterval(() => sendCommand("session.ping", { t: Date.now() }), 5000);
      statusTimer = window.setInterval(() => sendCommand("device.status", {}), 4000);
      return;
    }
    if (message?.type === "text") {
      try {
        onJson(JSON.parse(message.data));
      } catch {
        // Ignore a truncated control frame.
      }
      return;
    }
    if (message?.type === "binary") {
      onBinary(toBytes(message.data));
      return;
    }
    if (message?.type === "close" || message?.type === "error") {
      setStatus(message.type === "error" ? "خطای اتصال." : "قطع شد. دوباره USB یا Wi-Fi را بزنید.", true);
      closeSocket(false);
    }
  });
  port.onDisconnect.addListener(() => {
    if (agentPort !== port) return;
    setStatus("قطع شد. دوباره USB یا Wi-Fi را بزنید.", true);
    closeSocket(false);
  });
  setTimeout(sendConnect, 200);
}

function toBytes(data) {
  if (data instanceof ArrayBuffer) return new Uint8Array(data);
  if (ArrayBuffer.isView(data)) {
    return new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
  }
  if (Array.isArray(data)) return new Uint8Array(data);
  if (data && typeof data === "object") {
    const values = Object.values(data);
    if (values.length && values.every((item) => typeof item === "number")) {
      return new Uint8Array(values);
    }
  }
  return new Uint8Array();
}

function sendCommand(type, payload) {
  if (!agentPort || !agentOpen) return;
  agentPort.postMessage({ type: "send-text", data: JSON.stringify(envelope(type, payload)) });
}

function closeSocket(close = true) {
  window.clearInterval(pingTimer);
  window.clearInterval(statusTimer);
  pingTimer = 0;
  statusTimer = 0;
  agentOpen = false;
  const port = agentPort;
  agentPort = null;
  if (close && port) {
    try {
      port.postMessage({ type: "disconnect" });
    } catch {
      // Already closed.
    }
    try {
      port.disconnect();
    } catch {
      // Already closed.
    }
  }
}

function setStatus(text, openSettings = false) {
  statusEl.textContent = text;
  statusEl.title = text;
  if (openSettings) setSettingsOpen(true);
}

function setSettingsOpen(open) {
  settingsEl.classList.toggle("open", open);
  settingsToggle.setAttribute("aria-expanded", open ? "true" : "false");
  requestAnimationFrame(fitCanvas);
}

function fitCanvas() {
  const srcW = frameSize.width || canvas.width;
  const srcH = frameSize.height || canvas.height;
  const maxW = stage.clientWidth;
  const maxH = stage.clientHeight;
  if (srcW <= 0 || srcH <= 0 || maxW <= 0 || maxH <= 0) return;
  const scale = Math.min(maxW / srcW, maxH / srcH);
  canvas.style.width = `${Math.max(1, Math.floor(srcW * scale))}px`;
  canvas.style.height = `${Math.max(1, Math.floor(srcH * scale))}px`;
}

window.addEventListener("resize", fitCanvas);
if (typeof ResizeObserver === "function") {
  new ResizeObserver(fitCanvas).observe(stage);
}
window.addEventListener("beforeunload", () => closeSocket());
