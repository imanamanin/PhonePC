import { BROWSER_PORT } from "./protocol.js";

const USB_HINTS = ["192.168.42.129", "192.168.137.1", "192.168.43.1"];
const WIFI_PREFIXES = [
  "192.168.1",
  "192.168.0",
  "192.168.2",
  "192.168.3",
  "192.168.4",
  "192.168.8",
  "192.168.10",
  "192.168.11",
  "192.168.31",
  "192.168.50",
  "192.168.68",
  "192.168.86",
  "192.168.100",
  "10.0.0",
  "10.0.1"
];

export function normalizeHost(value) {
  return String(value || "")
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/\/.*$/, "")
    .replace(/:\d+$/, "");
}

export function isUsbHost(host) {
  return /^(192\.168\.42\.|192\.168\.137\.)/.test(host);
}

export async function findAgent(manualHost, options = {}) {
  const mode = options.mode === "wifi" ? "wifi" : "usb";
  const onProgress = options.onProgress;
  const hosts = await candidateHosts(manualHost, mode);
  return firstHit(hosts, onProgress);
}

async function candidateHosts(manualHost, mode) {
  const hosts = [];
  const add = (host) => {
    const value = normalizeHost(host);
    if (!value || hosts.includes(value)) return;
    if (value.startsWith("127.") || value.startsWith("169.254.")) return;
    hosts.push(value);
  };
  const typed = normalizeHost(manualHost);
  if (typed && !(mode === "wifi" && isUsbHost(typed))) {
    add(typed);
  }
  const local = await localIfaces();
  if (mode === "usb") {
    USB_HINTS.forEach(add);
    for (const iface of local.filter(isUsbIface)) {
      addSubnet(add, iface.address, { skipSelf: iface.address });
    }
    return hosts;
  }
  const wifiIfaces = local.filter(isWifiIface);
  for (const iface of wifiIfaces) {
    add(iface.address);
    addSubnet(add, iface.address, { skipSelf: iface.address });
  }
  if (wifiIfaces.length === 0) {
    const seen = new Set(hosts.map((h) => h.split(".").slice(0, 3).join(".")));
    for (const prefix of WIFI_PREFIXES) {
      if (seen.has(prefix)) continue;
      for (let i = 1; i <= 254; i += 1) add(`${prefix}.${i}`);
    }
  }
  return hosts;
}

function addSubnet(add, ip, { skipSelf } = {}) {
  const parts = String(ip || "").split(".");
  if (parts.length !== 4) return;
  const prefix = `${parts[0]}.${parts[1]}.${parts[2]}`;
  add(`${prefix}.1`);
  add(`${prefix}.129`);
  add(`${prefix}.225`);
  for (let i = 1; i <= 254; i += 1) {
    const host = `${prefix}.${i}`;
    if (host === skipSelf) continue;
    add(host);
  }
}

function isUsbIface(iface) {
  const name = `${iface.name || ""}`.toLowerCase();
  const ip = iface.address || "";
  if (ip.startsWith("192.168.42.") || ip.startsWith("192.168.137.")) return true;
  return /rndis|remote ndis|usb|android/.test(name);
}

function isWifiIface(iface) {
  const name = `${iface.name || ""}`.toLowerCase();
  const ip = iface.address || "";
  if (/wi-?fi|wlan|wireless|802\.11/.test(name)) return true;
  if (isUsbIface(iface)) return false;
  if (ip.startsWith("192.168.42.") || ip.startsWith("192.168.137.")) return false;
  return ip.startsWith("192.168.");
}

async function firstHit(hosts, onProgress) {
  const found = [];
  let next = 0;
  let stop = false;
  const total = hosts.length;
  const workers = Array.from({ length: Math.min(40, Math.max(1, hosts.length)) }, async () => {
    while (!stop && next < hosts.length) {
      const index = next;
      const host = hosts[next];
      next += 1;
      if (onProgress && index % 12 === 0) {
        onProgress({ host, scanned: index + 1, total });
      }
      const hit = await probe(host);
      if (hit) {
        found.push(hit);
        stop = true;
      }
    }
  });
  await Promise.all(workers);
  return found;
}

async function probe(host) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), 700);
  try {
    const res = await fetch(`http://${host}:${BROWSER_PORT}/health`, { signal: ctrl.signal });
    if (!res.ok) return null;
    const json = await res.json();
    if (json?.ok) return { host, ...json };
    return null;
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

async function localIfaces() {
  const list = [];
  try {
    const raw = await networkInterfaces();
    for (const item of raw) {
      const address = item?.address;
      if (address && /^\d+\.\d+\.\d+\.\d+$/.test(address) && !address.startsWith("127.")) {
        list.push({ name: item.name || "", address });
      }
    }
  } catch {
    // Fall through.
  }
  if (list.length === 0) {
    for (const ip of await webrtcIpv4()) {
      list.push({ name: "", address: ip });
    }
  }
  return list;
}

async function networkInterfaces() {
  const api = globalThis.chrome?.system?.network?.getNetworkInterfaces;
  if (!api) return [];
  try {
    const promised = api();
    if (promised && typeof promised.then === "function") {
      return (await promised) || [];
    }
  } catch {
    // Callback style below.
  }
  return await new Promise((resolve) => {
    try {
      api((items) => resolve(items || []));
    } catch {
      resolve([]);
    }
  });
}

async function webrtcIpv4() {
  const ips = new Set();
  const pc = new RTCPeerConnection({ iceServers: [] });
  pc.createDataChannel("x");
  await new Promise((resolve) => {
    const done = () => {
      pc.onicecandidate = null;
      resolve();
    };
    pc.onicecandidate = (event) => {
      if (!event.candidate) {
        done();
        return;
      }
      const match = event.candidate.candidate.match(/(\d+\.\d+\.\d+\.\d+)/);
      if (match && !match[1].startsWith("127.")) {
        ips.add(match[1]);
      }
    };
    pc.createOffer().then((offer) => pc.setLocalDescription(offer)).catch(done);
    setTimeout(done, 800);
  });
  pc.close();
  return [...ips];
}
