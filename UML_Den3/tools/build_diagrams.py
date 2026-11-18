"""Генератор функциональных диаграмм для «Дня 3».

Диаграммы:
  * hierarchy — иерархия функций: общая функция → подсистемы → операции;
  * idef0_context — контекстная диаграмма IDEF0 (A-0);
  * idef0_a0 — декомпозиция IDEF0 (A0): подсистемы и связи между ними.

Для каждой получаются .drawio (редактируемый исходник), .svg, .png, .pdf.
Запуск:  python tools/build_diagrams.py   (нужен пакет cairosvg)
"""
import math
import os
from xml.sax.saxutils import escape

import cairosvg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
FONT = "DejaVu Sans"

INK = "#1F2937"
MUTED = "#64748B"
STROKE = "#2B4C7E"
FILL = "#F3F7FC"
LINE = "#475569"
LABEL = "#1F2937"
ROOT_FILL = "#1E3A5F"
SUB_FILLS = ["#E3EEFB", "#E6F4EA", "#FFF4E0", "#FDE8EC", "#EFE9FB"]
SUB_STROKES = ["#2B4C7E", "#2F7A47", "#B45309", "#B4234A", "#5B3FA8"]
BADGE = {"П": ("#FEE2E2", "#B91C1C"), "К": ("#DBEAFE", "#1D4ED8")}


def tw(s, fs):
    return len(s) * fs * 0.6


def ea(s):
    return escape(s, {'"': "&quot;"})


# ---------------------------------------------------------------- модель
class Diagram:
    def __init__(self, name, w, h, title):
        self.name, self.w, self.h, self.title = name, w, h, title
        self.nodes, self.order, self.edges, self.texts = {}, [], [], []

    def box(self, nid, x, y, w, h, text, fill=FILL, stroke=STROKE, fs=13, bold=False, color=INK,
            sub=None, rx=8, badges=(), code=None, text_dx=0):
        self.nodes[nid] = dict(id=nid, x=x, y=y, w=w, h=h, text=text, fill=fill, stroke=stroke, fs=fs, bold=bold,
                               color=color, sub=sub, rx=rx, badges=list(badges), code=code, text_dx=text_dx)
        self.order.append(nid)
        return self.nodes[nid]

    def edge(self, src, dst, pts, arrow=True, labels=()):
        """src/dst = None — стрелка с границы диаграммы. labels = [(текст, x, y, 'start'|'middle'|'end')]."""
        self.edges.append(dict(src=src, dst=dst, pts=[tuple(p) for p in pts], arrow=arrow, labels=list(labels)))

    def text(self, x, y, s, fs=12, anchor="start", color=INK, bold=False):
        self.texts.append(dict(x=x, y=y, s=s, fs=fs, anchor=anchor, color=color, bold=bold))


# ---------------------------------------------------------------- SVG
def t_svg(x, y, s, fs, anchor="middle", weight="normal", color=INK, italic=False):
    st = ' font-style="italic"' if italic else ""
    return (f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{fs}" font-weight="{weight}" '
            f'fill="{color}" text-anchor="{anchor}"{st}>{escape(s)}</text>')


def lines_svg(x, yc, text, fs, anchor="middle", weight="normal", color=INK):
    ls = text.split("\n")
    lh = fs * 1.28
    y0 = yc - (len(ls) - 1) * lh / 2 + fs * 0.36
    return "".join(t_svg(x, y0 + i * lh, s, fs, anchor, weight, color) for i, s in enumerate(ls))


def arrowhead(a, b):
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    L, W = 12, 5.5
    bx, by = b[0] - L * math.cos(ang), b[1] - L * math.sin(ang)
    return (f'<polygon points="{bx + W * math.sin(ang):.1f},{by - W * math.cos(ang):.1f} {b[0]:.1f},{b[1]:.1f} '
            f'{bx - W * math.sin(ang):.1f},{by + W * math.cos(ang):.1f}" fill="{LINE}"/>')


def render_svg(d):
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{d.w}" height="{d.h}" viewBox="0 0 {d.w} {d.h}">',
         f'<rect width="{d.w}" height="{d.h}" fill="#FFFFFF"/>',
         t_svg(d.w / 2, 40, d.title, 22, "middle", "bold")]
    for e in d.edges:
        pts = e["pts"]
        o.append(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in pts)}" fill="none" '
                 f'stroke="{LINE}" stroke-width="1.6"/>')
        if e["arrow"]:
            o.append(arrowhead(pts[-2], pts[-1]))
    for nid in d.order:
        n = d.nodes[nid]
        x, y, w, h = n["x"], n["y"], n["w"], n["h"]
        o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{n["rx"]}" fill="{n["fill"]}" '
                 f'stroke="{n["stroke"]}" stroke-width="1.6"/>')
        cx = x + w / 2 + n["text_dx"]
        if n["sub"]:
            o.append(lines_svg(cx, y + h / 2 - 9, n["text"], n["fs"], "middle", "bold" if n["bold"] else "normal", n["color"]))
            o.append(t_svg(cx, y + h - 12, n["sub"], 11, "middle", color=MUTED, italic=True))
        else:
            o.append(lines_svg(cx, y + h / 2, n["text"], n["fs"], "middle", "bold" if n["bold"] else "normal", n["color"]))
        if n["code"]:
            o.append(t_svg(x + w - 8, y + h - 8, n["code"], 13, "end", "bold", n["stroke"]))
        for i, b in enumerate(n["badges"]):
            bf, bs = BADGE[b]
            bx, by = x + w - 13, y + 13 + i * 22
            o.append(f'<circle cx="{bx}" cy="{by}" r="9" fill="{bf}" stroke="{bs}" stroke-width="1.2"/>')
            o.append(t_svg(bx, by + 4, b, 11, "middle", "bold", bs))
    for e in d.edges:
        for s, lx, ly, anchor in e["labels"]:
            ls = s.split("\n")
            wm = max(tw(q, 12) for q in ls)
            x0 = {"start": lx, "middle": lx - wm / 2, "end": lx - wm}[anchor]
            o.append(f'<rect x="{x0 - 3:.1f}" y="{ly - 13:.1f}" width="{wm + 6:.1f}" height="{len(ls) * 15.5 + 3:.1f}" '
                     f'fill="#FFFFFF" fill-opacity="0.92"/>')
            for i, q in enumerate(ls):
                o.append(t_svg(lx, ly + i * 15.5, q, 12, anchor, color=LABEL))
    for t in d.texts:
        o.append(lines_svg(t["x"], t["y"], t["s"], t["fs"], t["anchor"], "bold" if t["bold"] else "normal", t["color"]))
    o.append("</svg>")
    return "\n".join(o)


# ---------------------------------------------------------------- draw.io
def render_drawio(d):
    font = f"fontFamily={FONT};"
    cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>',
             f'<mxCell id="title" value="{ea(d.title)}" style="text;html=1;align=center;verticalAlign=middle;'
             f'fontSize=22;fontStyle=1;fontColor={INK};{font}" vertex="1" parent="1"><mxGeometry x="{d.w / 2 - 500}" '
             f'y="20" width="1000" height="36" as="geometry"/></mxCell>']

    def html(s):
        return "<br>".join(escape(q) for q in s.split("\n"))

    for nid in d.order:
        n = d.nodes[nid]
        v = f'<b>{html(n["text"])}</b>' if n["bold"] else html(n["text"])
        if n["sub"]:
            v += f'<br><i style="font-size:11px;color:{MUTED}">{escape(n["sub"])}</i>'
        right = 30 if n["badges"] else 4
        st = (f"rounded=1;arcSize={max(1, int(n['rx'] * 100 / min(n['w'], n['h'])))};absoluteArcSize=0;whiteSpace=wrap;html=1;"
              f"fillColor={n['fill']};strokeColor={n['stroke']};strokeWidth=1.6;fontSize={n['fs']};fontColor={n['color']};"
              f"spacingRight={right};{font}")
        cells.append(f'<mxCell id="{nid}" value="{ea(v)}" style="{st}" vertex="1" parent="1"><mxGeometry '
                     f'x="{n["x"]}" y="{n["y"]}" width="{n["w"]}" height="{n["h"]}" as="geometry"/></mxCell>')
        if n["code"]:
            cells.append(f'<mxCell id="{nid}_code" value="{ea(n["code"])}" style="text;html=1;align=right;'
                         f'verticalAlign=bottom;fontSize=13;fontStyle=1;fontColor={n["stroke"]};{font}" vertex="1" '
                         f'parent="1"><mxGeometry x="{n["x"] + n["w"] - 60}" y="{n["y"] + n["h"] - 24}" width="54" '
                         f'height="20" as="geometry"/></mxCell>')
        for i, b in enumerate(n["badges"]):
            bf, bs = BADGE[b]
            cells.append(f'<mxCell id="{nid}_b{i}" value="{b}" style="ellipse;html=1;fillColor={bf};strokeColor={bs};'
                         f'fontColor={bs};fontStyle=1;fontSize=11;{font}" vertex="1" parent="1"><mxGeometry '
                         f'x="{n["x"] + n["w"] - 22}" y="{n["y"] + 4 + i * 22}" width="18" height="18" as="geometry"/></mxCell>')

    for i, e in enumerate(d.edges):
        pts = e["pts"]
        st = (f"html=1;rounded=0;strokeColor={LINE};strokeWidth=1.6;{font}"
              + ("endArrow=block;endFill=1;endSize=10;" if e["arrow"] else "endArrow=none;"))
        ends = ""
        if e["src"]:
            n = d.nodes[e["src"]]
            st += f"exitX={(pts[0][0] - n['x']) / n['w']:.4f};exitY={(pts[0][1] - n['y']) / n['h']:.4f};exitDx=0;exitDy=0;"
        else:
            ends += f'<mxPoint x="{pts[0][0]:.1f}" y="{pts[0][1]:.1f}" as="sourcePoint"/>'
        if e["dst"]:
            n = d.nodes[e["dst"]]
            st += f"entryX={(pts[-1][0] - n['x']) / n['w']:.4f};entryY={(pts[-1][1] - n['y']) / n['h']:.4f};entryDx=0;entryDy=0;"
        else:
            ends += f'<mxPoint x="{pts[-1][0]:.1f}" y="{pts[-1][1]:.1f}" as="targetPoint"/>'
        way = ""
        if len(pts) > 2:
            way = '<Array as="points">' + "".join(f'<mxPoint x="{x:.1f}" y="{y:.1f}"/>' for x, y in pts[1:-1]) + "</Array>"
        conn = (f' source="{e["src"]}"' if e["src"] else "") + (f' target="{e["dst"]}"' if e["dst"] else "")
        cells.append(f'<mxCell id="e{i}" value="" style="{st}" edge="1" parent="1"{conn}>'
                     f'<mxGeometry relative="1" as="geometry">{ends}{way}</mxGeometry></mxCell>')
        for j, (s, lx, ly, anchor) in enumerate(e["labels"]):
            ls = s.split("\n")
            wm = max(tw(q, 12) for q in ls)
            cxl = {"start": lx + wm / 2, "middle": lx, "end": lx - wm / 2}[anchor]
            cyl = ly - 4 + (len(ls) - 1) * 7.75
            cells.append(f'<mxCell id="e{i}l{j}" value="{ea(html(s))}" style="edgeLabel;html=1;align=center;'
                         f'verticalAlign=middle;resizable=0;points=[];fontSize=12;fontColor={LABEL};'
                         f'labelBackgroundColor=#FFFFFF;{font}" vertex="1" connectable="0" parent="e{i}">'
                         f'<mxGeometry x="-1" relative="1" as="geometry"><mxPoint x="{cxl - pts[0][0]:.1f}" '
                         f'y="{cyl - pts[0][1]:.1f}" as="offset"/></mxGeometry></mxCell>')

    for k, t in enumerate(d.texts):
        ls = t["s"].split("\n")
        wm = max(tw(q, t["fs"]) for q in ls) + 10
        hh = len(ls) * t["fs"] * 1.3 + 6
        x0 = {"start": t["x"], "middle": t["x"] - wm / 2, "end": t["x"] - wm}[t["anchor"]]
        align = {"start": "left", "middle": "center", "end": "right"}[t["anchor"]]
        cells.append(f'<mxCell id="t{k}" value="{ea(html(t["s"]))}" style="text;html=1;align={align};verticalAlign=middle;'
                     f'fontSize={t["fs"]};fontColor={t["color"]};{"fontStyle=1;" if t["bold"] else ""}{font}" vertex="1" '
                     f'parent="1"><mxGeometry x="{x0:.1f}" y="{t["y"] - hh / 2:.1f}" width="{wm:.1f}" height="{hh:.1f}" '
                     f'as="geometry"/></mxCell>')

    return ('<mxfile host="drawio" type="device">\n'
            f'<diagram id="{d.name}" name="{d.name}">\n'
            f'<mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" '
            f'arrows="1" fold="1" page="1" pageScale="1" pageWidth="{d.w}" pageHeight="{d.h}" math="0" shadow="0">\n'
            '<root>\n' + "\n".join(cells) + '\n</root>\n</mxGraphModel>\n</diagram>\n</mxfile>\n')


# ---------------------------------------------------------------- 1. иерархия функций
SUBSYSTEMS = [
    ("1. Пользователи\nи личный кабинет", "Зритель, Администратор", [
        ("1.1 Зарегистрироваться", "К"),
        ("1.2 Войти / выйти", "К"),
        ("1.3 Просмотреть мои билеты", "П"),
        ("1.4 Оставить отзыв", "ПК"),
        ("1.5 Управлять учётными\nзаписями сотрудников", "ПК"),
    ]),
    ("2. Работа с данными:\nкаталог и расписание", "Администратор", [
        ("2.1 Добавить / изменить\nфильм", "ПК"),
        ("2.2 Скрыть фильм из афиши", "П"),
        ("2.3 Вести залы\nи схемы мест", "ПК"),
        ("2.4 Составить расписание\nсеансов", "ПК"),
        ("2.5 Установить цены\nи промокоды", "ПК"),
    ]),
    ("3. Поиск\nи просмотр", "Зритель, Кассир", [
        ("3.1 Найти фильм в афише", "К"),
        ("3.2 Просмотреть карточку\nфильма", ""),
        ("3.3 Просмотреть сеансы\nна дату", "К"),
        ("3.4 Показать схему зала\nсо свободными местами", ""),
        ("3.5 Просмотреть отзывы\nи рейтинг", ""),
    ]),
    ("4. Оформление\nопераций", "Зритель, Кассир, Платёжная система", [
        ("4.1 Выбрать места,\nсоздать бронь", "ПК"),
        ("4.2 Рассчитать стоимость,\nприменить промокод", "К"),
        ("4.3 Оплатить заказ", "ПК"),
        ("4.4 Выпустить билеты,\nотправить на e-mail", ""),
        ("4.5 Снять просроченные\nброни (автоматически)", ""),
        ("4.6 Продать билет в кассе", "ПК"),
        ("4.7 Вернуть билет", "ПК"),
        ("4.8 Проверить билет\nна входе", "ПК"),
    ]),
    ("5. Отчётность", "Администратор", [
        ("5.1 Отчёт о продажах\nи выручке за период", "ПК"),
        ("5.2 Заполняемость\nсеансов и залов", "ПК"),
        ("5.3 Популярность фильмов", "ПК"),
        ("5.4 Отчёт о возвратах\nи отменах", "ПК"),
    ]),
]


def hierarchy():
    COLW, GAP, M = 272, 24, 40
    W = M * 2 + COLW * 5 + GAP * 4
    d = Diagram("hierarchy", W, 100, "Функциональная диаграмма: иерархия функций системы")
    rw = 520
    d.box("root", W / 2 - rw / 2, 72, rw, 74, "«Киноточка» — система продажи\nбилетов в кинотеатр",
          fill=ROOT_FILL, stroke=ROOT_FILL, fs=16, bold=True, color="#FFFFFF", rx=10)
    bus = 182
    sub_top, sub_h = 214, 78
    opw, oph, step = COLW - 30, 50, 64
    max_bottom = 0
    for i, (name, roles, ops) in enumerate(SUBSYSTEMS):
        x = M + i * (COLW + GAP)
        sid = f"s{i + 1}"
        d.box(sid, x, sub_top, COLW, sub_h, name, fill=SUB_FILLS[i], stroke=SUB_STROKES[i], fs=14, bold=True,
              sub=roles, rx=8)
        d.edge("root", sid, [(W / 2, 146), (W / 2, bus), (x + COLW / 2, bus), (x + COLW / 2, sub_top)], arrow=False)
        xl = x + 14
        for j, (op, badges) in enumerate(ops):
            oy = sub_top + sub_h + 22 + j * step
            oid = f"o{i + 1}_{j + 1}"
            d.box(oid, x + 30, oy, opw, oph, op, fill="#FFFFFF", stroke=SUB_STROKES[i], fs=12.5, rx=6,
                  badges=list(badges), text_dx=-10 if badges else 0)
            d.edge(sid, oid, [(xl, sub_top + sub_h), (xl, oy + oph / 2), (x + 30, oy + oph / 2)], arrow=False)
            max_bottom = max(max_bottom, oy + oph)
    # легенда
    ly = max_bottom + 42
    d.box("leg_p", M, ly - 9, 18, 18, "П", fill=BADGE["П"][0], stroke=BADGE["П"][1], fs=11, bold=True,
          color=BADGE["П"][1], rx=9)
    d.text(M + 26, ly, "— требуется проверка прав доступа (вход в систему, роль, владелец заказа)", 12)
    d.box("leg_k", M + 640, ly - 9, 18, 18, "К", fill=BADGE["К"][0], stroke=BADGE["К"][1], fs=11, bold=True,
          color=BADGE["К"][1], rx=9)
    d.text(M + 666, ly, "— требуется проверка корректности входных данных", 12)
    d.h = int(ly + 34)
    return d


# ---------------------------------------------------------------- 2. IDEF0 A-0
def idef0_context():
    d = Diagram("idef0_context", 1500, 720, "IDEF0, контекстная диаграмма A-0")
    bx, by, bw, bh = 560, 270, 380, 180
    d.box("A0", bx, by, bw, bh, "Продавать билеты\nв кинотеатр «Киноточка»", fill=FILL, stroke=STROKE, fs=17,
          bold=True, rx=0, code="A0")
    # входы
    ins = ["запрос зрителя: название, дата, жанр", "выбранные места и данные карты",
           "данные регистрации, логин и пароль", "данные о фильмах, залах, сеансах и ценах"]
    for i, s in enumerate(ins):
        y = by + 30 + i * 40
        d.edge(None, "A0", [(60, y), (bx, y)], labels=[(s, 70, y - 7, "start")])
    # управление
    ctr = [("правила продажи и возврата\n(бронь 15 минут, возврат до начала сеанса)", 600, 120),
           ("ролевая модель доступа", 750, 176), ("закон о персональных данных\n(пароли только в виде хеша)", 900, 214)]
    for s, x, top in ctr:
        d.edge(None, "A0", [(x, top), (x, by)], labels=[(s, x + 8, top + 2, "start")])
    # выходы
    outs = ["электронные билеты с QR-кодами", "уведомления на e-mail, возврат денег",
            "отчёты о продажах и заполняемости"]
    for i, s in enumerate(outs):
        y = by + 45 + i * 45
        d.edge("A0", None, [(bx + bw, y), (1440, y)], labels=[(s, bx + bw + 14, y - 7, "start")])
    # механизмы
    mech = [("Администратор", 610, 560), ("Кассир", 700, 610), ("Платёжная система", 800, 560),
            ("БД «Киноточка» (СУБД)", 890, 610)]
    for s, x, bot in mech:
        d.edge(None, "A0", [(x, bot), (x, by + bh)], labels=[(s, x, bot + 18, "middle")])
    d.text(60, 680, "Слева — входы, сверху — управление, справа — выходы, снизу — механизмы (исполнители).",
           12, color=MUTED)
    return d


# ---------------------------------------------------------------- 3. IDEF0 A0
def idef0_a0():
    d = Diagram("idef0_a0", 1880, 1060, "IDEF0, декомпозиция A0: подсистемы и их взаимосвязь")
    W_, H_ = 230, 110
    blocks = [("A1", "Управлять\nпользователями"), ("A2", "Вести каталог\nи расписание"),
              ("A3", "Искать и показывать\nфильмы и сеансы"), ("A4", "Оформлять\nи обслуживать заказы"),
              ("A5", "Формировать\nотчёты")]
    pos = {}
    for i, (code, name) in enumerate(blocks):
        x, y = 300 + i * 270, 150 + i * 170
        pos[code] = (x, y)
        d.box(code, x, y, W_, H_, name, fill=SUB_FILLS[i], stroke=SUB_STROKES[i], fs=14, bold=True, rx=0, code=code)

    def L(c, t):  # точка на левой стороне
        return (pos[c][0], pos[c][1] + H_ * t)

    def R(c, t):
        return (pos[c][0] + W_, pos[c][1] + H_ * t)

    def T(c, dx):
        return (pos[c][0] + dx, pos[c][1])

    def B(c, dx):
        return (pos[c][0] + dx, pos[c][1] + H_)

    # входы с границы
    for c, t, s in [("A1", 0.5, "данные регистрации,\nлогин и пароль"),
                    ("A2", 0.5, "данные о фильмах, залах,\nсеансах и ценах"),
                    ("A3", 0.5, "запрос зрителя:\nназвание, дата, жанр"),
                    ("A4", 0.72, "выбранные места,\nданные карты"),
                    ("A5", 0.72, "период отчёта")]:
        p = L(c, t)
        d.edge(None, c, [(40, p[1]), p], labels=[(s, 50, p[1] - (22 if "\n" in s else 7), "start")])
    # управление с границы
    d.edge(None, "A1", [(T("A1", 115)[0], 90), T("A1", 115)],
           labels=[("ролевая модель доступа,\nзакон о персональных данных", T("A1", 115)[0] + 8, 104, "start")])
    xr = T("A4", 190)[0]
    d.edge(None, "A4", [(xr, 90), T("A4", 190)],
           labels=[("правила продажи и возврата\n(бронь 15 мин, возврат до сеанса)", xr + 8, 104, "start")])
    # A1 → права доступа → управление A2 и A4
    y1 = L("A1", 0.5)[1]
    x2 = T("A2", 50)[0]
    x4 = T("A4", 120)[0]
    d.edge("A1", "A2", [R("A1", 0.5), (x2, y1), T("A2", 50)],
           labels=[("права доступа (роль)", R("A1", 0.5)[0] + 12, y1 - 8, "start")])
    d.edge("A1", "A4", [R("A1", 0.5), (x4, y1), T("A4", 120)])
    # A2 → афиша, расписание, цены → управление A3 и A4
    y2 = L("A2", 0.35)[1]
    x3 = T("A3", 60)[0]
    x4b = T("A4", 50)[0]
    d.edge("A2", "A3", [R("A2", 0.35), (x3, y2), T("A3", 60)],
           labels=[("афиша, расписание,\nцены и промокоды", x3 + 8, y2 + 24, "start")])
    d.edge("A2", "A4", [R("A2", 0.35), (x4b, y2), T("A4", 50)])
    # A3 → выбранный сеанс → вход A4
    xg = R("A3", 0)[0] + 20
    d.edge("A3", "A4", [R("A3", 0.5), (xg, R("A3", 0.5)[1]), (xg, L("A4", 0.3)[1]), L("A4", 0.3)],
           labels=[("выбранный\nсеанс", xg - 6, R("A3", 0.5)[1] + 72, "end")])
    # A4 → данные о продажах → вход A5
    xg = R("A4", 0)[0] + 20
    d.edge("A4", "A5", [R("A4", 0.75), (xg, R("A4", 0.75)[1]), (xg, L("A5", 0.3)[1]), L("A5", 0.3)],
           labels=[("данные о продажах\nи возвратах", xg + 8, R("A4", 0.75)[1] + 30, "start")])
    # выходы на границу
    yo = R("A4", 0.35)[1]
    d.edge("A4", None, [R("A4", 0.35), (1840, yo)],
           labels=[("билеты с QR-кодами, уведомления\nна e-mail, возврат денег", R("A4", 0.35)[0] + 14, yo - 22, "start")])
    yo = R("A5", 0.5)[1]
    d.edge("A5", None, [R("A5", 0.5), (1840, yo)],
           labels=[("отчёты о продажах, заполняемости\nи возвратах", R("A5", 0.5)[0] + 14, yo - 22, "start")])
    # механизмы
    for c, s in [("A1", "Администратор, БД"), ("A2", "Администратор, БД"), ("A3", "приложение, БД"),
                 ("A4", "Кассир, платёжная\nсистема, БД"), ("A5", "Администратор, БД")]:
        p = B(c, 115)
        d.edge(None, c, [(p[0], p[1] + 46), p], labels=[(s, p[0], p[1] + 62, "middle")])
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
    build(hierarchy())
    build(idef0_context())
    build(idef0_a0())
