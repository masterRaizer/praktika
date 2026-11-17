"""Генератор UML-диаграмм для «Дня 2»: диаграмма состояний и диаграмма классов.

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
MUTED = "#64748B"
STROKE = "#2B4C7E"
FILL = "#F3F7FC"
HEAD = "#E2EBF7"
ENUM_FILL = "#FFF7ED"
ENUM_HEAD = "#FCE9D2"
ENUM_STROKE = "#B45309"
LINE = "#475569"
LABEL = "#B45309"

LH = 19          # высота строки в классе
FS = 13          # основной размер шрифта


def tw(s, fs=FS):
    return len(s) * fs * 0.6


def esc_attr(s):
    return escape(s, {'"': "&quot;"})


# ---------------------------------------------------------------- модель
class Diagram:
    def __init__(self, name, w, h, title):
        self.name, self.w, self.h, self.title = name, w, h, title
        self.nodes = {}
        self.order = []
        self.edges = []

    def add(self, nid, kind, x, y, w, h, **kw):
        self.nodes[nid] = dict(id=nid, kind=kind, x=x, y=y, w=w, h=h, **kw)  # x, y — левый верхний угол
        self.order.append(nid)
        return self.nodes[nid]

    # класс: высота считается по числу строк
    def cls(self, nid, cx, top, name, attrs, methods=(), stereo=None, w=300):
        head = 34 + (18 if stereo else 0)
        h = head + len(attrs) * LH + 10 + (len(methods) * LH + 10 if methods else 0)
        return self.add(nid, "class", cx - w / 2, top, w, h, name=name, attrs=list(attrs),
                        methods=list(methods), stereo=stereo, head=head)

    def state(self, nid, cx, cy, name, body, w=270):
        h = 38 + len(body) * 18 + 12
        return self.add(nid, "state", cx - w / 2, cy - h / 2, w, h, name=name, body=list(body))

    def dot(self, nid, cx, cy, final=False):
        r = 14 if final else 13
        return self.add(nid, "end" if final else "start", cx - r, cy - r, 2 * r, 2 * r)

    def edge(self, src, dst, pts, kind, labels=()):
        """pts — ломаная в абсолютных координатах (от src к dst).
        labels — [(текст, x, y, выравнивание 'start'|'middle'|'end', роль 'name'|'mult'|'trans')]."""
        self.edges.append(dict(src=src, dst=dst, pts=[tuple(p) for p in pts], kind=kind, labels=list(labels)))

    # точки на сторонах узла
    def side(self, nid, s, t=0.5):
        n = self.nodes[nid]
        if s == "t":
            return (n["x"] + n["w"] * t, n["y"])
        if s == "b":
            return (n["x"] + n["w"] * t, n["y"] + n["h"])
        if s == "l":
            return (n["x"], n["y"] + n["h"] * t)
        return (n["x"] + n["w"], n["y"] + n["h"] * t)

    def cx(self, nid):
        n = self.nodes[nid]
        return n["x"] + n["w"] / 2

    def cy(self, nid):
        n = self.nodes[nid]
        return n["y"] + n["h"] / 2

    def bottom(self, nid):
        n = self.nodes[nid]
        return n["y"] + n["h"]


# ---------------------------------------------------------------- SVG
def t_svg(x, y, s, fs=FS, anchor="start", weight="normal", color=INK, italic=False):
    st = ' font-style="italic"' if italic else ""
    return (f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{fs}" font-weight="{weight}" '
            f'fill="{color}" text-anchor="{anchor}"{st}>{escape(s)}</text>')


def head_open(p_from, p_to):
    a = math.atan2(p_to[1] - p_from[1], p_to[0] - p_from[0])
    L, W = 13, 6.5
    bx, by = p_to[0] - L * math.cos(a), p_to[1] - L * math.sin(a)
    return (f'<polyline points="{bx + W * math.sin(a):.1f},{by - W * math.cos(a):.1f} {p_to[0]:.1f},{p_to[1]:.1f} '
            f'{bx - W * math.sin(a):.1f},{by + W * math.cos(a):.1f}" fill="none" stroke="{LINE}" stroke-width="1.6"/>')


def diamond(p_at, p_next):
    a = math.atan2(p_next[1] - p_at[1], p_next[0] - p_at[0])
    L, W = 22, 7
    tip = p_at
    mid = (p_at[0] + L / 2 * math.cos(a), p_at[1] + L / 2 * math.sin(a))
    back = (p_at[0] + L * math.cos(a), p_at[1] + L * math.sin(a))
    l = (mid[0] + W * math.sin(a), mid[1] - W * math.cos(a))
    r = (mid[0] - W * math.sin(a), mid[1] + W * math.cos(a))
    pts = " ".join(f"{x:.1f},{y:.1f}" for x, y in (tip, l, back, r))
    return f'<polygon points="{pts}" fill="{LINE}" stroke="{LINE}" stroke-width="1.2"/>'


def render_svg(d):
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{d.w}" height="{d.h}" viewBox="0 0 {d.w} {d.h}">',
         f'<rect width="{d.w}" height="{d.h}" fill="#FFFFFF"/>',
         t_svg(d.w / 2, 40, d.title, 22, "middle", "bold")]

    for e in d.edges:
        pts = e["pts"]
        dash = ' stroke-dasharray="7,5"' if e["kind"] == "dep" else ""
        o.append(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in pts)}" fill="none" '
                 f'stroke="{LINE}" stroke-width="1.6"{dash}/>')
        if e["kind"] in ("trans", "dep"):
            o.append(head_open(pts[-2], pts[-1]))
        if e["kind"] == "comp":
            o.append(diamond(pts[0], pts[1]))

    for nid in d.order:
        n = d.nodes[nid]
        x, y, w, h = n["x"], n["y"], n["w"], n["h"]
        if n["kind"] == "class":
            enum = n["stereo"] == "«enumeration»"
            fill, headc, stroke = (ENUM_FILL, ENUM_HEAD, ENUM_STROKE) if enum else (FILL, HEAD, STROKE)
            o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
            o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{n["head"]}" fill="{headc}" stroke="{stroke}" stroke-width="1.6"/>')
            ty = y + 23
            if n["stereo"]:
                o.append(t_svg(x + w / 2, ty - 2, n["stereo"], 12, "middle", color=MUTED, italic=True))
                ty += 18
            o.append(t_svg(x + w / 2, ty, n["name"], 15, "middle", "bold"))
            cy = y + n["head"] + 5
            for a in n["attrs"]:
                o.append(t_svg(x + 10, cy + 14, a))
                cy += LH
            cy += 5
            if n["methods"]:
                o.append(f'<line x1="{x}" y1="{cy}" x2="{x + w}" y2="{cy}" stroke="{stroke}" stroke-width="1.2"/>')
                cy += 5
                for m in n["methods"]:
                    o.append(t_svg(x + 10, cy + 14, m))
                    cy += LH
        elif n["kind"] == "state":
            o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="16" fill="{FILL}" stroke="{STROKE}" stroke-width="1.8"/>')
            o.append(t_svg(x + w / 2, y + 25, n["name"], 15, "middle", "bold"))
            o.append(f'<line x1="{x}" y1="{y + 38}" x2="{x + w}" y2="{y + 38}" stroke="{STROKE}" stroke-width="1.2"/>')
            for i, b in enumerate(n["body"]):
                o.append(t_svg(x + 12, y + 38 + 18 * (i + 1), b, 12))
        elif n["kind"] == "start":
            o.append(f'<circle cx="{x + w / 2}" cy="{y + h / 2}" r="{w / 2}" fill="{INK}"/>')
        elif n["kind"] == "end":
            o.append(f'<circle cx="{x + w / 2}" cy="{y + h / 2}" r="{w / 2}" fill="#FFFFFF" stroke="{INK}" stroke-width="2"/>')
            o.append(f'<circle cx="{x + w / 2}" cy="{y + h / 2}" r="{w / 2 - 6}" fill="{INK}"/>')

    for e in d.edges:
        for text, lx, ly, anchor, role in e["labels"]:
            fs = 13 if role == "mult" else 12
            lines = text.split("\n")
            wmax = max(tw(s, fs) for s in lines)
            x0 = {"start": lx, "middle": lx - wmax / 2, "end": lx - wmax}[anchor]
            o.append(f'<rect x="{x0 - 3:.1f}" y="{ly - fs - 1:.1f}" width="{wmax + 6:.1f}" '
                     f'height="{len(lines) * fs * 1.3 + 4:.1f}" fill="#FFFFFF" fill-opacity="0.9"/>')
            color = {"mult": INK, "name": MUTED, "trans": LABEL}[role]
            weight = "bold" if role == "mult" else "normal"
            for i, s in enumerate(lines):
                o.append(t_svg(lx, ly + i * fs * 1.3, s, fs, anchor, weight, color, italic=(role == "name")))
    o.append("</svg>")
    return "\n".join(o)


# ---------------------------------------------------------------- draw.io
def render_drawio(d):
    font = f"fontFamily={FONT};fontColor={INK};"
    cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>',
             f'<mxCell id="title" value="{esc_attr(d.title)}" style="text;html=1;align=center;verticalAlign=middle;'
             f'fontSize=22;fontStyle=1;{font}" vertex="1" parent="1"><mxGeometry x="{d.w / 2 - 400}" y="20" '
             f'width="800" height="36" as="geometry"/></mxCell>']

    def html_lines(lines):
        return "<br>".join(escape(s) for s in lines)

    for nid in d.order:
        n = d.nodes[nid]
        x, y, w, h = n["x"], n["y"], n["w"], n["h"]
        if n["kind"] == "class":
            enum = n["stereo"] == "«enumeration»"
            fill, stroke = (ENUM_FILL, ENUM_STROKE) if enum else (FILL, STROKE)
            head = ""
            if n["stereo"]:
                head += f'<i style="color:{MUTED};font-size:12px">{escape(n["stereo"])}</i><br>'
            head += f'<b style="font-size:15px">{escape(n["name"])}</b>'
            v = (f'<p style="margin:0px;margin-top:6px;text-align:center;">{head}</p><hr size="1">'
                 f'<p style="margin:0px;margin-left:8px;">{html_lines(n["attrs"])}</p>')
            if n["methods"]:
                v += f'<hr size="1"><p style="margin:0px;margin-left:8px;">{html_lines(n["methods"])}</p>'
            st = (f"verticalAlign=top;align=left;overflow=fill;html=1;whiteSpace=wrap;fontSize={FS};"
                  f"fillColor={fill};strokeColor={stroke};strokeWidth=1.6;{font}")
        elif n["kind"] == "state":
            v = (f'<p style="margin:0px;margin-top:8px;text-align:center;"><b style="font-size:15px">'
                 f'{escape(n["name"])}</b></p><hr size="1">'
                 f'<p style="margin:0px;margin-left:10px;font-size:12px">{html_lines(n["body"])}</p>')
            st = (f"rounded=1;arcSize=14;verticalAlign=top;align=left;overflow=fill;html=1;whiteSpace=wrap;"
                  f"fillColor={FILL};strokeColor={STROKE};strokeWidth=1.8;{font}")
        elif n["kind"] == "start":
            v, st = "", f"ellipse;html=1;fillColor={INK};strokeColor={INK};"
        else:
            v, st = "", f"ellipse;html=1;shape=endState;fillColor={INK};strokeColor={INK};"
        cells.append(f'<mxCell id="{nid}" value="{esc_attr(v)}" style="{st}" vertex="1" parent="1">'
                     f'<mxGeometry x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" as="geometry"/></mxCell>')

    for i, e in enumerate(d.edges):
        pts = e["pts"]
        st = f"html=1;rounded=0;strokeColor={LINE};strokeWidth=1.6;{font}"
        st += {"assoc": "endArrow=none;",
               "comp": "endArrow=none;startArrow=diamondThin;startFill=1;startSize=18;",
               "dep": "endArrow=open;endSize=12;dashed=1;",
               "trans": "endArrow=open;endSize=12;"}[e["kind"]]

        def rel(nid, p):
            n = d.nodes[nid]
            return round((p[0] - n["x"]) / n["w"], 4), round((p[1] - n["y"]) / n["h"], 4)
        ex, ey = rel(e["src"], pts[0])
        nx, ny = rel(e["dst"], pts[-1])
        st += f"exitX={ex};exitY={ey};exitDx=0;exitDy=0;entryX={nx};entryY={ny};entryDx=0;entryDy=0;"
        way = ""
        if len(pts) > 2:
            way = '<Array as="points">' + "".join(f'<mxPoint x="{x:.1f}" y="{y:.1f}"/>' for x, y in pts[1:-1]) + "</Array>"
        eid = f"e{i}"
        cells.append(f'<mxCell id="{eid}" value="" style="{st}" edge="1" parent="1" source="{e["src"]}" '
                     f'target="{e["dst"]}"><mxGeometry relative="1" as="geometry">{way}</mxGeometry></mxCell>')
        for j, (text, lx, ly, anchor, role) in enumerate(e["labels"]):
            fs = 13 if role == "mult" else 12
            lines = text.split("\n")
            wmax = max(tw(s, fs) for s in lines)
            # центр подписи относительно начала ребра
            cxl = {"start": lx + wmax / 2, "middle": lx, "end": lx - wmax / 2}[anchor]
            cyl = ly - fs * 0.35 + (len(lines) - 1) * fs * 0.65
            color = {"mult": INK, "name": MUTED, "trans": LABEL}[role]
            style = (f"edgeLabel;html=1;align=center;verticalAlign=middle;resizable=0;points=[];fontSize={fs};"
                     f"fontFamily={FONT};fontColor={color};labelBackgroundColor=#FFFFFF;"
                     + ("fontStyle=1;" if role == "mult" else "fontStyle=2;" if role == "name" else ""))
            cells.append(f'<mxCell id="{eid}l{j}" value="{esc_attr("<br>".join(escape(s) for s in lines))}" '
                         f'style="{style}" vertex="1" connectable="0" parent="{eid}">'
                         f'<mxGeometry x="-1" relative="1" as="geometry"><mxPoint x="{cxl - pts[0][0]:.1f}" '
                         f'y="{cyl - pts[0][1]:.1f}" as="offset"/></mxGeometry></mxCell>')

    return ('<mxfile host="drawio" type="device">\n'
            f'<diagram id="{d.name}" name="{d.name}">\n'
            f'<mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" '
            f'arrows="1" fold="1" page="1" pageScale="1" pageWidth="{d.w}" pageHeight="{d.h}" math="0" shadow="0">\n'
            '<root>\n' + "\n".join(cells) + '\n</root>\n</mxGraphModel>\n</diagram>\n</mxfile>\n')


# ---------------------------------------------------------------- диаграмма состояний
def states():
    d = Diagram("state", 1400, 1010, "Диаграмма состояний объекта «Заказ»")
    L, M, R = 230, 660, 1180
    d.dot("init", L, 100)
    d.state("Booked", L, 270, "Забронирован", [
        "entry / заблокировать места",
        "do / отсчёт 15 минут брони",
        "применён промокод / пересчитать сумму"], w=310)
    d.state("Paying", M + 40, 270, "Оплачивается", [
        "entry / отправить платёж в банк",
        "do / ждать ответа платёжной системы"], w=310)
    d.state("Paid", M + 40, 560, "Оплачен", [
        "entry / выпустить билеты с QR-кодами",
        "entry / отправить билеты на e-mail",
        "билет отсканирован / отметить вход"], w=310)
    d.state("Cancelled", L, 560, "Отменён", [
        "entry / снять блокировку мест"], w=310)
    d.state("Refunded", R, 560, "Возвращён", [
        "entry / вернуть деньги на карту",
        "entry / освободить места"], w=290)
    d.state("Done", M + 40, 790, "Завершён", [
        "entry / перенести заказ в историю"], w=310)
    d.dot("final", M + 40, 940, final=True)

    sx = d.cx
    # начальное → Забронирован
    yi = d.cy("init")
    d.edge("init", "Booked", [(sx("Booked"), d.bottom("init")), (sx("Booked"), d.nodes["Booked"]["y"])],
           "trans", [("места выбраны [свободны] /\nсоздать заказ", sx("Booked") + 12, 150, "start", "trans")])
    # Забронирован ⇄ Оплачивается
    y1 = d.cy("Booked") - 22
    y2 = d.cy("Booked") + 22
    d.edge("Booked", "Paying", [(d.nodes["Booked"]["x"] + 310, y1), (d.nodes["Paying"]["x"], y1)], "trans",
           [("нажата «Оплатить»", (d.nodes["Booked"]["x"] + 310 + d.nodes["Paying"]["x"]) / 2, y1 - 8, "middle", "trans")])
    d.edge("Paying", "Booked", [(d.nodes["Paying"]["x"], y2), (d.nodes["Booked"]["x"] + 310, y2)], "trans",
           [("платёж отклонён\n[бронь не истекла]", (d.nodes["Booked"]["x"] + 310 + d.nodes["Paying"]["x"]) / 2, y2 + 20,
             "middle", "trans")])
    # Оплачивается → Оплачен
    xb = sx("Paying") + 40
    d.edge("Paying", "Paid", [(xb, d.bottom("Paying")), (xb, d.nodes["Paid"]["y"])], "trans",
           [("платёж одобрен", xb + 10, (d.bottom("Paying") + d.nodes["Paid"]["y"]) / 2 + 4, "start", "trans")])
    # Забронирован → Отменён
    xa = sx("Booked") - 60
    d.edge("Booked", "Cancelled", [(xa, d.bottom("Booked")), (xa, d.nodes["Cancelled"]["y"])], "trans",
           [("прошло 15 минут\nили зритель отменил", xa - 10, (d.bottom("Booked") + d.nodes["Cancelled"]["y"]) / 2 - 6,
             "end", "trans")])
    # Оплачивается → Отменён
    xc = sx("Paying") - 80
    ym = (d.bottom("Paying") + d.nodes["Cancelled"]["y"]) / 2
    xd = sx("Booked") + 80
    d.edge("Paying", "Cancelled", [(xc, d.bottom("Paying")), (xc, ym), (xd, ym), (xd, d.nodes["Cancelled"]["y"])],
           "trans", [("платёж отклонён [бронь истекла]", (xc + xd) / 2, ym - 8, "middle", "trans")])
    # Оплачен → Возвращён
    yr = d.cy("Paid")
    d.edge("Paid", "Refunded", [(d.nodes["Paid"]["x"] + 310, yr), (d.nodes["Refunded"]["x"], yr)], "trans",
           [("запрос возврата\n[сеанс ещё не начался]", (d.nodes["Paid"]["x"] + 310 + d.nodes["Refunded"]["x"]) / 2,
             yr - 30, "middle", "trans")])
    # Оплачен → Завершён
    d.edge("Paid", "Done", [(sx("Paid"), d.bottom("Paid")), (sx("Paid"), d.nodes["Done"]["y"])], "trans",
           [("сеанс начался", sx("Paid") + 10, (d.bottom("Paid") + d.nodes["Done"]["y"]) / 2 + 4, "start", "trans")])
    # → конечное
    fy = d.cy("final")
    d.edge("Done", "final", [(sx("Done"), d.bottom("Done")), (sx("Done"), d.nodes["final"]["y"])], "trans")
    d.edge("Cancelled", "final", [(sx("Cancelled"), d.bottom("Cancelled")), (sx("Cancelled"), fy),
                                  (d.nodes["final"]["x"], fy)], "trans")
    d.edge("Refunded", "final", [(sx("Refunded"), d.bottom("Refunded")), (sx("Refunded"), fy),
                                 (d.nodes["final"]["x"] + d.nodes["final"]["w"], fy)], "trans")
    return d


# ---------------------------------------------------------------- диаграмма классов
def classes():
    d = Diagram("class", 1660, 1500, "Диаграмма классов системы «Киноточка»")
    C = [230, 630, 1030, 1430]
    top1 = 110
    d.cls("Role", C[0], top1, "Роль", ["ЗРИТЕЛЬ", "КАССИР", "АДМИНИСТРАТОР"], stereo="«enumeration»", w=260)
    d.cls("Status", C[1], top1, "СтатусЗаказа",
          ["ЗАБРОНИРОВАН", "ОПЛАЧИВАЕТСЯ", "ОПЛАЧЕН", "ЗАВЕРШЁН", "ОТМЕНЁН", "ВОЗВРАЩЁН"],
          stereo="«enumeration»", w=260)
    d.cls("Review", C[2], top1, "Отзыв",
          ["id: int", "оценка: int (1–5)", "текст: string", "дата: DateTime"],
          ["оставить(): bool"], w=280)
    d.cls("Film", C[3], top1, "Фильм",
          ["id: int", "название: string", "жанр: string", "длительностьМин: int", "возрастнойРейтинг: string",
           "описание: string", "активен: bool"],
          ["найти(запрос): List<Фильм>", "получитьСеансы(дата): List<Сеанс>", "рейтинг(): decimal", "скрыть(): void"])
    top2 = max(d.bottom(n) for n in ("Role", "Status", "Review", "Film")) + 90
    d.cls("User", C[0], top2, "Пользователь",
          ["id: int", "ФИО: string", "email: string", "телефон: string", "хешПароля: string", "роль: Роль"],
          ["войти(email, пароль): bool", "зарегистрироваться(): bool", "выйти(): void"])
    d.cls("Order", C[1], top2, "Заказ",
          ["id: int", "датаСоздания: DateTime", "броньДо: DateTime", "сумма: decimal", "скидка: decimal",
           "итого: decimal", "статус: СтатусЗаказа"],
          ["рассчитатьСтоимость(): decimal", "применитьПромокод(код): bool", "оплатить(): bool",
           "отменить(): void", "вернуть(): bool", "выручкаЗаПериод(с, по): decimal"])
    d.cls("Promo", C[2], top2, "Промокод",
          ["код: string", "скидкаПроцент: int", "действуетДо: DateTime", "активен: bool"],
          ["проверить(): bool", "применить(сумма): decimal"], w=280)
    d.cls("Session", C[3], top2, "Сеанс",
          ["id: int", "датаВремя: DateTime", "формат: string", "базоваяЦена: decimal"],
          ["свободныеМеста(): List<Место>", "заблокироватьМеста(места): bool", "освободитьМеста(места): void",
           "заполняемость(): decimal"])
    top3 = max(d.bottom(n) for n in ("User", "Order", "Promo", "Session")) + 90
    d.cls("Payment", C[0], top3, "Платёж",
          ["id: int", "сумма: decimal", "тип: string (оплата / возврат)", "транзакцияБанка: string",
           "успешен: bool", "дата: DateTime"],
          ["провести(): bool", "вернутьДеньги(): bool"])
    d.cls("Ticket", C[1], top3, "Билет",
          ["id: int", "цена: decimal", "qrКод: string", "использован: bool"],
          ["сгенерироватьQR(): string", "проверитьНаВходе(): bool"])
    d.cls("Seat", C[2], top3, "Место",
          ["id: int", "ряд: int", "номер: int", "тип: string", "коэффициентЦены: decimal"],
          ["цена(сеанс): decimal"], w=280)
    d.cls("Hall", C[3], top3, "Зал",
          ["id: int", "номер: int", "рядов: int", "местВРяду: int"],
          ["схемаМест(): List<Место>"])

    n = d.nodes
    M, NM = "mult", "name"

    def right(nid):
        return n[nid]["x"] + n[nid]["w"]

    # Пользователь ..> Роль, Заказ ..> СтатусЗаказа
    for a, b in (("User", "Role"), ("Order", "Status")):
        x = d.cx(a)
        d.edge(a, b, [(x, n[a]["y"]), (x, d.bottom(b))], "dep",
               [("«use»", x + 8, (n[a]["y"] + d.bottom(b)) / 2 + 4, "start", NM)])
    # Пользователь 1 — 0..* Заказ
    y = n["User"]["y"] + 70
    d.edge("User", "Order", [(right("User"), y), (n["Order"]["x"], y)], "assoc",
           [("1", right("User") + 8, y - 8, "start", M), ("0..*", n["Order"]["x"] - 8, y - 8, "end", M),
            ("оформляет", (right("User") + n["Order"]["x"]) / 2, y + 18, "middle", NM)])
    # Заказ 0..* — 0..1 Промокод
    y = n["Promo"]["y"] + 60
    d.edge("Order", "Promo", [(right("Order"), y), (n["Promo"]["x"], y)], "assoc",
           [("0..*", right("Order") + 8, y - 8, "start", M), ("0..1", n["Promo"]["x"] - 8, y - 8, "end", M),
            ("применён", (right("Order") + n["Promo"]["x"]) / 2, y + 18, "middle", NM)])
    # Заказ 0..* — 1 Сеанс (под промокодом)
    y = (d.bottom("Promo") + d.bottom("Session")) / 2
    d.edge("Order", "Session", [(right("Order"), y), (n["Session"]["x"], y)], "assoc",
           [("0..*", right("Order") + 8, y - 8, "start", M), ("1", n["Session"]["x"] - 8, y - 8, "end", M),
            ("на сеанс", (right("Promo") + n["Session"]["x"]) / 2, y + 18, "middle", NM)])
    # Сеанс 0..* — 1 Фильм
    x = d.cx("Session")
    d.edge("Session", "Film", [(x, n["Session"]["y"]), (x, d.bottom("Film"))], "assoc",
           [("0..*", x + 8, n["Session"]["y"] - 10, "start", M), ("1", x + 8, d.bottom("Film") + 20, "start", M),
            ("показывает", x - 8, (n["Session"]["y"] + d.bottom("Film")) / 2 + 4, "end", NM)])
    # Сеанс 0..* — 1 Зал
    d.edge("Session", "Hall", [(x, d.bottom("Session")), (x, n["Hall"]["y"])], "assoc",
           [("0..*", x + 8, d.bottom("Session") + 20, "start", M), ("1", x + 8, n["Hall"]["y"] - 10, "start", M),
            ("проходит в", x - 8, (d.bottom("Session") + n["Hall"]["y"]) / 2 + 4, "end", NM)])
    # Зал ◆ 1 — 1..* Место
    y = n["Hall"]["y"] + 60
    d.edge("Hall", "Seat", [(n["Hall"]["x"], y), (right("Seat"), y)], "comp",
           [("1", n["Hall"]["x"] - 26, y - 10, "end", M), ("1..*", right("Seat") + 8, y - 8, "start", M)])
    # Билет 0..* — 1 Место
    y = n["Ticket"]["y"] + 60
    d.edge("Ticket", "Seat", [(right("Ticket"), y), (n["Seat"]["x"], y)], "assoc",
           [("0..*", right("Ticket") + 8, y - 8, "start", M), ("1", n["Seat"]["x"] - 8, y - 8, "end", M),
            ("на место", (right("Ticket") + n["Seat"]["x"]) / 2, y + 18, "middle", NM)])
    # Заказ ◆ 1 — 1..* Билет
    x = d.cx("Order")
    d.edge("Order", "Ticket", [(x, d.bottom("Order")), (x, n["Ticket"]["y"])], "comp",
           [("1", x + 12, d.bottom("Order") + 34, "start", M), ("1..*", x + 8, n["Ticket"]["y"] - 10, "start", M),
            ("содержит", x - 10, (d.bottom("Order") + n["Ticket"]["y"]) / 2 + 4, "end", NM)])
    # Заказ 1 — 0..* Платёж
    xo = n["Order"]["x"] + 40
    ym = (d.bottom("Order") + n["Ticket"]["y"]) / 2 + 12
    xp = d.cx("Payment")
    d.edge("Order", "Payment", [(xo, d.bottom("Order")), (xo, ym), (xp, ym), (xp, n["Payment"]["y"])], "assoc",
           [("1", xo + 8, d.bottom("Order") + 20, "start", M), ("0..*", xp + 8, n["Payment"]["y"] - 10, "start", M),
            ("оплачивается", (xo + xp) / 2, ym - 8, "middle", NM)])
    # Отзыв — Фильм, Отзыв — Пользователь
    y = n["Review"]["y"] + 60
    d.edge("Review", "Film", [(right("Review"), y), (n["Film"]["x"], y)], "assoc",
           [("0..*", right("Review") + 8, y - 8, "start", M), ("1", n["Film"]["x"] - 8, y - 8, "end", M),
            ("о фильме", (right("Review") + n["Film"]["x"]) / 2, y + 18, "middle", NM)])
    xr = n["Review"]["x"] + 50
    xl = 40
    yt = n["Review"]["y"] - 18
    yu = n["User"]["y"] + 40
    d.edge("Review", "User", [(xr, n["Review"]["y"]), (xr, yt), (xl, yt), (xl, yu), (n["User"]["x"], yu)], "assoc",
           [("0..*", xr + 8, n["Review"]["y"] - 26, "start", M), ("1", n["User"]["x"] - 8, yu - 8, "end", M),
            ("пишет", (xr + xl) / 2 + 200, yt - 6, "middle", NM)])
    d.h = int(max(d.bottom(k) for k in d.nodes) + 50)
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
    build(states())
    build(classes())
