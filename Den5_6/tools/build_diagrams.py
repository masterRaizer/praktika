"""Диаграммы дней 5–6 приложения «Киноточка»: структура решения, навигация между формами, алгоритм входа.

Каждая диаграмма сохраняется как .drawio (редактируемый исходник для app.diagrams.net), .svg и .png.
Запуск:  python tools/build_diagrams.py   (нужен пакет cairosvg)
"""
import os
from xml.sax.saxutils import escape

import cairosvg

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "diagrams")
FONT = "DejaVu Sans"
INK, MUTED, LINE = "#1F2937", "#475569", "#475569"

COLORS = {  # заливка, рамка
    "ui": ("#DCE9FA", "#2B4C7E"),
    "logic": ("#DDF1E3", "#2F7A47"),
    "data": ("#FDEBD3", "#B45309"),
    "db": ("#FBE0E6", "#B4234A"),
    "lib": ("#EEE9FB", "#5B47E0"),
    "test": ("#F1F5F9", "#475569"),
    "start": ("#1F2937", "#1F2937"),
    "decision": ("#FFF7D6", "#B67600"),
}


def tw(s, fs):
    return len(s) * fs * 0.58


class Diagram:
    def __init__(self, name, title, width, height):
        self.name, self.title, self.w, self.h = name, title, width, height
        self.nodes, self.edges, self.groups = {}, [], []

    def node(self, nid, x, y, w, h, title, lines=(), kind="ui", shape="rect"):
        self.nodes[nid] = dict(x=x, y=y, w=w, h=h, title=title, lines=list(lines), kind=kind, shape=shape)

    def edge(self, a, b, label="", pts=None, dashed=False, sa=None, sb=None):
        self.edges.append(dict(a=a, b=b, label=label, pts=pts or [], dashed=dashed, sa=sa, sb=sb))

    def group(self, x, y, w, h, title):
        self.groups.append(dict(x=x, y=y, w=w, h=h, title=title))

    # ---------- геометрия ----------
    def anchor(self, nid, side):
        n = self.nodes[nid]
        cx, cy = n["x"] + n["w"] / 2, n["y"] + n["h"] / 2
        return {"t": (cx, n["y"]), "b": (cx, n["y"] + n["h"]), "l": (n["x"], cy), "r": (n["x"] + n["w"], cy)}[side]

    def auto_side(self, a, b):
        na, nb = self.nodes[a], self.nodes[b]
        ax, ay = na["x"] + na["w"] / 2, na["y"] + na["h"] / 2
        bx, by = nb["x"] + nb["w"] / 2, nb["y"] + nb["h"] / 2
        if abs(by - ay) * 1.4 > abs(bx - ax):
            return ("b", "t") if by > ay else ("t", "b")
        return ("r", "l") if bx > ax else ("l", "r")

    def path(self, e):
        sa, sb = self.auto_side(e["a"], e["b"])
        p0 = e["sa"] if isinstance(e["sa"], tuple) else self.anchor(e["a"], e["sa"] or sa)
        p1 = e["sb"] if isinstance(e["sb"], tuple) else self.anchor(e["b"], e["sb"] or sb)
        return [p0] + e["pts"] + [p1]

    # ---------- SVG ----------
    def svg(self):
        o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}" font-family="{FONT}">',
             '<defs><marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" orient="auto-start-reverse">'
             f'<path d="M0,0 L10,5 L0,10 z" fill="{LINE}"/></marker></defs>',
             f'<rect width="{self.w}" height="{self.h}" fill="#FFFFFF"/>',
             f'<text x="30" y="42" font-size="22" font-weight="bold" fill="{INK}">{escape(self.title)}</text>']
        for g in self.groups:
            o.append(f'<rect x="{g["x"]}" y="{g["y"]}" width="{g["w"]}" height="{g["h"]}" rx="14" fill="#F8FAFC" stroke="#94A3B8" stroke-dasharray="6 5"/>')
            o.append(f'<text x="{g["x"] + 14}" y="{g["y"] + 22}" font-size="13" font-weight="bold" fill="{MUTED}">{escape(g["title"])}</text>')
        for e in self.edges:
            pts = self.path(e)
            d = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
            dash = ' stroke-dasharray="6 4"' if e["dashed"] else ""
            o.append(f'<polyline points="{d}" fill="none" stroke="{LINE}" stroke-width="1.6"{dash} marker-end="url(#arr)"/>')
            if e["label"]:
                (x0, y0), (x1, y1) = pts[len(pts) // 2 - 1], pts[len(pts) // 2]
                mx, my = (x0 + x1) / 2, (y0 + y1) / 2
                for i, line in enumerate(e["label"].split("\n")):
                    w = tw(line, 11.5) + 8
                    yy = my - 8 + i * 15
                    o.append(f'<rect x="{mx - w / 2:.1f}" y="{yy - 11:.1f}" width="{w:.1f}" height="15" fill="#FFFFFF" opacity="0.92"/>')
                    o.append(f'<text x="{mx:.1f}" y="{yy:.1f}" font-size="11.5" text-anchor="middle" fill="{MUTED}">{escape(line)}</text>')
        for n in self.nodes.values():
            fill, stroke = COLORS[n["kind"]]
            x, y, w, h = n["x"], n["y"], n["w"], n["h"]
            if n["shape"] == "ellipse":
                o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{h / 2}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
                color = "#FFFFFF" if n["kind"] == "start" else INK
                o.append(f'<text x="{x + w / 2}" y="{y + h / 2 + 5}" font-size="13.5" font-weight="bold" text-anchor="middle" fill="{color}">{escape(n["title"])}</text>')
                continue
            if n["shape"] == "diamond":
                cx, cy = x + w / 2, y + h / 2
                o.append(f'<polygon points="{cx},{y} {x + w},{cy} {cx},{y + h} {x},{cy}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
                lines = n["title"].split("\n")
                for i, line in enumerate(lines):
                    o.append(f'<text x="{cx}" y="{cy + 5 + (i - (len(lines) - 1) / 2) * 16}" font-size="13" text-anchor="middle" fill="{INK}">{escape(line)}</text>')
                continue
            o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="#FFFFFF" stroke="{stroke}" stroke-width="1.6"/>')
            head = 30 if n["lines"] else h
            o.append(f'<path d="M{x},{y + head} L{x},{y + 10} Q{x},{y} {x + 10},{y} L{x + w - 10},{y} Q{x + w},{y} {x + w},{y + 10} L{x + w},{y + head} '
                     + (f'L{x},{y + head} Z' if n["lines"] else f'L{x + w},{y + h - 10} Q{x + w},{y + h} {x + w - 10},{y + h} L{x + 10},{y + h} Q{x},{y + h} {x},{y + h - 10} Z')
                     + f'" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
            title_lines = n["title"].split("\n")
            for i, line in enumerate(title_lines):
                ty = y + 20 + i * 16 if n["lines"] else y + h / 2 + 5 + (i - (len(title_lines) - 1) / 2) * 16
                o.append(f'<text x="{x + w / 2}" y="{ty}" font-size="13.5" font-weight="bold" text-anchor="middle" fill="{INK}">{escape(line)}</text>')
            for i, line in enumerate(n["lines"]):
                o.append(f'<text x="{x + 12}" y="{y + head + 19 + i * 17}" font-size="12" fill="{INK}">{escape(line)}</text>')
        o.append("</svg>")
        return "\n".join(o)

    # ---------- draw.io ----------
    def drawio(self):
        cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>']
        k = 2
        for g in self.groups:
            cells.append(f'<mxCell id="g{k}" value="{escape(g["title"])}" style="rounded=1;dashed=1;fillColor=#F8FAFC;strokeColor=#94A3B8;verticalAlign=top;align=left;spacingLeft=10;fontStyle=1;fontColor=#475569;" vertex="1" parent="1">'
                         f'<mxGeometry x="{g["x"]}" y="{g["y"]}" width="{g["w"]}" height="{g["h"]}" as="geometry"/></mxCell>')
            k += 1
        ids = {}
        for nid, n in self.nodes.items():
            fill, stroke = COLORS[n["kind"]]
            if n["lines"]:
                value = "<b>" + escape(n["title"]).replace("\n", "<br>") + "</b><hr>" + "<br>".join(escape(l) for l in n["lines"])
                style = f"rounded=1;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacingLeft=8;fillColor=#FFFFFF;strokeColor={stroke};"
            else:
                value = escape(n["title"]).replace("\n", "<br>")
                shape = "rhombus;" if n["shape"] == "diamond" else ("rounded=1;arcSize=50;" if n["shape"] == "ellipse" else "rounded=1;")
                font = "fontColor=#FFFFFF;" if n["kind"] == "start" else ""
                style = f"{shape}whiteSpace=wrap;html=1;fontStyle=1;fillColor={fill};strokeColor={stroke};{font}"
            cid = f"n{k}"
            ids[nid] = cid
            cells.append(f'<mxCell id="{cid}" value="{escape(value, {chr(34): "&quot;"})}" style="{style}" vertex="1" parent="1">'
                         f'<mxGeometry x="{n["x"]}" y="{n["y"]}" width="{n["w"]}" height="{n["h"]}" as="geometry"/></mxCell>')
            k += 1
        for e in self.edges:
            dash = "dashed=1;" if e["dashed"] else ""
            pts = "".join(f'<mxPoint x="{x}" y="{y}"/>' for x, y in e["pts"])
            cells.append(f'<mxCell id="e{k}" value="{escape(e["label"]).replace(chr(10), "&#xa;")}" style="edgeStyle=none;html=1;endArrow=block;fontSize=11;{dash}" edge="1" parent="1" source="{ids[e["a"]]}" target="{ids[e["b"]]}">'
                         f'<mxGeometry relative="1" as="geometry">{"<Array as=" + chr(34) + "points" + chr(34) + ">" + pts + "</Array>" if pts else ""}</mxGeometry></mxCell>')
            k += 1
        return (f'<mxfile host="app.diagrams.net"><diagram name="{escape(self.title)}"><mxGraphModel dx="1200" dy="800" grid="1" gridSize="10" '
                f'pageWidth="{self.w}" pageHeight="{self.h}"><root>' + "".join(cells) + "</root></mxGraphModel></diagram></mxfile>")

    def save(self):
        os.makedirs(OUT, exist_ok=True)
        base = os.path.join(OUT, self.name)
        svg = self.svg()
        open(base + ".svg", "w", encoding="utf-8").write(svg)
        open(base + ".drawio", "w", encoding="utf-8").write(self.drawio())
        cairosvg.svg2png(bytestring=svg.encode("utf-8"), write_to=base + ".png", output_width=self.w * 2)
        print("готово:", self.name)


def architecture():
    d = Diagram("architecture", "Структура решения Kinotochka.sln: интерфейс, бизнес-логика, работа с данными", 1500, 900)
    d.group(20, 70, 1090, 300, "Проект Kinotochka (Windows Forms, .NET Framework 4.7.2)")
    d.node("forms", 40, 105, 330, 250, "Интерфейс — папка Forms", [
        "LoginForm — авторизация", "RegisterForm — регистрация", "ConnectionForm — подключение",
        "MainForm — главное окно и разделы", "FilmEditForm — фильм", "ScreeningEditForm — сеанс",
        "UserEditForm — пользователь", "OrderStatusForm — статус заказа", "FormKit — показ ошибок, проверка полей"], "ui")
    d.node("logic", 400, 105, 330, 250, "Бизнес-логика — папка Logic", [
        "AuthService — вход, регистрация,", "   3 попытки и пауза 30 с",
        "FilmService, ScreeningService,", "   OrderService, UserService",
        "Roles — права ролей", "Validator — проверка ввода",
        "PasswordHasher — PBKDF2-SHA256", "ErrorTranslator, ErrorLog — исключения"], "logic")
    d.node("data", 760, 105, 330, 250, "Работа с данными — папка Data", [
        "Db — строка подключения, открытие", "   и закрытие соединения (using),",
        "   параметры SqlParameter", "LookupRepository — справочники",
        "UserRepository, FilmRepository", "ScreeningRepository, OrderRepository",
        "DashboardRepository — показатели", "Models — классы записей"], "data")
    d.node("fx", 40, 420, 330, 150, "Библиотека FxKit — папка Fx", [
        "окно без рамки, боковая панель, темы", "FxTable — таблица с выделением строки",
        "FxDialog — формы ввода и сообщения", "FxTextBox, FxSelect, FxButton, FxSwitch"], "lib")
    d.node("db", 1180, 105, 290, 250, "MS SQL Server: база «Киноточка»", [
        "14 таблиц (день 4): Пользователи,", "Роли, Фильмы, Жанры, Сеансы,",
        "Залы, Места, ТипыМест, Заказы,", "Билеты, Платежи, Промокоды,",
        "СтатусыЗаказа, Отзывы", "триггер ТР_Билеты_ПроверкаМеста",
        "представление СуммыЗаказов"], "db")
    d.node("cfg", 760, 420, 330, 150, "Настройки подключения", [
        "Kinotochka.exe.config — строка", "   подключения (LocalDB по умолчанию)",
        "%LOCALAPPDATA%\\Kinotochka\\server.txt", "   — сервер из формы «Подключение»",
        "%LOCALAPPDATA%\\Kinotochka\\errors.log"], "data")
    d.node("tests", 400, 650, 330, 150, "Проект Kinotochka.Tests", [
        "58 проверок: вход, регистрация, ввод,", "добавление, изменение, удаление,",
        "поиск, права ролей, ограничения базы,", "недоступность сервера", "→ Результаты_тестирования.md / .csv"], "test")
    d.edge("forms", "logic", "вызывает")
    d.edge("logic", "data", "вызывает")
    d.edge("data", "db", "ADO.NET\nSqlClient")
    d.edge("forms", "fx", "оформление")
    d.edge("data", "cfg", "читает", dashed=True)
    d.edge("tests", "logic", "проверяет", sa="t", sb="b")
    d.save()


def navigation():
    d = Diagram("navigation", "Навигация между формами приложения «Киноточка»", 1580, 960)
    d.node("start", 170, 90, 180, 46, "Запуск программы", kind="start", shape="ellipse")
    d.node("login", 140, 200, 240, 120, "Форма 1. Вход", ["эл. почта и пароль,", "проверка подключения", "к базе данных"], "ui")
    d.node("reg", 140, 430, 240, 100, "Форма 2. Регистрация", ["новый зритель,", "пароль → хеш"], "ui")
    d.node("conn", 140, 620, 240, 100, "Форма 3. Подключение", ["имя сервера SQL Server,", "кнопка «Проверить»"], "ui")
    d.node("end", 170, 840, 180, 46, "Выход из программы", kind="start", shape="ellipse")
    d.node("main", 540, 180, 310, 220, "Форма 4. Главное окно", ["боковая панель разделов:", "• Главная — показатели, афиша",
                                                            "• Фильмы", "• Сеансы", "• Заказы / Мои заказы", "• Пользователи (администратор)"], "ui")
    d.node("msg", 540, 600, 310, 110, "Сообщение об ошибке", ["понятный текст на русском,", "«Подробнее» — номер ошибки", "без паролей и строки подключения"], "db")
    d.node("film", 990, 90, 260, 80, "Форма 5. Фильм", ["добавление и изменение"], "ui")
    d.node("scr", 990, 200, 260, 80, "Форма 6. Сеанс", ["добавление и изменение"], "ui")
    d.node("ord", 990, 310, 260, 80, "Форма 8. Статус заказа", ["кассир, администратор"], "ui")
    d.node("usr", 990, 420, 260, 80, "Форма 7. Пользователь", ["только администратор"], "ui")
    d.node("conf", 990, 600, 260, 110, "Подтверждение", ["удаление записи,", "блокировка, выход;", "запрет при связанных данных"], "logic")
    d.node("roles", 1290, 90, 270, 300, "Права ролей", [
        "Зритель:", "  Главная, Фильмы и Сеансы —", "  просмотр, Мои заказы",
        "Кассир:", "  + все заказы, смена статуса",
        "Администратор:", "  + добавление, изменение", "  и удаление фильмов,", "  сеансов, заказов;", "  раздел Пользователи"], "logic")
    d.edge("start", "login")
    d.edge("login", "reg", "«Регистрация»", sa=(200, 320), sb=(200, 430))
    d.edge("reg", "login", "создан\nаккаунт", sa=(320, 430), sb=(320, 320))
    d.edge("login", "conn", "«Подключение»", sa=(140, 290), pts=[(100, 290), (100, 670)], sb=(140, 670))
    d.edge("login", "end", "закрыть\nокно", sa=(140, 230), pts=[(40, 230), (40, 863)], sb=(170, 863), dashed=True)
    d.edge("login", "main", "вход выполнен", sa=(380, 230), sb=(540, 230))
    d.edge("main", "login", "«Выйти»", sa=(540, 275), sb=(380, 275))
    d.edge("login", "msg", "нет подключения", sa=(380, 305), pts=[(470, 305), (470, 655)], sb=(540, 655), dashed=True)
    d.edge("main", "msg", "ошибка базы", dashed=True)
    d.edge("main", "film", "Добавить / Изменить", sa=(850, 220), sb=(990, 130))
    d.edge("main", "scr", sa=(850, 240), sb=(990, 240))
    d.edge("main", "ord", "Сменить статус", sa=(850, 300), sb=(990, 350))
    d.edge("main", "usr", sa=(850, 340), sb=(990, 460))
    d.edge("main", "conf", "Удалить", sa=(850, 380), pts=[(920, 380), (920, 655)], sb=(990, 655))
    d.save()


def auth():
    d = Diagram("auth", "Алгоритм авторизации (AuthService.Login)", 1100, 1080)
    cx = 330
    d.node("s", cx - 90, 70, 180, 46, "Начало", kind="start", shape="ellipse")
    d.node("in", cx - 150, 150, 300, 60, "Ввод эл. почты и пароля", kind="ui")
    d.node("lock", cx - 130, 245, 260, 90, "Вход\nприостановлен?", kind="decision", shape="diamond")
    d.node("val", cx - 130, 370, 260, 90, "Поля заполнены\nи почта верна?", kind="decision", shape="diamond")
    d.node("q", cx - 170, 495, 340, 60, "SELECT … WHERE ЭлПочта = @ЭлПочта", kind="data")
    d.node("ok", cx - 130, 590, 260, 90, "Пользователь найден\nи хеш совпал?", kind="decision", shape="diamond")
    d.node("bl", cx - 130, 715, 260, 90, "Учётная запись\nзаблокирована?", kind="decision", shape="diamond")
    d.node("main", cx - 150, 840, 300, 60, "Открыть главное окно по роли", kind="logic")
    d.node("e", cx - 90, 950, 180, 46, "Конец", kind="start", shape="ellipse")
    d.node("m1", 640, 260, 400, 60, "«Повторите вход через N с»", kind="db")
    d.node("m2", 640, 385, 400, 60, "«Заполните поле…» / «Неверный формат…»", kind="db")
    d.node("cnt", 640, 590, 400, 90, "Счётчик попыток + 1", ["3 неудачи подряд → пауза 30 секунд;", "текст одинаковый для неизвестной", "почты и неверного пароля"], "logic")
    d.node("m4", 640, 730, 400, 60, "«Учётная запись заблокирована»", kind="db")
    d.edge("s", "in")
    d.edge("in", "lock")
    d.edge("lock", "val", "нет")
    d.edge("lock", "m1", "да")
    d.edge("val", "q", "да")
    d.edge("val", "m2", "нет")
    d.edge("q", "ok")
    d.edge("ok", "bl", "да")
    d.edge("ok", "cnt", "нет")
    d.edge("bl", "main", "нет")
    d.edge("bl", "m4", "да")
    d.edge("main", "e")
    d.edge("m1", "in", "повторить", pts=[(1070, 290), (1070, 180)], sa="r", sb="r", dashed=True)
    d.save()


if __name__ == "__main__":
    architecture()
    navigation()
    auth()
