import sys
from PIL import Image, ImageChops
pad = int(sys.argv[2]) if len(sys.argv) > 2 else 24
im = Image.open(sys.argv[1]).convert("RGB")
bg = Image.new("RGB", im.size, (255, 255, 255))
bbox = ImageChops.difference(im, bg).getbbox()
if bbox:
    l, t, r, b = bbox
    im = im.crop((max(0, l - pad), max(0, t - pad), min(im.width, r + pad), min(im.height, b + pad)))
im.save(sys.argv[1], optimize=True)
print(im.size[0], im.size[1])
