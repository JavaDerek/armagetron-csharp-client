// Thin browser renderer for the server-rendered Armagetron web head. It opens a WebSocket to
// /play, receives one JSON Scene frame per tick (the SAME geometry every other client draws —
// built by SceneBuilder in Core.Protocol, server-side), paints it to the canvas, and sends turns
// back. No game logic lives here: it is purely a display + input surface.

const canvas = document.getElementById("screen");
const ctx = canvas.getContext("2d");
const statusEl = document.getElementById("status");

const q = new URLSearchParams(location.search);
const host = q.get("host") || "192.168.68.61";
const port = q.get("port") || "4534";
const name = q.get("name") || "Vlad";

const wsUrl = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}` +
              `/play?host=${encodeURIComponent(host)}&port=${port}&name=${encodeURIComponent(name)}`;

let ws;
let latest = null;

function colour(packed) {
  const n = packed | 0;
  return `rgb(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255})`;
}

function draw(frame) {
  // Match the canvas to the server's view size if it ever changes.
  if (frame.size && canvas.width !== frame.size) { canvas.width = canvas.height = frame.size; }

  ctx.fillStyle = "#000";
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // Light-wall segments + arena border.
  for (const l of frame.lines) {
    ctx.strokeStyle = colour(l[4]);
    ctx.lineWidth = l[5] || 2;
    ctx.beginPath();
    ctx.moveTo(l[0], l[1]);
    ctx.lineTo(l[2], l[3]);
    ctx.stroke();
  }
  // Cycle head markers.
  for (const r of frame.rects) {
    ctx.fillStyle = colour(r[4]);
    ctx.fillRect(r[0], r[1], r[2], r[3]);
  }

  statusEl.textContent = `${frame.status}${frame.myId >= 0 ? "  ·  cycle " + frame.myId : ""}`;
}

function loop() {
  if (latest) { draw(latest); latest = null; }
  requestAnimationFrame(loop);
}

function connect() {
  statusEl.textContent = "connecting…";
  ws = new WebSocket(wsUrl);
  ws.onmessage = (e) => { try { latest = JSON.parse(e.data); } catch { /* ignore */ } };
  ws.onclose = () => { statusEl.textContent = "disconnected — retrying…"; setTimeout(connect, 1500); };
  ws.onerror = () => ws.close();
}

function turn(dir) { if (ws && ws.readyState === WebSocket.OPEN) ws.send(dir); }

// Keyboard (desktop) and tap-to-turn (touch): left half = left, right half = right.
addEventListener("keydown", (e) => {
  if (e.key === "ArrowLeft") turn("L");
  else if (e.key === "ArrowRight") turn("R");
});
canvas.addEventListener("pointerdown", (e) => {
  const rect = canvas.getBoundingClientRect();
  turn(e.clientX - rect.left < rect.width / 2 ? "L" : "R");
});

connect();
loop();
