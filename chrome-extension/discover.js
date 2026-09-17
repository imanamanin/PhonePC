import { BROWSER_PORT } from "./protocol.js";

const HINTS = ["192.168.42.129", "192.168.137.1", "192.168.43.1"];

export function normalizeHost(value) {
  return String(value || "")
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/\/.*$/, "")
    .replace(/:\d+$/, "");
}

export async function findAgent(manualHost) {
  const hosts = [];
  const typed = normalizeHost(manualHost);
  if (typed) hosts.push(typed);
  hosts.push(...HINTS);
  try {
    const local = await localIpv4();
    for (const ip of local) {
      hosts.push(ip);
      const parts = ip.split(".");
      if (parts.length === 4) {
        const prefix = `${parts[0]}.${parts[1]}.${parts[2]}`;
        hosts.push(`${prefix}.1`, `${prefix}.129`, `${prefix}.225`);
      }
    }
  } catch {
    // WebRTC local IP listing is best-effort.
  }
  const unique = [...new Set(hosts.filter(Boolean))];
  const hits = await Promise.all(unique.map(probe));
  return hits.filter(Boolean);
}

async function probe(host) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), 600);
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

async function localIpv4() {
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
    setTimeout(done, 900);
  });
  pc.close();
  return [...ips];
}
