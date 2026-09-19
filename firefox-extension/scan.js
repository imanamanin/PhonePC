globalThis.Scan = {
  PORT: 17893,
  USB_HINTS: ["192.168.42.129", "192.168.137.1", "192.168.43.1"],
  USB_PREFIXES: ["192.168.42", "192.168.137", "192.168.43"],
  MDNS_HINTS: ["pcphone.local", "pc-phone.local"],

  normalizeHost(value) {
    return String(value || "")
      .trim()
      .replace(/^https?:\/\//i, "")
      .replace(/\/.*$/, "")
      .replace(/:\d+$/, "");
  },

  looksUsb(iface) {
    const name = `${iface?.name || ""}`.toLowerCase();
    const ip = iface?.address || "";
    if (ip.startsWith("192.168.42.") || ip.startsWith("192.168.137.")) return true;
    if (/rndis|remote ndis|usb|android/.test(name)) return true;
    return ip.startsWith("10.") && !ip.startsWith("10.0.0.") && !ip.startsWith("10.0.1.");
  },

  isPrivateIpv4(ip) {
    const value = String(ip || "");
    if (!/^\d+\.\d+\.\d+\.\d+$/.test(value)) return false;
    if (value === "0.0.0.0" || value.startsWith("127.") || value.startsWith("169.254.")) return false;
    if (value.startsWith("10.") || value.startsWith("192.168.")) return true;
    return /^172\.(1[6-9]|2\d|3[0-1])\./.test(value);
  },

  isWifiIface(iface) {
    const name = `${iface?.name || ""}`.toLowerCase();
    const ip = iface?.address || "";
    if (/wi-?fi|wlan|wireless|802\.11/.test(name)) return true;
    if (this.looksUsb(iface)) return false;
    if (ip.startsWith("192.168.42.") || ip.startsWith("192.168.137.")) return false;
    return ip.startsWith("192.168.") || ip.startsWith("10.0.0.") || ip.startsWith("10.0.1.");
  },

  isWsl(ip) {
    return /^172\.(1[6-9]|2\d|3[0-1])\./.test(ip || "");
  },

  buildCandidates(manualHost, mode, local) {
    const hosts = [];
    const add = (host) => {
      const value = this.normalizeHost(host);
      if (!value || hosts.includes(value)) return;
      if (value.endsWith(".local") || this.isPrivateIpv4(value)) hosts.push(value);
    };
    const addHot = (ip) => {
      const parts = String(ip || "").split(".");
      if (parts.length !== 4) return;
      const prefix = `${parts[0]}.${parts[1]}.${parts[2]}`;
      add(`${prefix}.225`);
      add(`${prefix}.1`);
      add(`${prefix}.129`);
    };
    const addSubnet = (ip, skipSelf) => {
      const parts = String(ip || "").split(".");
      if (parts.length !== 4) return;
      const prefix = `${parts[0]}.${parts[1]}.${parts[2]}`;
      for (let i = 1; i <= 254; i += 1) {
        const host = `${prefix}.${i}`;
        if (host === skipSelf) continue;
        add(host);
      }
    };

    const typed = this.normalizeHost(manualHost);
    if (typed && !(mode === "wifi" && this.isUsbHost(typed))) {
      add(typed);
    }
    const ifaces = Array.isArray(local) ? local : [];

    if (mode === "usb") {
      this.USB_HINTS.forEach(add);
      const usb = ifaces.filter((item) => this.looksUsb(item));
      const usable = usb.length ? usb : ifaces.filter((item) => !this.isWsl(item.address));
      const scan = usable.length ? usable : ifaces;
      for (const iface of scan) addHot(iface.address);
      for (const prefix of this.USB_PREFIXES) addHot(`${prefix}.1`);
      for (const iface of scan) addSubnet(iface.address, iface.address);
      return hosts;
    }

    this.MDNS_HINTS.forEach(add);
    const wifi = ifaces.filter((item) => this.isWifiIface(item));
    for (const iface of wifi) {
      add(iface.address);
      addHot(iface.address);
      if (iface.gateway) add(iface.gateway);
    }
    for (const iface of wifi) addSubnet(iface.address, iface.address);
    return hosts;
  },

  isUsbHost(host) {
    return /^(192\.168\.42\.|192\.168\.137\.)/.test(host || "") ||
      (/^10\./.test(host || "") && !/^10\.0\.[01]\./.test(host || ""));
  },

  lastError: "",

  async collectLocal(existing, names = []) {
    const byIp = new Map();
    const pending = new Set(names || []);
    const addIface = (name, address, gateway) => {
      if (!this.isPrivateIpv4(address)) return;
      const prev = byIp.get(address);
      if (prev && prev.name && !name) return;
      byIp.set(address, { name: name || prev?.name || "", address, gateway: gateway || prev?.gateway || "" });
    };
    const addIp = (value) => addIface("", value, "");
    const addName = (value) => {
      const name = String(value || "");
      if (name.endsWith(".local")) pending.add(name.replace(/\.$/, ""));
    };
    for (const item of existing || []) addIface(item?.name, item?.address, item?.gateway);
    this.MDNS_HINTS.forEach((name) => pending.add(name));
    for (const item of await this.nativeIfaces()) {
      addIface(item.name, item.address, item.gateway);
    }
    if (byIp.size === 0) {
      await this.harvestIce(addIp, addName);
    }
    for (const name of pending) {
      await this.resolveName(name, addIp);
    }
    return [...byIp.values()];
  },

  harvestIce(addIp, addName) {
    return new Promise((resolve) => {
      let pc;
      let settled = false;
      const finish = async () => {
        if (settled) return;
        settled = true;
        try {
          const stats = await pc.getStats();
          stats.forEach((report) => {
            addIp(report.address);
            addIp(report.ip);
            addIp(report.ipAddress);
            addIp(report.localAddress);
            addIp(report.relatedAddress);
            addName(report.address);
            addName(report.ipAddress);
          });
        } catch {
          // Stats are optional.
        }
        try {
          pc.close();
        } catch {
          // Already closed.
        }
        resolve();
      };
      try {
        pc = new RTCPeerConnection({
          iceServers: [
            { urls: "stun:stun.cloudflare.com:3478" },
            { urls: "stun:stun.l.google.com:19302" }
          ],
          iceTransportPolicy: "all"
        });
      } catch {
        resolve();
        return;
      }
      try {
        pc.createDataChannel("x");
      } catch {
        // Offer still gathers host candidates.
      }
      pc.onicecandidate = (event) => {
        if (!event.candidate) {
          finish();
          return;
        }
        addIp(event.candidate.address);
        addIp(event.candidate.relatedAddress);
        addName(event.candidate.address);
        const line = event.candidate.candidate || "";
        const raddr = line.match(/raddr (\d+\.\d+\.\d+\.\d+)/);
        if (raddr) addIp(raddr[1]);
        const host = line.match(/ (\d+\.\d+\.\d+\.\d+) \d+ typ /);
        if (host) addIp(host[1]);
        const mdns = line.match(/ ([A-Za-z0-9._-]+\.local)/);
        if (mdns) addName(mdns[1]);
      };
      pc.createOffer()
        .then((offer) => pc.setLocalDescription(offer))
        .catch(() => finish());
      setTimeout(finish, 2800);
    });
  },

  async nativeIfaces() {
    try {
      const runtime = globalThis.browser?.runtime || globalThis.chrome?.runtime;
      if (!runtime?.sendNativeMessage) return [];
      const reply = await runtime.sendNativeMessage("pcphone.ifaces", { list: true });
      if (Array.isArray(reply?.ifaces) && reply.ifaces.length) {
        return reply.ifaces.map((item) => ({
          address: item.address || "",
          name: item.name || "",
          gateway: item.gateway || ""
        }));
      }
      return (reply?.ips || []).map((address) => ({ address, name: "", gateway: "" }));
    } catch (error) {
      this.lastError = `native: ${error.message || error}`;
      return [];
    }
  },

  async resolveName(hostname, addIp) {
    try {
      const dns = globalThis.browser?.dns || globalThis.chrome?.dns;
      if (!dns?.resolve) return;
      const rec = await dns.resolve(hostname, ["bypass_cache", "disable_ipv6"]);
      for (const address of rec?.addresses || []) addIp(address);
    } catch {
      // mDNS resolve is best-effort.
    }
  },

  async probe(host, allowWs = false) {
    const http = await this.probeHttp(host);
    if (http) return http;
    if (!allowWs) return null;
    return this.probeWs(host);
  },

  async probeHttp(host) {
    const xhrHit = await this.probeXhr(host);
    if (xhrHit) return xhrHit;
    return this.probeFetch(host);
  },

  probeXhr(host) {
    return new Promise((resolve) => {
      const xhr = new XMLHttpRequest();
      xhr.timeout = 900;
      xhr.onload = () => {
        try {
          const json = JSON.parse(xhr.responseText);
          resolve(json?.ok ? { host, ...json } : null);
        } catch (error) {
          this.lastError = `${host}: ${error.message || error}`;
          resolve(null);
        }
      };
      xhr.onerror = () => {
        this.lastError = `${host}: network`;
        resolve(null);
      };
      xhr.ontimeout = () => {
        this.lastError = `${host}: timeout`;
        resolve(null);
      };
      try {
        xhr.open("GET", `http://${host}:${this.PORT}/health`);
        xhr.send();
      } catch (error) {
        this.lastError = `${host}: ${error.message || error}`;
        resolve(null);
      }
    });
  },

  async probeFetch(host) {
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), 900);
    try {
      const res = await fetch(`http://${host}:${this.PORT}/health`, {
        cache: "no-store",
        signal: ctrl.signal
      });
      if (!res.ok) return null;
      const json = await res.json();
      return json?.ok ? { host, ...json } : null;
    } catch (error) {
      this.lastError = `${host}: ${error.message || error}`;
      return null;
    } finally {
      clearTimeout(timer);
    }
  },

  probeWs(host) {
    return new Promise((resolve) => {
      let settled = false;
      let socket;
      const done = (hit) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        try {
          socket?.close();
        } catch {
          // Already closed.
        }
        resolve(hit);
      };
      const timer = setTimeout(() => done(null), 700);
      try {
        socket = new WebSocket(`ws://${host}:${this.PORT}/ws`);
      } catch {
        done(null);
        return;
      }
      socket.addEventListener("open", () => done({ host, ok: true }));
      socket.addEventListener("error", () => done(null));
      socket.addEventListener("close", () => done(null));
    });
  },

  preferModeHost(found, mode) {
    if (!found) return null;
    if (mode !== "wifi") return found;
    const wifi = this.normalizeHost(found.wifi);
    if (wifi && this.isPrivateIpv4(wifi) && !this.isUsbHost(wifi)) {
      return { ...found, host: wifi };
    }
    if (this.isUsbHost(found.host)) return null;
    return found;
  },

  async scan(hosts, onProgress, isCancelled, mode) {
    let next = 0;
    let hit = null;
    const total = hosts.length;
    const workers = Array.from({ length: Math.min(18, Math.max(1, hosts.length)) }, async () => {
      while (!hit && !isCancelled() && next < hosts.length) {
        const index = next;
        const host = hosts[next];
        next += 1;
        if (index === 0 || index % 6 === 0) {
          onProgress({ host, scanned: index + 1, total });
        }
        const found = await this.probe(host, index < 16);
        const chosen = this.preferModeHost(found, mode);
        if (chosen) hit = chosen;
      }
    });
    await Promise.all(workers);
    return hit;
  }
};
