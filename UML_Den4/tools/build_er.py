"""ER-диаграмма базы «Киноточка» (день 4) в нотации «вороньих лапок» (crow's foot).

Столбцы, типы, первичные, внешние и уникальные ключи берутся из tools/schema.json —
выгрузки из созданной базы (sys.tables / sys.columns), поэтому диаграмма совпадает со скриптом.
Результат: er.drawio (редактируемый), er.svg, er.png, er.pdf.
Запуск:  python tools/build_er.py   (нужен пакет cairosvg)
"""
import json
import math
import os
from xml.sax.saxutils import escape

import cairosvg

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..")
FONT = "DejaVu Sans"
INK, MUTED, LINE = "#1F2937", "#64748B", "#475569"

GROUPS = {  # заливка заголовка, рамка
    "dict":    ("#FDEBD3", "#B45309"),
    "catalog": ("#DDF1E3", "#2F7A47"),
    "users":   ("#DCE9FA", "#2B4C7E"),
    "sales":   ("#FBE0E6", "#B4234A"),
}
TABLES = {
    "Roles": ("Роли", "dict"), "Genres": ("Жанры", "dict"), "SeatTypes": ("Типы мест", "dict"),
    "OrderStatuses": ("Статусы заказа", "dict"), "PromoCodes": ("Промокоды", "dict"),
    "Films": ("Фильмы", "catalog"), "Halls": ("Залы", "catalog"), "Seats": ("Места", "catalog"),
    "Sessions": ("Сеансы", "catalog"),
    "Users": ("Пользователи", "users"), "Reviews": ("Отзывы", "users"),
    "Orders": ("Заказы", "sales"), "Tickets": ("Билеты", "sales"), "Payments": ("Платежи", "sales"),
}
GRID = [  # строки × столбцы
    ["Roles", "Reviews", "Films", "Genres"],
    ["Users", "Orders", "Sessions", "Halls"],
    ["PromoCodes", "Payments", "Tickets", "Seats"],
    ["OrderStatuses", None, None, "SeatTypes"],
]
W, HEAD, ROW = 340, 46, 21
GAP_X, GAP_Y, M, TOP = 110, 70, 40, 80


def tw(s, fs):
    return len(s) * fs * 0.6


def load():
    cols = {}
    for r in json.load(open(os.path.join(HERE, "schema.json"), encoding="utf-8")):
        cols.setdefault(r["tbl"], []).append(r)
    for t in cols:
        cols[t].sort(key=lambda r: r["ord"])
    return cols


def layout(cols):
    box = {}
    y = TOP
    for row in GRID:
        hmax = 0
        for c, t in enumerate(row):
            if not t:
                continue
            h = HEAD + len(cols[t]) * ROW + 8
            box[t] = dict(x=M + c * (W + GAP_X), y=y, w=W, h=h)
            hmax = max(hmax, h)
        y += hmax + GAP_Y
    return box


def key_mark(r):
    k = []
    if r["pk"]:
        k.append("PK")
    if r.get("fk"):
        k.append("FK")
    if r["uq"] and not r["pk"]:
        k.append("UQ")
    return ",".join(k)


# ---------------------------------------------------------------- связи
def relations(b):
    """(родитель, дочерняя, точки от родителя к дочерней, обозначение у родителя, у дочерней, подпись)."""
    def L(t, dy):
        return (b[t]["x"], b[t]["y"] + dy)

    def R(t, dy):
        return (b[t]["x"] + W, b[t]["y"] + dy)

    def T(t, dx=W / 2):
        return (b[t]["x"] + dx, b[t]["y"])

    def B(t, dx=W / 2):
        return (b[t]["x"] + dx, b[t]["y"] + b[t]["h"])

    g01 = b["Users"]["x"] + W  # правая граница первого столбца
    g12 = b["Orders"]["x"] + W
    rel = [
        ("Roles", "Users", [B("Roles"), T("Users")], "1", "0N", None),
        ("Users", "Reviews", [R("Users", 34), (g01 + 55, b["Users"]["y"] + 34), (g01 + 55, b["Reviews"]["y"] + 34),
                              L("Reviews", 34)], "1", "0N", None),
        ("Films", "Reviews", [L("Films", 60), R("Reviews", 60)], "1", "0N", None),
        ("Genres", "Films", [L("Genres", 60), R("Films", 60)], "1", "0N", None),
        ("Users", "Orders", [R("Users", 70), L("Orders", 70)], "01", "0N", "покупатель"),
        ("Users", "Orders", [R("Users", 112), L("Orders", 112)], "01", "0N", "кассир"),
        ("Sessions", "Orders", [L("Sessions", 90), R("Orders", 90)], "1", "0N", None),
        ("Films", "Sessions", [B("Films"), T("Sessions")], "1", "0N", None),
        ("Halls", "Sessions", [L("Halls", 70), R("Sessions", 70)], "1", "0N", None),
        ("Halls", "Seats", [B("Halls"), T("Seats")], "1", "1N", None),
        ("SeatTypes", "Seats", [T("SeatTypes"), B("Seats")], "1", "0N", None),
        ("Seats", "Tickets", [L("Seats", 70), R("Tickets", 70)], "1", "0N", None),
        ("Orders", "Tickets", [R("Orders", b["Orders"]["h"] - 30), (g12 + 55, b["Orders"]["y"] + b["Orders"]["h"] - 30),
                               (g12 + 55, b["Tickets"]["y"] + 40), L("Tickets", 40)], "1", "1N", None),
        ("Orders", "Payments", [B("Orders"), T("Payments")], "1", "0N", None),
        ("PromoCodes", "Orders", [R("PromoCodes", 40), (g01 + 35, b["PromoCodes"]["y"] + 40),
                                  (g01 + 35, b["Orders"]["y"] + 150), L("Orders", 150)], "01", "0N", None),
        ("OrderStatuses", "Orders", [R("OrderStatuses", 40), (g01 + 75, b["OrderStatuses"]["y"] + 40),
                                     (g01 + 75, b["Orders"]["y"] + 186), L("Orders", 186)], "1", "0N", None),
    ]
    return rel


# ---------------------------------------------------------------- SVG
def t_svg(x, y, s, fs, anchor="start", weight="normal", color=INK, italic=False, family=FONT):
    st = ' font-style="italic"' if italic else ""
    return (f'<text x="{x:.1f}" y="{y:.1f}" font-family="{family}" font-size="{fs}" font-weight="{weight}" '
            f'fill="{color}" text-anchor="{anchor}"{st}>{escape(s)}</text>')


def crow(p, q, kind):
    """Обозначение кратности у сущности: p — точка на границе, q — следующая точка линии."""
    d = math.dist(p, q)
    ux, uy = (q[0] - p[0]) / d, (q[1] - p[1]) / d
    nx, ny = -uy, ux
    o = []

    def at(k):
        return (p[0] + ux * k, p[1] + uy * k)

    def bar(k):
        c = at(k)
        o.append(f'<line x1="{c[0] + nx * 8:.1f}" y1="{c[1] + ny * 8:.1f}" x2="{c[0] - nx * 8:.1f}" '
                 f'y2="{c[1] - ny * 8:.1f}" stroke="{LINE}" stroke-width="1.6"/>')

    def circle(k):
        c = at(k)
        o.append(f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="5" fill="#FFFFFF" stroke="{LINE}" stroke-width="1.6"/>')

    def foot():
        a = at(16)
        for s in (-1, 0, 1):
            e = (p[0] + nx * 9 * s, p[1] + ny * 9 * s)
            o.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{e[0]:.1f}" y2="{e[1]:.1f}" '
                     f'stroke="{LINE}" stroke-width="1.6"/>')

    if kind == "1":
        bar(9), bar(16)
    elif kind == "01":
        bar(10), circle(22)
    elif kind == "1N":
        foot(), bar(22)
    elif kind == "0N":
        foot(), circle(26)
    return "".join(o)


def render_svg(cols, b, rel, w, h):
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">',
         f'<rect width="{w}" height="{h}" fill="#FFFFFF"/>',
         t_svg(w / 2, 42, "ER-диаграмма базы данных «Киноточка»", 22, "middle", "bold")]
    for par, ch, pts, kp, kc, lab in rel:
        o.append(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in pts)}" fill="none" '
                 f'stroke="{LINE}" stroke-width="1.6"/>')
        o.append(crow(pts[0], pts[1], kp))
        o.append(crow(pts[-1], pts[-2], kc))
        if lab:
            mx = (pts[0][0] + pts[-1][0]) / 2
            o.append(f'<rect x="{mx - tw(lab, 11) / 2 - 3:.1f}" y="{pts[0][1] - 16:.1f}" width="{tw(lab, 11) + 6:.1f}" '
                     f'height="13" fill="#FFFFFF"/>')
            o.append(t_svg(mx, pts[0][1] - 6, lab, 11, "middle", color=MUTED, italic=True))
    for t, bx in b.items():
        cap, grp = TABLES[t]
        fill, stroke = GROUPS[grp]
        x, y = bx["x"], bx["y"]
        o.append(f'<rect x="{x}" y="{y}" width="{W}" height="{bx["h"]}" fill="#FFFFFF" stroke="{stroke}" stroke-width="1.6"/>')
        o.append(f'<rect x="{x}" y="{y}" width="{W}" height="{HEAD}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
        o.append(t_svg(x + 12, y + 20, t, 15, weight="bold"))
        o.append(t_svg(x + 12, y + 37, cap, 11.5, color=MUTED, italic=True))
        for i, r in enumerate(cols[t]):
            ry = y + HEAD + 4 + i * ROW + 15
            k = key_mark(r)
            if r["pk"]:
                o.append(f'<rect x="{x + 1}" y="{ry - 15}" width="{W - 2}" height="{ROW}" fill="{fill}" fill-opacity="0.45"/>')
            o.append(t_svg(x + 10, ry, k, 10.5, weight="bold", color=stroke))
            o.append(t_svg(x + 64, ry, r["col"], 12.5, weight="bold" if r["pk"] else "normal",
                           color=INK if not r["nullable"] else MUTED))
            typ = r["type"] + ("" if not r["nullable"] else " NULL")
            o.append(t_svg(x + W - 10, ry, typ, 11, "end", color=MUTED))
    # легенда
    ly = h - 40
    lx = M
    items = [("1", "ровно один"), ("01", "ноль или один"), ("1N", "один или много"), ("0N", "ноль или много")]
    for k, s in items:
        o.append(f'<line x1="{lx}" y1="{ly}" x2="{lx + 60}" y2="{ly}" stroke="{LINE}" stroke-width="1.6"/>')
        o.append(crow((lx + 60, ly), (lx, ly), k))
        o.append(t_svg(lx + 70, ly + 4, s, 12))
        lx += 220
    o.append(t_svg(lx + 10, ly - 4, "PK — первичный ключ, FK — внешний ключ, UQ — уникальное значение "
                   "(у нескольких столбцов — уникальна их комбинация);", 12, color=MUTED))
    o.append(t_svg(lx + 10, ly + 12, "серым — столбцы, допускающие NULL", 12, color=MUTED))
    o.append("</svg>")
    return "\n".join(o)


# ---------------------------------------------------------------- draw.io
def render_drawio(cols, b, rel, w, h):
    f = f"fontFamily={FONT};"
    cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>',
             f'<mxCell id="title" value="ER-диаграмма базы данных «Киноточка»" style="text;html=1;align=center;'
             f'fontSize=22;fontStyle=1;fontColor={INK};{f}" vertex="1" parent="1"><mxGeometry x="{w / 2 - 400}" y="20" '
             f'width="800" height="36" as="geometry"/></mxCell>']
    for t, bx in b.items():
        cap, grp = TABLES[t]
        fill, stroke = GROUPS[grp]
        rows = "".join(
            f'<tr style="height:{ROW}px"><td style="width:46px;color:{stroke};font-size:10px"><b>{key_mark(r)}</b></td>'
            f'<td style="color:{INK if not r["nullable"] else MUTED}">{"<b>" if r["pk"] else ""}{escape(r["col"])}'
            f'{"</b>" if r["pk"] else ""}</td><td style="text-align:right;color:{MUTED};font-size:11px">'
            f'{escape(r["type"])}{" NULL" if r["nullable"] else ""}</td></tr>' for r in cols[t])
        v = (f'<div style="background:{fill};padding:4px 10px;height:{HEAD - 9}px;border-bottom:1px solid {stroke}">'
             f'<b style="font-size:15px">{t}</b><br><i style="font-size:11px;color:{MUTED}">{escape(cap)}</i></div>'
             f'<table style="width:100%;font-size:12px;border-collapse:collapse;margin:4px 0 0 0;padding:0 8px">{rows}</table>')
        st = (f"verticalAlign=top;align=left;overflow=fill;html=1;whiteSpace=wrap;fillColor=#FFFFFF;"
              f"strokeColor={stroke};strokeWidth=1.6;spacing=0;fontColor={INK};{f}")
        cells.append(f'<mxCell id="{t}" value="{escape(v, {chr(34): "&quot;"})}" style="{st}" vertex="1" parent="1">'
                     f'<mxGeometry x="{bx["x"]}" y="{bx["y"]}" width="{W}" height="{bx["h"]}" as="geometry"/></mxCell>')
    er = {"1": "ERmandOne", "01": "ERzeroToOne", "1N": "ERoneToMany", "0N": "ERzeroToMany"}
    for i, (par, ch, pts, kp, kc, lab) in enumerate(rel):
        def rel_pt(t, p):
            bx = b[t]
            return round((p[0] - bx["x"]) / W, 4), round((p[1] - bx["y"]) / bx["h"], 4)
        ex, ey = rel_pt(par, pts[0])
        nx, ny = rel_pt(ch, pts[-1])
        st = (f"html=1;rounded=0;strokeColor={LINE};strokeWidth=1.6;startArrow={er[kp]};endArrow={er[kc]};"
              f"startFill=0;endFill=0;startSize=10;endSize=10;fontSize=11;fontStyle=2;fontColor={MUTED};"
              f"exitX={ex};exitY={ey};exitDx=0;exitDy=0;entryX={nx};entryY={ny};entryDx=0;entryDy=0;{f}")
        way = ""
        if len(pts) > 2:
            way = '<Array as="points">' + "".join(f'<mxPoint x="{x:.1f}" y="{y:.1f}"/>' for x, y in pts[1:-1]) + "</Array>"
        cells.append(f'<mxCell id="r{i}" value="{escape(lab or "")}" style="{st}" edge="1" parent="1" source="{par}" '
                     f'target="{ch}"><mxGeometry relative="1" as="geometry">{way}</mxGeometry></mxCell>')
    cells.append(f'<mxCell id="legend" value="PK — первичный ключ, FK — внешний ключ, UQ — уникальное значение (у нескольких столбцов — уникальна их комбинация); '
                 f'серым — столбцы, допускающие NULL. Концы связей: || ровно один, o| ноль или один, '
                 f'|&lt; один или много, o&lt; ноль или много" style="text;html=1;align=left;fontSize=12;'
                 f'fontColor={MUTED};{f}" vertex="1" parent="1"><mxGeometry x="{M}" y="{h - 56}" width="{w - 2 * M}" '
                 f'height="30" as="geometry"/></mxCell>')
    return ('<mxfile host="drawio" type="device">\n<diagram id="er" name="er">\n'
            f'<mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" '
            f'fold="1" page="1" pageScale="1" pageWidth="{w}" pageHeight="{h}" math="0" shadow="0">\n<root>\n'
            + "\n".join(cells) + '\n</root>\n</mxGraphModel>\n</diagram>\n</mxfile>\n')


def main():
    cols = load()
    assert set(cols) == set(TABLES), set(cols) ^ set(TABLES)
    b = layout(cols)
    rel = relations(b)
    # каждая связь на диаграмме соответствует внешнему ключу и наоборот
    fks = sorted((r["fk"], r["tbl"]) for t in cols for r in cols[t] if r.get("fk"))
    assert sorted((p, c) for p, c, *_ in rel) == fks, (fks, sorted((p, c) for p, c, *_ in rel))
    w = M * 2 + 4 * W + 3 * GAP_X
    h = int(max(bx["y"] + bx["h"] for bx in b.values()) + 80)
    svg = render_svg(cols, b, rel, w, h)
    base = os.path.join(OUT, "er")
    open(base + ".svg", "w", encoding="utf-8").write(svg)
    open(base + ".drawio", "w", encoding="utf-8").write(render_drawio(cols, b, rel, w, h))
    cairosvg.svg2png(bytestring=svg.encode(), write_to=base + ".png", scale=2)
    cairosvg.svg2pdf(bytestring=svg.encode(), write_to=base + ".pdf")
    print("ok: er", w, h, "связей:", len(rel))


if __name__ == "__main__":
    main()
