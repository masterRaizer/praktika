"""Ресурсы приложения «Шаг»: логотип, иконка, картинка-заглушка и изображения моделей обуви.

Запуск:  python tools/make_images.py   (нужны пакеты cairosvg и pillow)
Результат кладётся в src/ShagShoes/Resources и src/ShagShoes/Images.
"""
import io
import os

import cairosvg
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "ShagShoes")
RES = os.path.join(ROOT, "Resources")
IMG = os.path.join(ROOT, "Images")

# силуэты в поле 200×140 (вид сбоку, носок справа)
SHAPES = {
    "sneaker": dict(
        upper="M22,96 C22,74 38,64 58,60 L92,42 C104,36 116,39 123,49 L136,70 C154,72 176,79 184,93 L184,99 L22,99 Z",
        sole="M16,97 L188,97 C190,97 192,99 192,102 L192,106 C192,110 189,113 185,113 L22,113 C18,113 16,110 16,106 Z",
        extra='<path d="M96,52 L108,62 M90,58 L102,68 M84,63 L96,73" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round" opacity=".85"/>'
              '<path d="M40,84 C80,80 120,82 160,88" stroke="#FFFFFF" stroke-width="3" fill="none" opacity=".5"/>'),
    "boot": dict(
        upper="M34,99 L34,30 C34,22 40,17 48,17 L94,17 C100,17 103,22 103,28 L105,62 C132,66 168,74 180,92 L180,99 Z",
        sole="M28,97 L184,97 C187,97 189,99 189,102 L189,107 C189,111 186,114 182,114 L34,114 C30,114 28,111 28,107 Z",
        extra='<path d="M40,24 L98,24" stroke="#FFFFFF" stroke-width="5" opacity=".55"/>'
              '<path d="M86,36 L98,40 M84,46 L98,50 M84,56 L100,60" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round" opacity=".8"/>'),
    "shoe": dict(
        upper="M26,98 C26,82 38,74 54,72 L112,64 C142,62 170,74 182,92 L182,99 Z",
        sole="M22,97 L186,97 C188,97 190,99 190,101 L190,104 C190,107 188,109 185,109 L58,109 L56,118 L28,118 L26,109 C23,109 22,106 22,104 Z",
        extra='<path d="M62,74 C90,70 120,70 150,76" stroke="#FFFFFF" stroke-width="2.5" fill="none" opacity=".55"/>'),
    "sandal": dict(
        upper="M60,96 C64,80 74,72 86,70 L100,70 C110,70 116,76 118,84 L120,96 Z M136,96 C140,84 150,78 162,80 C170,82 176,88 178,96 Z",
        sole="M20,95 L188,95 C191,95 193,97 193,100 L193,105 C193,109 190,112 186,112 L26,112 C22,112 20,109 20,105 Z",
        extra='<path d="M34,96 C40,76 56,66 70,64" stroke-width="8" stroke-linecap="round" fill="none" stroke="{c}"/>'),
}


def product(kind, color, sole, bg1, bg2, path):
    s = SHAPES[kind]
    extra = s["extra"].replace("{c}", color)
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="400" height="300" viewBox="0 0 200 150">
<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{bg1}"/><stop offset="1" stop-color="{bg2}"/></linearGradient>
<linearGradient id="up" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{color}"/><stop offset="1" stop-color="{color}" stop-opacity=".82"/></linearGradient></defs>
<rect width="200" height="150" fill="url(#bg)"/>
<ellipse cx="104" cy="122" rx="86" ry="7" fill="#000000" opacity=".12"/>
<g transform="translate(0,4)"><path d="{s["upper"]}" fill="url(#up)"/>{extra}<path d="{s["sole"]}" fill="{sole}"/></g>
</svg>'''
    cairosvg.svg2png(bytestring=svg.encode(), write_to=path)


def placeholder(path):
    svg = '''<svg xmlns="http://www.w3.org/2000/svg" width="400" height="300" viewBox="0 0 200 150">
<rect width="200" height="150" fill="#E9ECF3"/>
<g fill="none" stroke="#9AA3B8" stroke-width="7" stroke-linejoin="round" stroke-linecap="round">
<rect x="52" y="30" width="96" height="90" rx="14"/>
<path d="M60,110 L88,76 L104,94 L118,80 L140,110"/></g>
<circle cx="120" cy="54" r="9" fill="none" stroke="#9AA3B8" stroke-width="6"/>
</svg>'''
    cairosvg.svg2png(bytestring=svg.encode(), write_to=path)


LOGO = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 64 64">
<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#7C6CFF"/><stop offset=".55" stop-color="#22D3EE"/><stop offset="1" stop-color="#F471B5"/></linearGradient></defs>
<rect width="64" height="64" rx="16" fill="url(#g)"/>
<g fill="#FFFFFF"><path d="M12,42 C12,35 17,31 23,30 L31,24 C34,22 37,23 39,26 L42,32 C48,33 53,36 54,41 L54,44 L12,44 Z"/>
<rect x="10" y="44" width="46" height="5" rx="2.5"/></g>
<path d="M30,30 L34,34 M27,33 L31,37" stroke="url(#g)" stroke-width="2.2" stroke-linecap="round"/>
</svg>'''


def main():
    os.makedirs(RES, exist_ok=True)
    os.makedirs(IMG, exist_ok=True)
    cairosvg.svg2png(bytestring=LOGO.encode(), write_to=os.path.join(RES, "logo.png"), output_width=256, output_height=256)
    png = cairosvg.svg2png(bytestring=LOGO.encode(), output_width=256, output_height=256)
    Image.open(io.BytesIO(png)).save(os.path.join(RES, "icon.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    placeholder(os.path.join(RES, "picture.png"))
    items = [
        ("sneaker_wind.png", "sneaker", "#2F80ED", "#FFFFFF", "#E8F1FF", "#CFE0FF"),
        ("sneaker_city.png", "sneaker", "#F4F4F4", "#3A3F4B", "#F2EFEA", "#DCD6CC"),
        ("boots_taiga.png", "boot", "#8A5A35", "#2E2A27", "#F5EDE4", "#E3D3C0"),
        ("boots_trek.png", "boot", "#5C6B3C", "#2B2B2B", "#EEF2E6", "#D5DEC4"),
        ("shoes_milan.png", "shoe", "#5A2E1B", "#1E1A18", "#F3ECE7", "#E0D2C8"),
        ("boots_vyuga.png", "boot", "#6B4E8A", "#2A2433", "#F1ECF7", "#DCD1EA"),
        ("sneaker_start.png", "sneaker", "#E2483D", "#F2F2F2", "#FFF0EE", "#FFD6D1"),
        ("sandals_breeze.png", "sandal", "#B07A45", "#5B4632", "#FBF3E8", "#EFDCC3"),
    ]
    for name, kind, color, sole, bg1, bg2 in items:
        product(kind, color, sole, bg1, bg2, os.path.join(IMG, name))
    print("ok:", len(items), "изображений, логотип, иконка, заглушка")


if __name__ == "__main__":
    main()
