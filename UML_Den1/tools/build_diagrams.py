"""Генератор UML-диаграмм для «Дня 1».

Из одного описания каждой диаграммы получаются:
  * .drawio — редактируемый исходник (открывается в diagrams.net / draw.io);
  * .svg, .png, .pdf — экспорт.

Запуск:  python tools/build_diagrams.py   (нужен пакет cairosvg)
"""
import math
import os
from xml.sax.saxutils import escape

import cairosvg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
FONT = "DejaVu Sans"

INK = "#1F2937"
STROKE = "#2B4C7E"
FILL = "#F3F7FC"
LINE = "#475569"
DEC_STROKE = "#B45309"
DEC_FILL = "#FFF7ED"
LANE_LINE = "#94A3B8"
LANE_COLORS = ["#EAF2FC", "#EDF7EF", "#FCEFEF"]


def text_width(s, fs):
    return len(s) * fs * 0.6


# ---------------------------------------------------------------- модель
class Diagram:
    def __init__(self, name, w, h):
        self.name, self.w, self.h = name, w, h
        self.nodes = {}
        self.order = []
        self.edges = []
        self.lanes = []      # (title, x0, x1, color)
        self.boundary = None  # (title, x0, y0, x1, y1)
        self.title = None     # (text, x, y)

    def node(self, nid, kind, x, y, text="", w=None, h=None, fs=13, stereo=None):
        defaults = {
            "action": (210, 50), "decision": (190, 76), "merge": (36, 36),
            "start": (26, 26), "end": (28, 28), "bar": (300, 8),
            "actor": (36, 70),
        }
        if kind == "usecase":
            w = w or text_width(text, fs) + 46
            h = h or 54
        else:
            dw, dh = defaults[kind]
            w, h = w or dw, h or dh
        self.nodes[nid] = dict(id=nid, kind=kind, x=x, y=y, w=w, h=h, text=text, fs=fs, stereo=stereo)
        self.order.append(nid)

    def edge(self, src, dst, kind="flow", label=None, via=(), out=None, inn=None, lpos=0.5):
        self.edges.append(dict(src=src, dst=dst, kind=kind, label=label, via=list(via),
                               out=out, inn=inn, lpos=lpos))

    # --- геометрия
    def anchor(self, nid, side):
        n = self.nodes[nid]
        dx = {"l": -n["w"] / 2, "r": n["w"] / 2}.get(side, 0)
        dy = {"t": -n["h"] / 2, "b": n["h"] / 2}.get(side, 0)
        return (n["x"] + dx, n["y"] + dy)

    def perimeter(self, nid, toward):
        n = self.nodes[nid]
        cx, cy = n["x"], n["y"]
        dx, dy = toward[0] - cx, toward[1] - cy
        if n["kind"] == "usecase":
            a, b = n["w"] / 2, n["h"] / 2
            t = 1 / math.sqrt((dx / a) ** 2 + (dy / b) ** 2)
            return (cx + dx * t, cy + dy * t)
        # прямоугольник (актор: фигура + подпись)
        a, b = 34, 62
        t = min(a / abs(dx) if dx else 1e9, b / abs(dy) if dy else 1e9)
        return (cx + dx * t, cy + dy * t)

    def path(self, e):
        if e["out"]:
            p0 = self.anchor(e["src"], e["out"])
            p1 = self.anchor(e["dst"], e["inn"])
            via = list(e["via"])
            # у полос fork/join ребро выходит/входит в точке над/под ветвью
            if self.nodes[e["src"]]["kind"] == "bar" and via:
                p0, via = (via[0][0], p0[1]), via[1:]
            if self.nodes[e["dst"]]["kind"] == "bar" and via:
                p1, via = (via[-1][0], p1[1]), via[:-1]
            return [p0] + via + [p1]
        s, d = self.nodes[e["src"]], self.nodes[e["dst"]]
        return [self.perimeter(e["src"], (d["x"], d["y"])),
                self.perimeter(e["dst"], (s["x"], s["y"]))]


# ---------------------------------------------------------------- SVG
def svg_text(x, y, text, fs=13, anchor="middle", weight="normal", color=INK, italic=False):
    lines = text.split("\n")
    lh = fs * 1.25
    y0 = y - (len(lines) - 1) * lh / 2 + fs * 0.35
    style = ' font-style="italic"' if italic else ""
    out = []
    for i, ln in enumerate(lines):
        out.append(f'<text x="{x:.1f}" y="{y0 + i * lh:.1f}" font-family="{FONT}" font-size="{fs}" '
                   f'font-weight="{weight}" fill="{color}" text-anchor="{anchor}"{style}>{escape(ln)}</text>')
    return "\n".join(out)


def arrow_head(p_from, p_to, kind):
    ang = math.atan2(p_to[1] - p_from[1], p_to[0] - p_from[0])
    L, W = (14, 7) if kind != "gen" else (18, 10)
    bx, by = p_to[0] - L * math.cos(ang), p_to[1] - L * math.sin(ang)
    lx, ly = bx + W * math.sin(ang), by - W * math.cos(ang)
    rx, ry = bx - W * math.sin(ang), by + W * math.cos(ang)
    if kind == "gen":
        return (f'<polygon points="{lx:.1f},{ly:.1f} {p_to[0]:.1f},{p_to[1]:.1f} {rx:.1f},{ry:.1f}" '
                f'fill="#FFFFFF" stroke="{LINE}" stroke-width="1.6"/>')
    return (f'<polyline points="{lx:.1f},{ly:.1f} {p_to[0]:.1f},{p_to[1]:.1f} {rx:.1f},{ry:.1f}" '
            f'fill="none" stroke="{LINE}" stroke-width="1.6"/>')


def label_point(pts, t):
    segs = [(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
    lens = [math.dist(a, b) for a, b in segs]
    target = sum(lens) * t
    for (a, b), L in zip(segs, lens):
        if target <= L and L > 0:
            k = target / L
            return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)
        target -= L
    return pts[-1]


def render_svg(d):
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{d.w}" height="{d.h}" viewBox="0 0 {d.w} {d.h}">',
         f'<rect width="{d.w}" height="{d.h}" fill="#FFFFFF"/>']
    if d.title:
        t, x, y = d.title
        o.append(svg_text(x, y, t, fs=20, weight="bold"))
    for title, x0, x1, y0, y1, color in d.lanes:
        o.append(f'<rect x="{x0}" y="{y0}" width="{x1 - x0}" height="{y1 - y0}" fill="{color}" fill-opacity="0.45" '
                 f'stroke="{LANE_LINE}" stroke-width="1.2"/>')
        o.append(f'<rect x="{x0}" y="{y0}" width="{x1 - x0}" height="40" fill="{color}" stroke="{LANE_LINE}" stroke-width="1.2"/>')
        o.append(svg_text((x0 + x1) / 2, y0 + 20, title, fs=15, weight="bold"))
    if d.boundary:
        title, x0, y0, x1, y1 = d.boundary
        o.append(f'<rect x="{x0}" y="{y0}" width="{x1 - x0}" height="{y1 - y0}" rx="6" fill="#FBFCFE" '
                 f'stroke="{LANE_LINE}" stroke-width="1.6"/>')
        o.append(svg_text((x0 + x1) / 2, y0 + 26, title, fs=16, weight="bold"))

    # рёбра под узлами
    labels = []
    for e in d.edges:
        pts = d.path(e)
        dash = ' stroke-dasharray="7,5"' if e["kind"] in ("include", "extend") else ""
        ptxt = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        o.append(f'<polyline points="{ptxt}" fill="none" stroke="{LINE}" stroke-width="1.6"{dash}/>')
        if e["kind"] != "assoc":
            o.append(arrow_head(pts[-2], pts[-1], e["kind"]))
        if e["label"]:
            labels.append((label_point(pts, e["lpos"]), e["label"], e["kind"]))

    for nid in d.order:
        n = d.nodes[nid]
        x, y, w, h, k = n["x"], n["y"], n["w"], n["h"], n["kind"]
        if k == "action":
            o.append(f'<rect x="{x - w / 2:.1f}" y="{y - h / 2:.1f}" width="{w}" height="{h}" rx="16" '
                     f'fill="{FILL}" stroke="{STROKE}" stroke-width="1.6"/>')
            o.append(svg_text(x, y, n["text"], n["fs"]))
        elif k in ("decision", "merge"):
            o.append(f'<polygon points="{x},{y - h / 2} {x + w / 2},{y} {x},{y + h / 2} {x - w / 2},{y}" '
                     f'fill="{DEC_FILL}" stroke="{DEC_STROKE}" stroke-width="1.6"/>')
            if n["text"]:
                o.append(svg_text(x, y, n["text"], 12))
        elif k == "start":
            o.append(f'<circle cx="{x}" cy="{y}" r="{w / 2}" fill="{INK}"/>')
        elif k == "end":
            o.append(f'<circle cx="{x}" cy="{y}" r="{w / 2}" fill="#FFFFFF" stroke="{INK}" stroke-width="2"/>')
            o.append(f'<circle cx="{x}" cy="{y}" r="{w / 2 - 6}" fill="{INK}"/>')
        elif k == "bar":
            o.append(f'<rect x="{x - w / 2}" y="{y - h / 2}" width="{w}" height="{h}" rx="2" fill="{INK}"/>')
        elif k == "usecase":
            o.append(f'<ellipse cx="{x}" cy="{y}" rx="{w / 2:.1f}" ry="{h / 2}" fill="{FILL}" '
                     f'stroke="{STROKE}" stroke-width="1.6"/>')
            o.append(svg_text(x, y, n["text"], n["fs"]))
        elif k == "actor":
            top = y - 35
            o.append(f'<g fill="none" stroke="{STROKE}" stroke-width="2.2" stroke-linecap="round">'
                     f'<circle cx="{x}" cy="{top + 10}" r="10" fill="#FFFFFF"/>'
                     f'<line x1="{x}" y1="{top + 20}" x2="{x}" y2="{top + 46}"/>'
                     f'<line x1="{x - 18}" y1="{top + 29}" x2="{x + 18}" y2="{top + 29}"/>'
                     f'<line x1="{x}" y1="{top + 46}" x2="{x - 15}" y2="{top + 70}"/>'
                     f'<line x1="{x}" y1="{top + 46}" x2="{x + 15}" y2="{top + 70}"/></g>')
            ly = y + 50
            if n["stereo"]:
                o.append(svg_text(x, ly, n["stereo"], 12, color="#64748B", italic=True))
                ly += 17
            o.append(svg_text(x, ly, n["text"], 14, weight="bold"))

    for (x, y), text, kind in labels:
        fs = 12
        tw = max(text_width(s, fs) for s in text.split("\n"))
        nl = len(text.split("\n"))
        o.append(f'<rect x="{x - tw / 2 - 4:.1f}" y="{y - nl * fs * 0.65 - 2:.1f}" width="{tw + 8:.1f}" '
                 f'height="{nl * fs * 1.3 + 4:.1f}" rx="3" fill="#FFFFFF" fill-opacity="0.92"/>')
        color = "#64748B" if kind in ("include", "extend") else DEC_STROKE
        o.append(svg_text(x, y, text, fs, color=color, weight="bold" if kind == "flow" else "normal"))
    o.append("</svg>")
    return "\n".join(o)


# ---------------------------------------------------------------- draw.io
def render_drawio(d):
    cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>']

    def val(s):
        return escape(s.replace("\n", "<br>"), {'"': "&quot;"})

    def vertex(cid, value, style, x, y, w, h):
        cells.append(f'<mxCell id="{cid}" value="{val(value)}" style="{style}" vertex="1" parent="1">'
                     f'<mxGeometry x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" as="geometry"/></mxCell>')

    font = f"fontFamily={FONT};fontColor={INK};"
    if d.title:
        t, x, y = d.title
        vertex("title", t, f"text;html=1;align=center;verticalAlign=middle;fontSize=20;fontStyle=1;{font}",
               x - 300, y - 15, 600, 30)
    for i, (title, x0, x1, y0, y1, color) in enumerate(d.lanes):
        vertex(f"lane{i}", title, f"swimlane;html=1;startSize=40;fillColor={color};swimlaneFillColor={color};"
               f"strokeColor={LANE_LINE};fontSize=15;fontStyle=1;{font}", x0, y0, x1 - x0, y1 - y0)
    if d.boundary:
        title, x0, y0, x1, y1 = d.boundary
        vertex("system", title, f"rounded=1;arcSize=1;html=1;verticalAlign=top;fontSize=16;fontStyle=1;"
               f"fillColor=#FBFCFE;strokeColor={LANE_LINE};spacingTop=10;{font}", x0, y0, x1 - x0, y1 - y0)

    for nid in d.order:
        n = d.nodes[nid]
        x, y, w, h, k = n["x"] - n["w"] / 2, n["y"] - n["h"] / 2, n["w"], n["h"], n["kind"]
        fs = n["fs"]
        if k == "action":
            st = f"rounded=1;arcSize=40;whiteSpace=wrap;html=1;fillColor={FILL};strokeColor={STROKE};fontSize={fs};{font}"
        elif k in ("decision", "merge"):
            st = f"rhombus;whiteSpace=wrap;html=1;fillColor={DEC_FILL};strokeColor={DEC_STROKE};fontSize=12;{font}"
        elif k == "start":
            st = f"ellipse;html=1;fillColor={INK};strokeColor={INK};"
        elif k == "end":
            st = f"ellipse;html=1;shape=endState;fillColor={INK};strokeColor={INK};"
        elif k == "bar":
            st = f"rounded=0;html=1;fillColor={INK};strokeColor=none;"
        elif k == "usecase":
            st = f"ellipse;whiteSpace=wrap;html=1;fillColor={FILL};strokeColor={STROKE};fontSize={fs};{font}"
        elif k == "actor":
            st = (f"shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;outlineConnect=0;"
                  f"fillColor=#FFFFFF;strokeColor={STROKE};strokeWidth=2;fontSize=14;fontStyle=1;{font}")
            x, y, w, h = n["x"] - 15, n["y"] - 35, 30, 70
        value = n["text"]
        if n["stereo"]:
            value = n["stereo"] + "\n" + value
        vertex(nid, value, st, x, y, w, h)

    for i, e in enumerate(d.edges):
        st = f"html=1;rounded=0;strokeColor={LINE};strokeWidth=1.6;fontSize=12;{font}labelBackgroundColor=#FFFFFF;"
        if e["kind"] == "flow":
            st += "endArrow=open;endSize=12;fontStyle=1;fontColor=#B45309;"
        elif e["kind"] in ("include", "extend"):
            st += "endArrow=open;endSize=12;dashed=1;fontColor=#64748B;"
        elif e["kind"] == "gen":
            st += "endArrow=block;endFill=0;endSize=16;"
        else:
            st += "endArrow=none;"
        pts_xml = ""
        geo_x = ""
        if e["out"]:
            pts = d.path(e)

            def rel(nid, p):
                nd = d.nodes[nid]
                return (round((p[0] - nd["x"]) / nd["w"] + 0.5, 3), round((p[1] - nd["y"]) / nd["h"] + 0.5, 3))
            ex, ey = rel(e["src"], pts[0])
            nx, ny = rel(e["dst"], pts[-1])
            st += f"exitX={ex};exitY={ey};exitDx=0;exitDy=0;entryX={nx};entryY={ny};entryDx=0;entryDy=0;"
            if len(pts) > 2:
                pts_xml = '<Array as="points">' + "".join(
                    f'<mxPoint x="{px:.1f}" y="{py:.1f}"/>' for px, py in pts[1:-1]) + "</Array>"
            geo_x = f' x="{(e["lpos"] - 0.5) * 2:.2f}"'
        cells.append(f'<mxCell id="e{i}" value="{val(e["label"] or "")}" style="{st}" edge="1" parent="1" '
                     f'source="{e["src"]}" target="{e["dst"]}"><mxGeometry{geo_x} relative="1" as="geometry">'
                     f'{pts_xml}</mxGeometry></mxCell>')

    return ('<mxfile host="drawio" type="device">\n'
            f'<diagram id="{d.name}" name="{d.name}">\n'
            f'<mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" '
            f'arrows="1" fold="1" page="1" pageScale="1" pageWidth="{d.w}" pageHeight="{d.h}" math="0" shadow="0">\n'
            '<root>\n' + "\n".join(cells) + '\n</root>\n</mxGraphModel>\n</diagram>\n</mxfile>\n')


# ---------------------------------------------------------------- Use Case
def use_case():
    d = Diagram("usecase", 1600, 1100)
    d.boundary = ("Система онлайн-продажи билетов «Киноточка»", 270, 30, 1310, 1070)

    d.node("Viewer", "actor", 125, 420, "Зритель")
    d.node("Cashier", "actor", 125, 930, "Кассир")
    d.node("PaySys", "actor", 1470, 560, "Платёжная система", stereo="«внешняя система»")
    d.node("Admin", "actor", 1470, 880, "Администратор")

    A, B, C = 480, 830, 1140
    for nid, x, y, t in [
        ("Find", A, 120, "Найти фильм в афише"),
        ("Card", A, 240, "Просмотреть карточку фильма"),
        ("Buy", A, 400, "Купить билет"),
        ("MyTickets", A, 620, "Просмотреть мои билеты"),
        ("ReviewUC", A, 740, "Оставить отзыв"),
        ("Sell", A, 860, "Продать билет в кассе"),
        ("Check", A, 980, "Проверить билет на входе"),
        ("Info", B + 70, 240, "Просмотреть сеансы и отзывы"),
        ("Login", B, 330, "Войти в систему"),
        ("Seats", B, 430, "Выбрать сеанс и места"),
        ("Pay", B, 530, "Оплатить заказ"),
        ("Refund", B, 660, "Вернуть билет"),
        ("Reg", C, 330, "Зарегистрироваться"),
        ("Cost", C, 430, "Рассчитать стоимость"),
        ("Films", C, 730, "Добавить / скрыть фильм"),
        ("Schedule", C, 810, "Составить расписание сеансов"),
        ("Price", C, 890, "Установить цены и скидки"),
        ("Stats", C, 970, "Просмотреть статистику продаж"),
    ]:
        d.node(nid, "usecase", x, y, t, fs=14)

    for a, ucs in [("Viewer", ["Find", "Card", "Buy", "MyTickets"]),
                   ("Cashier", ["Sell", "Check"]),
                   ("Admin", ["Films", "Schedule", "Price", "Stats"])]:
        for uc in ucs:
            d.edge(a, uc, "assoc")
    inc, ext = "«include»", "«extend»"
    d.edge("Find", "Card", "include", inc)
    d.edge("Card", "Info", "include", inc)
    d.edge("Buy", "Card", "extend", ext)
    d.edge("Buy", "Login", "include", inc, lpos=0.55)
    d.edge("Reg", "Login", "extend", ext)
    d.edge("Buy", "Seats", "include", inc)
    d.edge("Buy", "Pay", "include", inc, lpos=0.55)
    d.edge("Seats", "Cost", "include", inc)
    d.edge("Refund", "MyTickets", "extend", ext)
    d.edge("ReviewUC", "MyTickets", "extend", ext)
    d.edge("Pay", "PaySys", "assoc")
    d.edge("Refund", "PaySys", "assoc")
    return d


# ---------------------------------------------------------------- Activity
def activity():
    d = Diagram("activity", 1400, 2020)
    d.title = ("Диаграмма деятельности: покупка билета онлайн", 700, 32)
    V, S, SL, SR, P = 190, 780, 520, 1010, 1250
    top, bottom = 60, 2000
    d.lanes = [("Зритель", 20, 380, top, bottom, LANE_COLORS[0]),
               ("Система «Киноточка»", 380, 1120, top, bottom, LANE_COLORS[1]),
               ("Платёжная система", 1120, 1380, top, bottom, LANE_COLORS[2])]

    n = d.node
    n("start", "start", V, 125)
    n("a_open", "action", V, 190, "Открыть афишу кинотеатра")
    n("m_query", "merge", V, 260)
    n("a_query", "action", V, 335, "Ввести запрос: название,\nдата, жанр")
    n("a_search", "action", S, 335, "Найти фильмы\nпо запросу")
    n("d_found", "decision", S, 425, "Фильмы\nнайдены?")
    n("a_none", "action", SR, 425, "Сообщить\n«Ничего не найдено»", w=196)
    n("a_card", "action", V, 520, "Выбрать фильм, открыть\nкарточку: описание,\nсеансы, отзывы", h=64)
    n("d_buy", "decision", V, 620, "Купить\nбилет?")
    n("d_auth", "decision", S, 710, "Вход\nвыполнен?")
    n("a_login", "action", V, 710, "Войти или\nзарегистрироваться")
    n("m_auth", "merge", S, 800)
    n("a_session", "action", V, 885, "Выбрать дату\nи сеанс")
    n("a_hall", "action", S, 885, "Показать схему зала\nсо свободными местами")
    n("m_seats", "merge", V, 960)
    n("a_seats", "action", V, 1035, "Выбрать места")
    n("a_check", "action", S, 1035, "Проверить, свободны\nли выбранные места")
    n("d_free", "decision", S, 1125, "Места\nсвободны?")
    n("a_busy", "action", SL, 1125, "Сообщить, что места\nзаняты, обновить схему")
    n("a_lock", "action", S, 1215, "Заблокировать места на 15 мин,\nрассчитать стоимость со скидкой", w=270)
    n("m_pay", "merge", V, 1305)
    n("a_pay", "action", V, 1385, "Проверить заказ, ввести\nданные карты, оплатить")
    n("a_proc", "action", P, 1385, "Обработать платёж", w=200)
    n("d_paid", "decision", S, 1475, "Оплата\nпрошла?")
    n("d_time", "decision", SL, 1475, "Время брони\nистекло?")
    n("a_unlock", "action", SL, 1570, "Снять блокировку мест")
    n("a_cancel", "action", SL, 1660, "Отменить заказ")
    n("a_notify", "action", V, 1660, "Получить уведомление\nоб отмене заказа")
    n("end_fail", "end", V, 1745)
    n("a_issue", "action", S, 1570, "Оформить билеты,\nсгенерировать QR-коды")
    n("a_mail", "action", S, 1660, "Отправить билеты\nна e-mail")
    n("a_ticket", "action", V, 1850, "Получить\nэлектронный билет")
    n("end_ok", "end", V, 1935)

    e = d.edge
    e("start", "a_open", out="b", inn="t")
    e("a_open", "m_query", out="b", inn="t")
    e("m_query", "a_query", out="b", inn="t")
    e("a_query", "a_search", out="r", inn="l")
    e("a_search", "d_found", out="b", inn="t")
    e("d_found", "a_none", label="нет", out="r", inn="l")
    e("a_none", "m_query", out="t", inn="r", via=[(SR, 260)])
    e("d_found", "a_card", label="да", out="b", inn="t", via=[(S, 475), (V, 475)], lpos=0.06)
    e("a_card", "d_buy", out="b", inn="t")
    e("d_buy", "m_query", label="нет", out="l", inn="l",
      via=[(50, 620), (50, 260)], lpos=0.04)
    e("d_buy", "d_auth", label="да", out="b", inn="t", via=[(V, 662), (S, 662)], lpos=0.1)
    e("d_auth", "a_login", label="нет", out="l", inn="r", lpos=0.2)
    e("d_auth", "m_auth", label="да", out="b", inn="t", lpos=0.35)
    e("a_login", "m_auth", out="b", inn="l", via=[(V, 800)])
    e("m_auth", "a_session", out="b", inn="t", via=[(S, 840), (V, 840)])
    e("a_session", "a_hall", out="r", inn="l")
    e("a_hall", "m_seats", out="b", inn="r", via=[(S, 960)])
    e("m_seats", "a_seats", out="b", inn="t")
    e("a_seats", "a_check", out="r", inn="l")
    e("a_check", "d_free", out="b", inn="t")
    e("d_free", "a_busy", label="нет", out="l", inn="r")
    e("a_busy", "m_seats", out="l", inn="l", via=[(50, 1125), (50, 960)])
    e("d_free", "a_lock", label="да", out="b", inn="t", lpos=0.35)
    e("a_lock", "m_pay", out="b", inn="t", via=[(S, 1265), (V, 1265)])
    e("m_pay", "a_pay", out="b", inn="t")
    e("a_pay", "a_proc", out="r", inn="l")
    e("a_proc", "d_paid", out="b", inn="r", via=[(P, 1475)])
    e("d_paid", "d_time", label="нет", out="l", inn="r")
    e("d_time", "m_pay", label="нет —\nповторить", out="l", inn="l", via=[(50, 1475), (50, 1305)], lpos=0.12)
    e("d_time", "a_unlock", label="да", out="b", inn="t", lpos=0.35)
    e("a_unlock", "a_cancel", out="b", inn="t")
    e("a_cancel", "a_notify", out="l", inn="r")
    e("a_notify", "end_fail", out="b", inn="t")
    e("d_paid", "a_issue", label="да", out="b", inn="t", lpos=0.35)
    e("a_issue", "a_mail", out="b", inn="t")
    e("a_mail", "a_ticket", out="b", inn="r", via=[(S, 1850)])
    e("a_ticket", "end_ok", out="b", inn="t")
    return d


def build(d):
    svg = render_svg(d)
    base = os.path.join(OUT, d.name)
    with open(base + ".svg", "w", encoding="utf-8") as f:
        f.write(svg)
    with open(base + ".drawio", "w", encoding="utf-8") as f:
        f.write(render_drawio(d))
    cairosvg.svg2png(bytestring=svg.encode(), write_to=base + ".png", scale=2)
    cairosvg.svg2pdf(bytestring=svg.encode(), write_to=base + ".pdf")
    print("ok:", d.name)


if __name__ == "__main__":
    build(use_case())
    build(activity())
