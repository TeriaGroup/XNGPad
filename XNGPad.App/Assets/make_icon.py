import math
import struct
from pathlib import Path

ACCENT = (107, 138, 253, 255)
FILL = (22, 23, 26, 255)


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def dist_seg(px, py, x1, y1, x2, y2):
    vx, vy = x2 - x1, y2 - y1
    l2 = vx * vx + vy * vy
    if l2 == 0:
        return math.hypot(px - x1, py - y1)
    t = clamp(((px - x1) * vx + (py - y1) * vy) / l2)
    return math.hypot(px - (x1 + t * vx), py - (y1 + t * vy))


def roundrect_sdf(px, py, size, pad, radius):
    cx = (size - 1) * 0.5
    cy = (size - 1) * 0.5
    hw = (size - 1) * 0.5 - pad
    hh = hw
    dx = abs(px - cx) - (hw - radius)
    dy = abs(py - cy) - (hh - radius)
    ox = max(dx, 0.0)
    oy = max(dy, 0.0)
    return math.hypot(ox, oy) + min(max(dx, dy), 0.0) - radius


def pixel(size, px, py):
    pad = max(1.0, size * 0.08)
    radius = max(2.0, size * 0.22)
    border = max(1.2, size / 14.0)
    sdf = roundrect_sdf(px, py, size, pad, radius)
    m = size * 0.30
    xw = max(1.6, size / 7.5)
    d = min(
        dist_seg(px, py, m, m, size - 1 - m, size - 1 - m),
        dist_seg(px, py, size - 1 - m, m, m, size - 1 - m),
    )
    x_cov = clamp((xw - d) + 0.5)
    fill_cov = clamp(0.5 - sdf)
    border_cov = clamp(0.5 - abs(sdf) + border * 0.5) * fill_cov
    a = max(fill_cov, x_cov)
    if a <= 0:
        return (0, 0, 0, 0)
    r = FILL[0] * fill_cov
    g = FILL[1] * fill_cov
    b = FILL[2] * fill_cov
    r = r * (1 - border_cov) + ACCENT[0] * border_cov
    g = g * (1 - border_cov) + ACCENT[1] * border_cov
    b = b * (1 - border_cov) + ACCENT[2] * border_cov
    r = r * (1 - x_cov) + ACCENT[0] * x_cov
    g = g * (1 - x_cov) + ACCENT[1] * x_cov
    b = b * (1 - x_cov) + ACCENT[2] * x_cov
    alpha = max(fill_cov, border_cov, x_cov)
    return (int(r), int(g), int(b), int(clamp(alpha) * 255))


def render(size):
    samples = 4 if size >= 32 else 2
    step = 1.0 / samples
    rows = []
    for y in range(size):
        row = []
        for x in range(size):
            r = g = b = a = 0.0
            for oy in range(samples):
                for ox in range(samples):
                    pr, pg, pb, pa = pixel(size, x + (ox + 0.5) * step, y + (oy + 0.5) * step)
                    r += pr
                    g += pg
                    b += pb
                    a += pa
            n = samples * samples
            row.append((int(r / n), int(g / n), int(b / n), int(a / n)))
        rows.append(row)
    return rows


def dib(size, rows):
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        size,
        size * 2,
        1,
        32,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    xor = bytearray()
    for y in range(size - 1, -1, -1):
        for x in range(size):
            r, g, b, a = rows[y][x]
            xor += struct.pack("BBBB", b, g, r, a)
    row_and = ((size + 31) // 32) * 4
    mask = bytes(row_and * size)
    return header + xor + mask


def write_ico(path, sizes):
    images = []
    for s in sizes:
        data = dib(s, render(s))
        images.append((s, data))
    offset = 6 + 16 * len(images)
    buf = bytearray()
    buf += struct.pack("<HHH", 0, 1, len(images))
    for s, data in images:
        dim = 0 if s >= 256 else s
        buf += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in images:
        buf += data
    path.write_bytes(buf)


if __name__ == "__main__":
    out = Path(__file__).with_name("app.ico")
    write_ico(out, (16, 32, 48, 256))
    print(out, out.stat().st_size)
