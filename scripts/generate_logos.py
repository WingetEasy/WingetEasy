"""Gera os ícones do WingetEasy a partir da logo original em alta resolução.

Uso: python scripts/generate_logos.py

Saídas (src/WingetEasy.App/Assets):
  - Logo.ico, WingetEasy-Logo.ico        -> quadrados, multi-resolução (16..256)
  - WingetEasy-Logo-update.ico           -> idem, com indicador vermelho de atualização
  - WingetEasy-Logo.png                  -> logo para a UI, recortada (sem margem transparente)
"""
import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Documents" / "3. Design" / "Design compilado" / "assets" / "logo-w.png"
ASSETS = ROOT / "src" / "WingetEasy.App" / "Assets"
ICO_SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
MASTER = 1024  # tamanho da matriz quadrada antes de reduzir


def load_cropped() -> Image.Image:
    im = Image.open(SOURCE).convert("RGBA")
    # Recorta pela área realmente visível (alfa > 8), sem esticar.
    alpha = im.getchannel("A").point(lambda a: 255 if a > 8 else 0)
    return im.crop(alpha.getbbox())


def square_master(w_img: Image.Image, margin: float = 0.04, indicator: bool = False) -> Image.Image:
    """Centraliza o 'W' em um quadrado transparente, preservando a proporção."""
    canvas = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    inner = int(MASTER * (1 - 2 * margin))
    scale = inner / max(w_img.size)
    new_size = (round(w_img.width * scale), round(w_img.height * scale))
    resized = w_img.resize(new_size, Image.LANCZOS)
    x = (MASTER - new_size[0]) // 2
    y = (MASTER - new_size[1]) // 2
    canvas.alpha_composite(resized, (x, y))

    if indicator:
        r = int(MASTER * 0.17)
        cx, cy = MASTER - r - int(MASTER * 0.02), r + int(MASTER * 0.02)
        draw = ImageDraw.Draw(canvas)
        ring = int(MASTER * 0.03)
        draw.ellipse((cx - r - ring, cy - r - ring, cx + r + ring, cy + r + ring), fill=(255, 255, 255, 255))
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(239, 68, 68, 255))
    return canvas


def write_ico(master: Image.Image, path: Path) -> None:
    frames = []
    for s in ICO_SIZES:
        buf = io.BytesIO()
        master.resize((s, s), Image.LANCZOS).save(buf, format="PNG", optimize=True)
        frames.append((s, buf.getvalue()))
    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries, data = b"", b""
    for s, png in frames:
        dim = 0 if s >= 256 else s
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset + len(data))
        data += png
    path.write_bytes(header + entries + data)
    print(f"{path.name}: {len(frames)} camadas {ICO_SIZES}")


def main() -> None:
    w_img = load_cropped()
    # Ícone do app (janela, .exe, atalhos, busca do Windows). Os ícones da bandeja
    # (WingetEasy-Logo.ico / -update.ico) e o PNG da barra de título são mantidos como estavam.
    write_ico(square_master(w_img, margin=0.01), ASSETS / "Logo.ico")

    # PNG da UI: recortado rente ao "W" (2% de folga), 2x para telas com escala alta.
    pad = round(max(w_img.size) * 0.02)
    ui = Image.new("RGBA", (w_img.width + 2 * pad, w_img.height + 2 * pad), (0, 0, 0, 0))
    ui.alpha_composite(w_img, (pad, pad))
    target_w = 600
    ui = ui.resize((target_w, round(ui.height * target_w / ui.width)), Image.LANCZOS)
    ui.save(ASSETS / "WingetEasy-Logo.png", optimize=True)
    print(f"WingetEasy-Logo.png: {ui.size[0]}x{ui.size[1]} (proporção {ui.width / ui.height:.2f})")

    # PNG do banner (UpdatesPage): mesma composição da arte antiga (420x280, "W" com ~10% de margem),
    # porém em 2x (840x560) para ficar nítido. Mantém o alinhamento da sombra do banner.
    hero = Image.new("RGBA", (840, 560), (0, 0, 0, 0))
    hero_w = 674
    hero_h = round(w_img.height * hero_w / w_img.width)
    hero_logo = w_img.resize((hero_w, hero_h), Image.LANCZOS)
    hero.alpha_composite(hero_logo, ((840 - hero_w) // 2, 285 - hero_h // 2))
    hero.save(ASSETS / "WingetEasy-Logo-Hero.png", optimize=True)
    print("WingetEasy-Logo-Hero.png: 840x560")


if __name__ == "__main__":
    main()
