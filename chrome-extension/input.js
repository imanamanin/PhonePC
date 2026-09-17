export class PointerInput {
  constructor(send) {
    this.send = send;
    this.screen = { width: 1080, height: 2400, rotation: 0 };
    this.down = null;
    this.dragging = false;
    this.lastRight = 0;
    this.rightTimer = 0;
  }

  setScreen(width, height, rotation = 0) {
    if (width > 0 && height > 0) {
      this.screen = { width, height, rotation };
    }
  }

  map(event, canvas) {
    const rect = canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return null;
    const nx = (event.clientX - rect.left) / rect.width;
    const ny = (event.clientY - rect.top) / rect.height;
    if (nx < 0 || ny < 0 || nx > 1 || ny > 1) return null;
    return {
      x: nx * this.screen.width,
      y: ny * this.screen.height
    };
  }

  onPointerDown(event, canvas) {
    if (event.button === 2) return;
    this.down = this.map(event, canvas);
    this.dragging = false;
  }

  onPointerMove(event, canvas) {
    if (!this.down) return;
    const point = this.map(event, canvas);
    if (point && distance(this.down, point) > 14) this.dragging = true;
  }

  onPointerUp(event, canvas) {
    if (event.button === 2) {
      this.turnPage();
      return;
    }
    const up = this.map(event, canvas) || this.down;
    const start = this.down || up;
    this.down = null;
    if (!start || !up) return;
    if (this.dragging || distance(start, up) > 14) {
      this.send("input.touch", {
        action: "swipe",
        x: start.x,
        y: start.y,
        x2: up.x,
        y2: up.y,
        durationMs: 200
      });
    } else {
      this.send("input.touch", {
        action: "tap",
        x: start.x,
        y: start.y,
        durationMs: 60
      });
    }
    this.dragging = false;
  }

  onWheel(event, canvas) {
    const origin = this.map(event, canvas) || {
      x: this.screen.width / 2,
      y: this.screen.height / 2
    };
    const distanceY = Math.min(
      Math.max((Math.abs(event.deltaY) / 120) * this.screen.height * 0.2, 120),
      this.screen.height * 0.45
    );
    const sign = event.deltaY === 0 ? 1 : Math.sign(event.deltaY);
    this.send("input.touch", {
      action: "scroll",
      x: origin.x,
      y: origin.y,
      x2: origin.x,
      y2: origin.y - sign * distanceY,
      durationMs: 220,
      delta: -event.deltaY
    });
  }

  turnPage() {
    const now = Date.now();
    const doubled = this.lastRight !== 0 && now - this.lastRight <= 400;
    this.lastRight = now;
    window.clearTimeout(this.rightTimer);
    if (doubled) {
      this.pageSwipe(false);
      return;
    }
    this.rightTimer = window.setTimeout(() => this.pageSwipe(true), 280);
  }

  pageSwipe(forward) {
    const y = this.screen.height * 0.5;
    const startX = this.screen.width * (forward ? 0.82 : 0.18);
    const endX = this.screen.width * (forward ? 0.18 : 0.82);
    this.send("input.touch", {
      action: "swipe",
      x: startX,
      y,
      x2: endX,
      y2: y,
      durationMs: 240
    });
  }

  onKeyDown(event) {
    const key = mapKey(event);
    if (!key) return false;
    event.preventDefault();
    this.send("input.key", { action: "click", key });
    return true;
  }

  onText(text) {
    if (!text) return;
    this.send("input.key", { action: "type", key: "type", text });
  }
}

function mapKey(event) {
  if (event.key === "Escape") return "back";
  if (event.key === "Home") return "home";
  if (event.key === "Enter") return "enter";
  if (event.key === "Backspace") return "backspace";
  if (event.key === "Delete") return "delete";
  return null;
}

function distance(a, b) {
  const dx = a.x - b.x;
  const dy = a.y - b.y;
  return Math.sqrt(dx * dx + dy * dy);
}
