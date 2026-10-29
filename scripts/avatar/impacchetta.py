"""I fotogrammi dell'avatar (avatar/frames/<oggetto>/<animazione>/NNN.png, fatti da rendi.mjs) in un'immagine sola
risorse/avatar/<oggetto>.png con il suo <oggetto>.json: la finestrella dell'avatar ritaglia da lì ogni fotogramma.
Uso: python scripts/avatar/impacchetta.py <oggetto> [lato=192] [fps=24]
"""
import json, os, sys
from PIL import Image

radice = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
oggetto = sys.argv[1]
lato = int(sys.argv[2]) if len(sys.argv) > 2 else 192
fps = int(sys.argv[3]) if len(sys.argv) > 3 else 24
sorgente = os.path.join(radice, "avatar", "frames", oggetto)
anim = sorted(d for d in os.listdir(sorgente) if os.path.isdir(os.path.join(sorgente, d)))
frames, meta = [], {}
for a in anim:
    files = sorted(f for f in os.listdir(os.path.join(sorgente, a)) if f.endswith(".png"))
    meta[a] = {"da": len(frames), "n": len(files)}
    for f in files:
        im = Image.open(os.path.join(sorgente, a, f)).convert("RGBA").resize((lato, lato), Image.LANCZOS)
        frames.append(im)
colonne = 12
righe = -(-len(frames) // colonne)
foglio = Image.new("RGBA", (colonne * lato, righe * lato), (0, 0, 0, 0))
for i, im in enumerate(frames):
    foglio.paste(im, ((i % colonne) * lato, (i // colonne) * lato))
uscita = os.path.join(radice, "risorse", "avatar")
os.makedirs(uscita, exist_ok=True)
png = os.path.join(uscita, f"{oggetto}.png")
# 256 colori con la trasparenza: a questa misura non si vede la differenza e pesa un quinto (1 MB invece di 5)
foglio = foglio.quantize(256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.FLOYDSTEINBERG)
foglio.save(png, optimize=True)
json.dump({"oggetto": oggetto, "lato": lato, "colonne": colonne, "fps": fps, "animazioni": meta}, open(os.path.join(uscita, f"{oggetto}.json"), "w", encoding="utf-8"), indent=1)
print(f"{oggetto}: {len(frames)} fotogrammi {lato}px -> {png} ({os.path.getsize(png) // 1024} KB)")
