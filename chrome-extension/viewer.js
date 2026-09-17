import { PcmPlayer } from "./audio.js";
import { findAgent, normalizeHost } from "./discover.js";
import { PointerInput } from "./input.js";
import {
  BROWSER_PORT,
  decodeAudio,
  decodeVideo,
  envelope,
  isAudio,
  JPEG
} from "./protocol.js";

const hostInput = document.getElementById("host");
const pinInput = document.getElementById("pin");
const statusEl = document.getElementById("status");
const hintEl = document.getElementById("hint");
const canvas = document.getElementById("screen");
const ctx = canvas.getContext("2d");
const stage = document.getElementById("stage");
const settingsEl = document.getElementById("settings");
const settingsToggle = document.getElementById("settings-toggle");
const popoutBtn = document.getElementById("popout");
const detached = new URLSearchParams(location.search).get("mode") === "window";
const player = new PcmPlayer();
const input = new PointerInput(sendCommand);
let socket = null;
let objectUrl = null;
let pingTimer = 0;
let statusTimer = 0;
let pairing = { pairingId: null, sessionToken: null };
let frameSize = { width: 0, height: 0 };

restore();
if (detached) {
  document.body.classList.add("window");
  popoutBtn.hidden = true;
}

settingsToggle.addEventListener("click", () => setSettingsOpen(!settingsEl.classList.contains("open")));
popoutBtn.addEventListener("click", () => chrome.runtime.sendMessage({ type: "open-window" }));
document.getElementById("find").addEventListener("click", () => discover(true));
document.getElementById("connect").addEventListener("click", () => connect());
document.getElementById("pair").addEventListener("click", () => submitPin());
pinInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") submitPin();
});

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

discover(false);

async function restore() {
  const saved = await chrome.storage.local.get(["host", "pairingId", "sessionToken"]);
  if (saved.host) hostInput.value = saved.host;
  pairing = {
    pairingId: saved.pairingId || null,
    sessionToken: saved.sessionToken || null
  };
}

async function discover(manual) {
  setStatus(manual ? "در حال جستجو روی USB tether…" : "در حال پیدا کردن گوشی…");
  const found = await findAgent(hostInput.value);
  if (!found.length) {
    setStatus("گوشی پیدا نشد. IP را دستی وارد کنید.", true);
    return;
  }
  hostInput.value = found[0].host;
  await chrome.storage.local.set({ host: found[0].host });
  setStatus(`پیدا شد: ${found[0].host}`);
  if (manual) connect();
}

async function connect() {
  const host = normalizeHost(hostInput.value);
  if (!host) {
    setStatus("ابتدا IP گوشی را وارد کنید.", true);
    return;
  }
  await player.unlock();
  closeSocket();
  await chrome.storage.local.set({ host });
  setStatus(`اتصال به ${host}…`);
  const ws = new WebSocket(`ws://${host}:${BROWSER_PORT}/ws`);
  ws.binaryType = "arraybuffer";
  socket = ws;
  ws.addEventListener("open", () => {
    setStatus("متصل شد. در حال pairing…");
    sendCommand("session.hello", {
      pairingId: pairing.pairingId,
      sessionToken: pairing.sessionToken
    });
    pingTimer = window.setInterval(() => sendCommand("session.ping", { t: Date.now() }), 5000);
    statusTimer = window.setInterval(() => sendCommand("device.status", {}), 4000);
  });
  ws.addEventListener("message", (event) => {
    if (typeof event.data === "string") {
      onJson(JSON.parse(event.data));
      return;
    }
    onBinary(new Uint8Array(event.data));
  });
  ws.addEventListener("close", () => {
    setStatus("قطع شد. دوباره اتصال بزنید.", true);
    closeSocket(false);
  });
  ws.addEventListener("error", () => setStatus("خطای اتصال. tether و اپ گوشی را چک کنید.", true));
}

function onJson(msg) {
  if (msg.error) {
    if (msg.error.code === "UNPAIRED") {
      setStatus("PIN روی گوشی را اینجا وارد کنید.", true);
      pinInput.focus();
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
    const required = msg.payload?.pairingRequired;
    if (required) {
      setStatus("PIN شش‌رقمی گوشی را وارد کنید.", true);
      pinInput.focus();
      return;
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
    const sas = msg.payload?.sas;
    setStatus(sas ? `جفت شد. SAS: ${sas}` : "جفت شد.");
    afterPaired();
    return;
  }
  if (msg.type === "pairing.rejected" || msg.type === "pairing.expired") {
    setStatus("PIN رد شد. دوباره از روی گوشی بخوانید.", true);
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
  setStatus("روی تصویر کلیک کنید.");
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

function sendCommand(type, payload) {
  if (!socket || socket.readyState !== WebSocket.OPEN) return;
  socket.send(JSON.stringify(envelope(type, payload)));
}

function submitPin() {
  const pin = pinInput.value.trim();
  if (!/^\d{4,8}$/.test(pin)) {
    setStatus("PIN را کامل وارد کنید.", true);
    return;
  }
  sendCommand("pairing.submit", { pin });
  pinInput.value = "";
}

function closeSocket(close = true) {
  window.clearInterval(pingTimer);
  window.clearInterval(statusTimer);
  pingTimer = 0;
  statusTimer = 0;
  if (close && socket) {
    try {
      socket.close();
    } catch {
      // Already closed.
    }
  }
  socket = null;
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
