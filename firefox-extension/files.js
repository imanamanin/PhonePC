const CHUNK = 24 * 1024;
const MAX_BYTES = 512 * 1024 * 1024;

export function bindFileDrop({ root, tray, send, isConnected, setStatus }) {
  const incoming = new Map();
  const queue = [];
  let sending = false;

  const onDrag = (event) => {
    if (!hasFiles(event)) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
    root.classList.add("dropping");
  };
  root.addEventListener("dragenter", onDrag);
  root.addEventListener("dragover", onDrag);
  root.addEventListener("dragleave", (event) => {
    if (!root.contains(event.relatedTarget)) root.classList.remove("dropping");
  });
  root.addEventListener("drop", (event) => {
    if (!hasFiles(event)) return;
    event.preventDefault();
    root.classList.remove("dropping");
    const files = [...(event.dataTransfer.files || [])];
    if (!files.length) return;
    if (!isConnected()) {
      setStatus("اول به گوشی وصل شوید، بعد فایل را روی تصویر رها کنید.", true);
      return;
    }
    queue.push(...files);
    pump();
  });

  async function pump() {
    if (sending) return;
    sending = true;
    try {
      while (queue.length) {
        await sendOne(queue.shift());
      }
    } catch (error) {
      setStatus(error.message || "ارسال فایل انجام نشد.", true);
    } finally {
      sending = false;
    }
  }

  async function sendOne(file) {
    if (file.size > MAX_BYTES) {
      setStatus(`${file.name} خیلی بزرگ است.`, true);
      return;
    }
    const transferId = crypto.randomUUID();
    send("file.offer", {
      transferId,
      name: file.name,
      size: file.size,
      mime: file.type || "application/octet-stream",
      direction: "to_phone"
    });
    setStatus(`ارسال ${file.name} به گوشی…`);
    let sent = 0;
    while (sent < file.size) {
      const end = Math.min(file.size, sent + CHUNK);
      const bytes = new Uint8Array(await file.slice(sent, end).arrayBuffer());
      send("file.chunk", { transferId, offset: sent, data: toBase64(bytes) });
      sent = end;
      const total = file.size || 1;
      setStatus(`ارسال ${file.name} · ${Math.floor((sent * 100) / total)}٪`);
      await wait();
    }
    send("file.complete", { transferId, name: file.name, bytesSent: sent });
  }

  function onMessage(msg) {
    if (!msg?.type?.startsWith("file.")) return false;
    const payload = msg.payload || {};
    if (msg.type === "file.offer" && payload.direction === "to_windows") {
      incoming.set(payload.transferId, {
        name: payload.name || "file",
        mime: payload.mime || "application/octet-stream",
        size: payload.size || 0,
        parts: []
      });
      setStatus(`دریافت ${payload.name || "فایل"} از گوشی…`);
      return true;
    }
    if (msg.type === "file.chunk") {
      const session = incoming.get(payload.transferId);
      if (session && payload.data) session.parts.push(fromBase64(payload.data));
      return true;
    }
    if (msg.type === "file.complete") {
      const session = incoming.get(payload.transferId);
      if (session) {
        incoming.delete(payload.transferId);
        saveFile(session, tray);
        setStatus(`${session.name} در Downloads ذخیره شد. می‌توانید آن را به دسکتاپ بکشید.`);
      } else if (payload.name) {
        setStatus(`${payload.name} روی گوشی ذخیره شد.`);
      }
      return true;
    }
    if (msg.type === "file.progress" && payload.bytesTotal) {
      setStatus(`انتقال فایل ${Math.floor((payload.bytesSent * 100) / payload.bytesTotal)}٪`);
      return true;
    }
    if (msg.type === "file.error" || msg.error) {
      setStatus(msg.error?.message || "انتقال فایل انجام نشد.", true);
      return true;
    }
    return true;
  }

  return { onMessage };
}

function hasFiles(event) {
  const types = event.dataTransfer?.types;
  if (!types) return false;
  return [...types].includes("Files");
}

function saveFile(session, tray) {
  const blob = new Blob(session.parts, { type: session.mime });
  const file = new File([blob], session.name, { type: session.mime });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = session.name;
  link.rel = "noopener";
  document.body.appendChild(link);
  link.click();
  link.remove();
  if (!tray) return;
  tray.hidden = false;
  const chip = document.createElement("button");
  chip.type = "button";
  chip.className = "file-chip";
  chip.textContent = session.name;
  chip.draggable = true;
  chip.title = "برای ذخیره دوباره کلیک کنید یا به دسکتاپ بکشید";
  chip.addEventListener("click", () => {
    const again = document.createElement("a");
    again.href = url;
    again.download = file.name;
    again.click();
  });
  chip.addEventListener("dragstart", (event) => {
    event.dataTransfer.effectAllowed = "copyMove";
    try {
      event.dataTransfer.items.add(file);
    } catch {
      // Some browsers only accept DownloadURL.
    }
    try {
      event.dataTransfer.setData("DownloadURL", `${file.type || "application/octet-stream"}:${file.name}:${url}`);
    } catch {
      // Optional desktop drop hint.
    }
  });
  tray.appendChild(chip);
  while (tray.children.length > 6) {
    tray.removeChild(tray.firstChild);
  }
}

function toBase64(bytes) {
  let text = "";
  for (let i = 0; i < bytes.length; i += 1) {
    text += String.fromCharCode(bytes[i]);
  }
  return btoa(text);
}

function fromBase64(text) {
  const binary = atob(String(text || ""));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

function wait() {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
