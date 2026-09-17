export class PcmPlayer {
  constructor() {
    this.ctx = null;
    this.next = 0;
  }

  async unlock() {
    if (!this.ctx) {
      this.ctx = new AudioContext();
    }
    if (this.ctx.state === "suspended") {
      await this.ctx.resume();
    }
  }

  play(int16, sampleRate, channels) {
    if (!this.ctx || !int16?.length) return;
    const ch = Math.max(1, channels || 1);
    const frames = Math.floor(int16.length / ch);
    if (frames <= 0) return;
    const rate = sampleRate || this.ctx.sampleRate;
    const buffer = this.ctx.createBuffer(ch, frames, rate);
    for (let c = 0; c < ch; c += 1) {
      const data = buffer.getChannelData(c);
      for (let i = 0; i < frames; i += 1) {
        data[i] = int16[i * ch + c] / 32768;
      }
    }
    const src = this.ctx.createBufferSource();
    src.buffer = buffer;
    src.connect(this.ctx.destination);
    const now = this.ctx.currentTime;
    if (this.next < now) this.next = now;
    src.start(this.next);
    this.next += buffer.duration;
  }
}
