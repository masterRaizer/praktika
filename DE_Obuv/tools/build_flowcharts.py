"""Блок-схемы алгоритмов (ГОСТ 19.701-90) для задания 2.

  * algorithm_system — алгоритм работы системы оформления заказа обуви;
  * algorithm_price  — алгоритм расчёта цены товара с учётом скидки.

Для каждой схемы получаются .drawio (редактируемый исходник), .svg, .png, .pdf в папке algorithms.
Запуск:  python tools/build_flowcharts.py   (нужен пакет cairosvg)
"""
import math
import os
from xml.sax.saxutils import escape

import cairosvg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "algorithms")
FONT = "DejaVu Sans"
INK, LINE, MUTED = "#1F2937", "#334155", "#B45309"
FILLS = {"term": "#E3ECF8", "proc": "#FFFFFF", "dec": "#FFF7ED", "io": "#ECF7EF", "pre": "#F3EEFC"}
STROKES = {"term": "#2B4C7E", "proc": "#334155", "dec": "#B45309", "io": "#2F7A47", "pre": "#5B3FA8"}


class Chart:
    def __init__(self, name, w, h, title):
        self.name, self.w, self.h, self.title = name, w, h, title
        self.nodes, self.order, self.edges = {}, [], []

    def n(self, nid, kind, cx, cy, text, w=250, h=None):
        h = h or {"term": 44, "dec": 84}.get(kind, 56)
        self.nodes[nid] = dict(kind=kind, cx=cx, cy=cy, w=w, h=h, text=text)
        self.order.append(nid)

    def side(self, nid, s):
        n = self.nodes[nid]
        dx = {"l": -n["w"] / 2, "r": n["w"] / 2}.get(s, 0)
        dy = {"t": -n["h"] / 2, "b": n["h"] / 2}.get(s, 0)
        if n["kind"] == "io" and s in ("l", "r"):
            dx += -9 if s == "r" else 9   # середина наклонной стороны параллелограмма
        return (n["cx"] + dx, n["cy"] + dy)

    def e(self, a, sa, b, sb, via=(), label=None, lpos=None):
        pts = [self.side(a, sa)] + list(via) + [self.side(b, sb)]
        self.edges.append(dict(src=a, dst=b, pts=pts, label=label, lpos=lpos))


# ---------------------------------------------------------------- SVG
def text_svg(x, y, s, fs=13, anchor="middle", color=INK, weight="normal"):
    lines = s.split("\n")
    lh = fs * 1.3
    y0 = y - (len(lines) - 1) * lh / 2 + fs * 0.36
    return "".join(f'<text x="{x:.1f}" y="{y0 + i * lh:.1f}" font-family="{FONT}" font-size="{fs}" '
                   f'font-weight="{weight}" fill="{color}" text-anchor="{anchor}">{escape(q)}</text>'
                   for i, q in enumerate(lines))


def shape_svg(n):
    x, y, w, h = n["cx"] - n["w"] / 2, n["cy"] - n["h"] / 2, n["w"], n["h"]
    f, s = FILLS[n["kind"]], STROKES[n["kind"]]
    st = f'fill="{f}" stroke="{s}" stroke-width="1.8"'
    k = n["kind"]
    if k == "term":
        return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{h / 2}" {st}/>'
    if k == "proc":
        return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" {st}/>'
    if k == "pre":
        return (f'<rect x="{x}" y="{y}" width="{w}" height="{h}" {st}/>'
                f'<line x1="{x + 14}" y1="{y}" x2="{x + 14}" y2="{y + h}" stroke="{s}" stroke-width="1.8"/>'
                f'<line x1="{x + w - 14}" y1="{y}" x2="{x + w - 14}" y2="{y + h}" stroke="{s}" stroke-width="1.8"/>')
    if k == "io":
        o = 18
        return f'<polygon points="{x + o},{y} {x + w},{y} {x + w - o},{y + h} {x},{y + h}" {st}/>'
    return (f'<polygon points="{n["cx"]},{y} {x + w},{n["cy"]} {n["cx"]},{y + h} {x},{n["cy"]}" {st}/>')


def arrow(a, b):
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    L, W = 11, 5
    bx, by = b[0] - L * math.cos(ang), b[1] - L * math.sin(ang)
    return (f'<polygon points="{bx + W * math.sin(ang):.1f},{by - W * math.cos(ang):.1f} {b[0]:.1f},{b[1]:.1f} '
            f'{bx - W * math.sin(ang):.1f},{by + W * math.cos(ang):.1f}" fill="{LINE}"/>')


def render_svg(c):
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{c.w}" height="{c.h}" viewBox="0 0 {c.w} {c.h}">',
         f'<rect width="{c.w}" height="{c.h}" fill="#FFFFFF"/>', text_svg(c.w / 2, 36, c.title, 20, weight="bold")]
    for e in c.edges:
        o.append(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in e["pts"])}" fill="none" '
                 f'stroke="{LINE}" stroke-width="1.6"/>')
        o.append(arrow(e["pts"][-2], e["pts"][-1]))
    for nid in c.order:
        n = c.nodes[nid]
        o.append(shape_svg(n))
        o.append(text_svg(n["cx"], n["cy"], n["text"], 12.5 if n["kind"] != "dec" else 12))
    for e in c.edges:
        if e["label"]:
            lx, ly = e["lpos"] or (e["pts"][0][0] + 8, e["pts"][0][1] - 8)
            o.append(f'<rect x="{lx - 2}" y="{ly - 12}" width="{len(e["label"]) * 7.6 + 4:.1f}" height="16" fill="#FFFFFF"/>')
            o.append(text_svg(lx, ly - 4, e["label"], 12, "start", MUTED, "bold"))
    o.append("</svg>")
    return "\n".join(o)


# ---------------------------------------------------------------- draw.io
def render_drawio(c):
    f = f"fontFamily={FONT};fontColor={INK};"
    style = {
        "term": "rounded=1;arcSize=50;", "proc": "rounded=0;", "dec": "rhombus;",
        "io": "shape=parallelogram;perimeter=parallelogramPerimeter;size=0.07;",
        "pre": "shape=process;backgroundOutline=1;size=0.06;",
    }
    cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>',
             f'<mxCell id="title" value="{escape(c.title)}" style="text;html=1;align=center;fontSize=20;fontStyle=1;{f}" '
             f'vertex="1" parent="1"><mxGeometry x="{c.w / 2 - 400}" y="16" width="800" height="36" as="geometry"/></mxCell>']
    for nid in c.order:
        n = c.nodes[nid]
        v = escape(n["text"]).replace("\n", "&lt;br&gt;")
        st = (style[n["kind"]] + f"whiteSpace=wrap;html=1;fillColor={FILLS[n['kind']]};strokeColor={STROKES[n['kind']]};"
              f"strokeWidth=1.8;fontSize=12;{f}")
        cells.append(f'<mxCell id="{nid}" value="{v}" style="{st}" vertex="1" parent="1"><mxGeometry '
                     f'x="{n["cx"] - n["w"] / 2}" y="{n["cy"] - n["h"] / 2}" width="{n["w"]}" height="{n["h"]}" as="geometry"/></mxCell>')
    for i, e in enumerate(c.edges):
        def rel(nid, p):
            n = c.nodes[nid]
            return round((p[0] - n["cx"]) / n["w"] + 0.5, 4), round((p[1] - n["cy"]) / n["h"] + 0.5, 4)
        ex, ey = rel(e["src"], e["pts"][0])
        nx, ny = rel(e["dst"], e["pts"][-1])
        st = (f"html=1;rounded=0;endArrow=block;endFill=1;endSize=8;strokeColor={LINE};strokeWidth=1.6;"
              f"exitX={ex};exitY={ey};exitDx=0;exitDy=0;entryX={nx};entryY={ny};entryDx=0;entryDy=0;"
              f"fontSize=12;fontStyle=1;fontColor={MUTED};labelBackgroundColor=#FFFFFF;{f}")
        way = ""
        if len(e["pts"]) > 2:
            way = '<Array as="points">' + "".join(f'<mxPoint x="{x:.1f}" y="{y:.1f}"/>' for x, y in e["pts"][1:-1]) + "</Array>"
        cells.append(f'<mxCell id="e{i}" value="" style="{st}" edge="1" parent="1" source="{e["src"]}" target="{e["dst"]}">'
                     f'<mxGeometry relative="1" as="geometry">{way}</mxGeometry></mxCell>')
        if e["label"]:
            lx, ly = e["lpos"] or (e["pts"][0][0] + 8, e["pts"][0][1] - 8)
            wl = len(e["label"]) * 7.6
            cells.append(f'<mxCell id="e{i}l" value="{escape(e["label"])}" style="edgeLabel;html=1;resizable=0;points=[];'
                         f'fontSize=12;fontStyle=1;fontColor={MUTED};labelBackgroundColor=#FFFFFF;{f}" vertex="1" '
                         f'connectable="0" parent="e{i}"><mxGeometry x="-1" relative="1" as="geometry">'
                         f'<mxPoint x="{lx + wl / 2 - e["pts"][0][0]:.1f}" y="{ly - 8 - e["pts"][0][1]:.1f}" as="offset"/>'
                         f'</mxGeometry></mxCell>')
    return ('<mxfile host="drawio" type="device">\n'
            f'<diagram id="{c.name}" name="{c.name}">\n<mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" '
            f'tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{c.w}" pageHeight="{c.h}" '
            f'math="0" shadow="0">\n<root>\n' + "\n".join(cells) + '\n</root>\n</mxGraphModel>\n</diagram>\n</mxfile>\n')


# ---------------------------------------------------------------- 1. алгоритм расчёта цены
def price_chart():
    c = Chart("algorithm_price", 900, 1180, "Алгоритм расчёта цены товара с учётом скидки")
    X = 400
    c.n("s", "term", X, 90, "Начало", 180)
    c.n("in", "io", X, 170, "Ввод: цена модели P,\nдата расчёта D", 280)
    c.n("chk", "dec", X, 270, "P > 0 ?", 200)
    c.n("err", "io", 720, 270, "Вывод: «Цена товара\nдолжна быть больше нуля»", 260)
    c.n("per", "proc", X, 380, "Начало периода S = 1-е число\nмесяца D минус 1 месяц;\nконец периода E = 1-е число месяца D", 330, 70)
    c.n("cnt", "proc", X, 490, "N = число заказов модели\n(любого размера) с датой S ≤ дата < E", 330, 62)
    c.n("dec", "dec", X, 600, "N = 0 ?", 200)
    c.n("disc", "proc", 210, 710, "Цена = P × (100 − 25) / 100", 280)
    c.n("full", "proc", 590, 710, "Цена = P", 200)
    c.n("rnd", "proc", X, 820, "Округлить цену до копеек\n(0,5 копейки — вверх)", 300)
    c.n("out", "io", X, 920, "Вывод: цена со скидкой", 280)
    c.n("end", "term", X, 1010, "Конец", 180)
    c.n("end2", "term", 720, 380, "Конец", 160)
    c.e("s", "b", "in", "t")
    c.e("in", "b", "chk", "t")
    c.e("chk", "r", "err", "l", label="нет")
    c.e("err", "b", "end2", "t")
    c.e("chk", "b", "per", "t", label="да", lpos=(X + 8, 330))
    c.e("per", "b", "cnt", "t")
    c.e("cnt", "b", "dec", "t")
    c.e("dec", "l", "disc", "t", via=[(210, 600)], label="да", lpos=(250, 592))
    c.e("dec", "r", "full", "t", via=[(590, 600)], label="нет", lpos=(520, 592))
    c.e("disc", "b", "rnd", "l", via=[(210, 820)])
    c.e("full", "b", "rnd", "r", via=[(590, 820)])
    c.e("rnd", "b", "out", "t")
    c.e("out", "b", "end", "t")
    c.h = 1070
    return c


# ---------------------------------------------------------------- 2. алгоритм работы системы
def system_chart():
    c = Chart("algorithm_system", 1240, 1900, "Алгоритм работы системы оформления заказа обуви")
    X, L, R = 520, 170, 960
    c.n("s", "term", X, 90, "Начало", 180)
    c.n("db", "proc", X, 170, "Запуск приложения,\nподключение к базе данных", 300)
    c.n("dbok", "dec", X, 270, "Подключение\nуспешно?", 220)
    c.n("dberr", "io", R, 270, "Вывод сообщения об ошибке\nи порядка её исправления", 300)
    c.n("load", "pre", X, 380, "Загрузка каталога и расчёт\nцены каждой модели со скидкой", 320, 62)
    c.n("show", "io", X, 480, "Вывод каталога: изображение, категория,\nнаименование, производство, состав, цена,\n«много» / «мало»; подсветка ≤ 3 пар", 420, 76)
    c.n("login", "io", X, 600, "Ввод логина", 240)
    c.n("found", "dec", X, 700, "Логин есть\nв списке?", 220)
    c.n("lerr", "io", L, 700, "Сообщение\n«Пользователь не найден»", 240)
    c.n("fio", "proc", X, 810, "Вывод ФИО пользователя,\nдоступ к функциям по роли", 300)
    c.n("role", "dec", X, 920, "Роль —\nклиент?", 200)
    c.n("orders", "pre", R, 920, "Работа со списком заказов:\nпросмотр, добавление,\nудаление, редактирование", 300, 76)
    c.n("pick", "proc", X, 1040, "Поиск, фильтр, сортировка;\nвыбор модели в каталоге", 300)
    c.n("size", "io", X, 1140, "Ввод размера и количества пар", 330)
    c.n("avail", "dec", X, 1245, "Количество\nдоступно?", 220)
    c.n("aerr", "io", L, 1245, "Сообщение\n«Нет нужного количества»", 240)
    c.n("add", "proc", X, 1355, "Добавить позицию в заказ", 300)
    c.n("more", "dec", X, 1460, "Добавить ещё\nмодель?", 220)
    c.n("conf", "dec", X, 1580, "Подтвердить\nзаказ?", 220)
    c.n("save", "proc", X, 1695, "Сохранить заказ с датой,\nуменьшить остатки, обновить каталог", 340, 62)
    c.n("end", "term", X, 1800, "Конец", 180)
    c.n("cancel", "proc", R, 1580, "Отказ от заказа:\nпозиции не сохраняются", 260)
    c.n("end_db", "term", R, 370, "Конец", 160)
    c.n("end_o", "term", R, 1050, "Конец", 160)
    c.e("s", "b", "db", "t")
    c.e("db", "b", "dbok", "t")
    c.e("dbok", "r", "dberr", "l", label="нет")
    c.e("dberr", "b", "end_db", "t")
    c.e("dbok", "b", "load", "t", label="да", lpos=(X + 8, 330))
    c.e("load", "b", "show", "t")
    c.e("show", "b", "login", "t")
    c.e("login", "b", "found", "t")
    c.e("found", "l", "lerr", "r", label="нет", lpos=(330, 692))
    c.e("lerr", "t", "login", "l", via=[(L, 600)])
    c.e("found", "b", "fio", "t", label="да", lpos=(X + 8, 760))
    c.e("fio", "b", "role", "t")
    c.e("role", "r", "orders", "l", label="нет", lpos=(640, 912))
    c.e("orders", "b", "end_o", "t")
    c.e("role", "b", "pick", "t", label="да", lpos=(X + 8, 980))
    c.e("pick", "b", "size", "t")
    c.e("size", "b", "avail", "t")
    c.e("avail", "l", "aerr", "r", label="нет", lpos=(330, 1237))
    c.e("aerr", "t", "size", "l", via=[(L, 1140)])
    c.e("avail", "b", "add", "t", label="да", lpos=(X + 8, 1305))
    c.e("add", "b", "more", "t")
    c.e("more", "l", "pick", "l", via=[(60, 1460), (60, 1040)], label="да", lpos=(330, 1452))
    c.e("more", "b", "conf", "t", label="нет", lpos=(X + 8, 1520))
    c.e("conf", "r", "cancel", "l", label="нет", lpos=(650, 1572))
    c.e("cancel", "b", "end", "r", via=[(R, 1800)])
    c.e("conf", "b", "save", "t", label="да", lpos=(X + 8, 1640))
    c.e("save", "b", "end", "t")
    c.h = 1860
    return c


def build(c):
    os.makedirs(OUT, exist_ok=True)
    svg = render_svg(c)
    base = os.path.join(OUT, c.name)
    open(base + ".svg", "w", encoding="utf-8").write(svg)
    open(base + ".drawio", "w", encoding="utf-8").write(render_drawio(c))
    cairosvg.svg2png(bytestring=svg.encode(), write_to=base + ".png", scale=2)
    cairosvg.svg2pdf(bytestring=svg.encode(), write_to=base + ".pdf")
    print("ok:", c.name)


if __name__ == "__main__":
    build(system_chart())
    build(price_chart())
