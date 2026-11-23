"""Диаграммы дней 5–7 (приложение Den5_7 «Киноточка»).

  * den5_idef0_a0      — функциональная диаграмма IDEF0 A0 приложения: 5 подпроцессов, стрелки ICOM;
  * den5_screen_map    — карта экранов и переходов (включая сообщения и подтверждения);
  * den6_deployment    — схема развёртывания: компьютер пользователя, SQL Server, модули;
  * den7_integration   — интеграция модулей «Клиенты» и «Заявки» через REST API;
  * den7_sequence      — последовательность «создать заявку» с проверкой клиента.

Отрисовка — генератором дня 3 (UML_Den3/tools/build_diagrams.py): .drawio (редактируемый), .svg, .png, .pdf.
Запуск:  python tools/build_diagrams57.py   (нужен пакет cairosvg)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "UML_Den3", "tools"))
import build_diagrams as g  # noqa: E402

g.OUT = os.path.join(HERE, "..", "docs", "diagrams")
Diagram = g.Diagram
F, S = g.SUB_FILLS, g.SUB_STROKES
MUTED = g.MUTED

MSG_FILL, MSG_STROKE = "#FDE8EC", "#B4234A"      # сообщения об ошибках
ASK_FILL, ASK_STROKE = "#FFF4E0", "#B45309"      # подтверждения
GROUP_FILL = "#F8FAFC"
DAY6_FILL, DAY6_STROKE = "#E0F2F1", "#0F766E"   # экраны дня 6
DLG_FILL, DLG_STROKE = "#FFFFFF", "#64748B"      # стандартные диалоги


# ---------------------------------------------------------------- день 5: IDEF0 A0 приложения
def den5_idef0_a0():
    d = Diagram("den5_idef0_a0", 1900, 1080, "IDEF0, A0: функции приложения «Киноточка» (день 5)")
    W_, H_ = 240, 110
    blocks = [("A1", "Войти в систему\nи определить роль"), ("A2", "Вести справочник\nфильмов"),
              ("A3", "Искать, фильтровать\nи сортировать"), ("A4", "Формировать отчёты\nи экспорт в CSV"),
              ("A5", "Вести журнал\nдействий")]
    pos = {}
    for i, (code, name) in enumerate(blocks):
        x, y = 320 + i * 270, 170 + i * 170
        pos[code] = (x, y)
        d.box(code, x, y, W_, H_, name, fill=F[i], stroke=S[i], fs=14, bold=True, rx=0, code=code)

    def L(c, t):
        return (pos[c][0], pos[c][1] + H_ * t)

    def R(c, t):
        return (pos[c][0] + W_, pos[c][1] + H_ * t)

    def T(c, dx):
        return (pos[c][0] + dx, pos[c][1])

    def B(c, dx):
        return (pos[c][0] + dx, pos[c][1] + H_)

    # I — входы с границы
    for c, t, s in [("A1", 0.5, "e-mail и пароль"),
                    ("A2", 0.5, "данные фильма\nиз карточки"),
                    ("A3", 0.5, "строка поиска, жанр,\nпорядок сортировки"),
                    ("A4", 0.72, "заказы, билеты, платежи;\nперечень для экспорта")]:
        p = L(c, t)
        d.edge(None, c, [(40, p[1]), p], labels=[(s, 50, p[1] - (22 if "\n" in s else 7), "start")])
    # C — управление с границы
    x = T("A1", 120)[0]
    d.edge(None, "A1", [(x, 90), T("A1", 120)],
           labels=[("ролевая модель; пароли хранятся\nтолько в виде хеша PBKDF2", x + 8, 104, "start")])
    x = T("A2", 190)[0]
    d.edge(None, "A2", [(x, 90), T("A2", 190)],
           labels=[("ограничения базы:\nNOT NULL, UNIQUE, CHECK, FK", x + 8, 104, "start")])
    x = T("A3", 190)[0]
    d.edge(None, "A3", [(x, 90), T("A3", 190)],
           labels=[("ввод передаётся\nпараметрами SqlParameter", x + 8, 104, "start")])
    x = T("A5", 200)[0]
    d.edge(None, "A5", [(x, 90), T("A5", 200)],
           labels=[("просмотр журнала —\nтолько администратор", x + 8, 104, "start")])
    # A1 → роль и права → управление A2 и A4
    y1 = L("A1", 0.5)[1]
    d.edge("A1", "A2", [R("A1", 0.5), (T("A2", 60)[0], y1), T("A2", 60)],
           labels=[("роль и права доступа", R("A1", 0.5)[0] + 12, y1 - 8, "start")])
    d.edge("A1", "A4", [R("A1", 0.5), (T("A4", 60)[0], y1), T("A4", 60)])
    # A2 → каталог → управление A3
    y2 = R("A2", 0.5)[1]
    d.edge("A2", "A3", [R("A2", 0.5), (T("A3", 60)[0], y2), T("A3", 60)],
           labels=[("каталог фильмов\nи жанров", R("A2", 0.5)[0] + 12, y2 - 22, "start")])
    # A3 → отобранный список → вход A4
    xg = R("A3", 0)[0] + 22
    d.edge("A3", "A4", [R("A3", 0.5), (xg, R("A3", 0.5)[1]), (xg, L("A4", 0.3)[1]), L("A4", 0.3)],
           labels=[("отобранный\nсписок", xg - 6, R("A3", 0.5)[1] + 70, "end")])
    # A4 → сведения о действиях → вход A5
    xg = R("A4", 0)[0] + 22
    d.edge("A4", "A5", [R("A4", 0.75), (xg, R("A4", 0.75)[1]), (xg, L("A5", 0.3)[1]), L("A5", 0.3)],
           labels=[("сведения о действиях\n(вход, изменения, экспорт)", xg + 8, R("A4", 0.75)[1] + 34, "start")])
    # O — выходы на границу
    for c, t, s in [("A3", 0.25, "список фильмов, афиша"),
                    ("A4", 0.3, "отчёты о выручке и\nзаполняемости, файлы CSV"),
                    ("A5", 0.5, "журнал действий")]:
        p = R(c, t)
        lx = 1630 if c != "A5" else p[0] + 40   # подписи правее линии управления A5
        d.edge(c, None, [p, (1860, p[1])], labels=[(s, lx, p[1] - (22 if "\n" in s else 7), "start")])
    # M — механизмы
    for c, s in [("A1", "окно входа,\nПользователи, Роли"), ("A2", "администратор,\nтаблица Фильмы"),
                 ("A3", "приложение,\nпредставление Афиша"), ("A4", "кассир, администратор,\nВыручкаПоФильмам"),
                 ("A5", "таблица\nЖурналДействий")]:
        p = B(c, 120)
        d.edge(None, c, [(p[0], p[1] + 50), p], labels=[(s, p[0], p[1] + 66, "middle")])
    d.text(40, 1050, "Стрелки: слева — входы (I), сверху — управление (C), справа — выходы (O), "
                     "снизу — механизмы (M). Права из A1 управляют A2 и A4.", 12, color=MUTED)
    return d


# ---------------------------------------------------------------- день 5: карта экранов
def den5_screen_map():
    d = Diagram("den5_screen_map", 1900, 1110, "Карта экранов и переходов приложения «Киноточка» (дни 5–7)")
    d.box("hdr", 40, 70, 1820, 56,
          "Главное окно: боковая панель «День 5 · День 6 · День 7», кнопка «☰» (три полоски) сворачивает панель; "
          "разделы дня — вкладки сверху",
          fill="#1E3A5F", stroke="#1E3A5F", color="#FFFFFF", fs=14, bold=True, rx=10)
    # контейнеры дней
    # рамки дней без заливки, чтобы стрелки переходов оставались видны
    d.box("g5", 40, 170, 900, 900, "", fill="none", stroke="#CBD5E1", rx=12)
    d.box("g6", 970, 170, 420, 900, "", fill="none", stroke="#CBD5E1", rx=12)
    d.box("g7", 1420, 170, 440, 900, "", fill="none", stroke="#CBD5E1", rx=12)
    d.text(60, 194, "День 5 — интерфейс и база данных", 14, bold=True)
    d.text(990, 194, "День 6 — функции и тестирование", 14, bold=True)
    d.text(1440, 194, "День 7 — интеграция REST API", 14, bold=True)
    for gx in (490, 1180, 1640):
        d.edge("hdr", None, [(gx, 126), (gx, 170)])

    bw, bh = 220, 64

    def scr(nid, x, y, text, sub=None, i=0):
        fill, stroke = (DAY6_FILL, DAY6_STROKE) if i == 6 else (F[i], S[i])
        d.box(nid, x, y, bw, bh, text, fill=fill, stroke=stroke, fs=13, bold=True, sub=sub)

    def msg(nid, x, y, text, ask=False, dlg=False):
        fill, stroke = (DLG_FILL, DLG_STROKE) if dlg else (ASK_FILL, ASK_STROKE) if ask else (MSG_FILL, MSG_STROKE)
        d.box(nid, x, y, bw, bh, text, fill=fill, stroke=stroke, fs=11.5, rx=14)

    # ---- день 5
    scr("login", 80, 250, "Вход", "e-mail и пароль")
    scr("home", 390, 250, "Главная", "сводка и афиша")
    msg("ask_exit", 690, 250, "Подтверждение:\n«Выйти из системы?»", ask=True)
    msg("err_login", 80, 390, "Сообщение:\n«Неверный e-mail или пароль»")
    scr("films", 390, 460, "Фильмы (справочник)", "поиск, фильтр, сортировка", 1)
    msg("ask_del", 690, 460, "Подтверждение:\n«Удалить фильм?»", ask=True)
    scr("card", 390, 680, "Карточка фильма", "добавление и изменение", 1)
    msg("err_check", 690, 620, "Сообщение: ошибки\nпроверки данных")
    msg("ask_cancel", 690, 740, "Подтверждение:\n«Отменить изменения?»", ask=True)
    scr("sql", 80, 560, "SQL-запросы", "13 запросов, параметры", 0)
    scr("dict", 80, 680, "Словарь данных", "из MS_Description", 0)
    scr("model", 80, 800, "Модель", "IDEF0 и карта экранов", 0)

    d.edge("login", "home", [(300, 272), (390, 272)], labels=[("вход\nуспешен", 345, 244, "middle")])
    d.edge("login", "err_login", [(150, 314), (150, 390)], labels=[("ошибка", 158, 356, "start")])
    d.edge("err_login", "login", [(230, 390), (230, 314)], labels=[("ОК", 238, 356, "start")])
    d.edge("home", "ask_exit", [(610, 282), (690, 282)], labels=[("«Выйти»", 650, 272, "middle")])
    d.edge("ask_exit", "login", [(800, 250), (800, 216), (190, 216), (190, 250)], labels=[("«Да»", 495, 212, "middle")])
    d.edge("home", "films", [(500, 314), (500, 460)], labels=[("«Фильмы»", 508, 390, "start")])
    d.edge("films", "ask_del", [(610, 492), (690, 492)], labels=[("«Удалить»", 650, 482, "middle")])
    d.edge("films", "card", [(470, 524), (470, 680)], labels=[("«Добавить»,\n«Изменить»", 462, 590, "end")])
    d.edge("card", "films", [(540, 680), (540, 524)], labels=[("«Сохранить»,\n«Отмена»", 548, 590, "start")])
    d.edge("card", "err_check", [(610, 700), (650, 700), (650, 652), (690, 652)])
    d.edge("card", "ask_cancel", [(610, 724), (650, 724), (650, 772), (690, 772)])
    for nid, y in (("sql", 592), ("dict", 712), ("model", 832)):
        d.edge("home", nid, [(390, 300), (350, 300), (350, y), (300, y)])
    d.text(80, 950, "Сообщения об ошибках — розовые, подтверждения опасных действий — жёлтые,\nстандартные диалоги Windows — белые. "
                    "Кассир и зритель не видят кнопок изменения фильмов.\n"
                    "Сообщения не содержат технических подробностей и паролей.", 12, color=MUTED)

    # ---- день 6
    x6 = 1050
    scr("rep", x6, 250, "Отчёты", "администратор, кассир", 6)
    scr("jour", x6, 380, "Журнал действий", "только администратор", 6)
    scr("tests", x6, 510, "Тест-кейсы", "21 проверка", 6)
    scr("docs", x6, 640, "Документация", "инструкции и сценарий", 6)
    msg("save", x6, 800, "Диалог «Сохранить как»\n→ «Файл сохранён»", dlg=True)
    msg("locked", x6, 930, "Нет прав: раздел скрыт,\nпоказано пояснение")
    d.edge("rep", "save", [(1270, 282), (1340, 282), (1340, 832), (1270, 832)], labels=[("«Экспорт\nв CSV»", 1332, 742, "end")])
    d.edge("jour", "save", [(1270, 412), (1340, 412)], arrow=False)

    # ---- день 7
    x7 = 1530
    scr("mods", x7, 250, "Модули", "запуск, остановка, проверка", 4)
    scr("cli", x7, 380, "Клиенты", "GET /api/clients", 4)
    scr("ccard", x7, 500, "Карточка клиента", "клиент + его заявки", 4)
    scr("newreq", x7, 640, "Новая заявка", "POST /api/requests", 4)
    msg("res", x7, 760, "Результат: 201 создана /\n422 нет клиента / 503")
    scr("itest", x7, 880, "Тесты интеграции", "10 сквозных сценариев", 4)
    d.box("api", x7, 990, bw, 56, "Контракт API", fill=F[4], stroke=S[4], fs=13, bold=True)
    d.edge("cli", "ccard", [(1640, 444), (1640, 500)], labels=[("щелчок\nпо клиенту", 1648, 466, "start")])
    d.edge("newreq", "res", [(1640, 704), (1640, 760)], labels=[("«Создать\nзаявку»", 1648, 726, "start")])
    d.edge("mods", "cli", [(1530, 282), (1470, 282), (1470, 412), (1530, 412)],
           labels=[("модули\nзапущены", 1478, 330, "start")])
    d.edge("mods", "newreq", [(1530, 282), (1470, 282), (1470, 672), (1530, 672)])
    return d


# ---------------------------------------------------------------- день 6: развёртывание
def den6_deployment():
    d = Diagram("den6_deployment", 1700, 860, "Схема развёртывания приложения «Киноточка» (день 6)")
    d.box("pc", 40, 80, 1040, 740, "", fill="none", stroke="#94A3B8", rx=12)
    d.text(60, 104, "Компьютер пользователя: Windows 10/11, .NET Framework 4.7.2", 14, bold=True)
    d.box("srv", 1130, 80, 530, 740, "", fill="none", stroke="#94A3B8", rx=12)
    d.text(1150, 104, "SQL Server (LocalDB, Express или полный)", 14, bold=True)

    d.box("exe", 90, 160, 380, 110, "Den5_7.exe", fill=F[0], stroke=S[0], fs=16, bold=True,
          sub="окно: дни 5, 6, 7 (WinForms)")
    d.box("cfg", 90, 340, 300, 90, "Den5_7.exe.config", fill="#FFFFFF", stroke=S[0], fs=14, bold=True,
          sub="подключение «Киноточка», порты")
    d.box("res", 90, 500, 300, 80, "Fonts\\, Resources\\", fill="#FFFFFF", stroke="#94A3B8", fs=14, bold=True,
          sub="рядом с exe: шрифты, логотип")
    d.box("csv", 90, 650, 300, 80, "Файлы *.csv", fill="#FFFFFF", stroke=S[3], fs=14, bold=True,
          sub="экспорт (UTF-8, «;»)")
    d.box("mc", 660, 160, 380, 110, "Модуль «Клиенты»", fill=F[4], stroke=S[4], fs=15, bold=True,
          sub="Den5_7.exe --module=clients, порт 5101")
    d.box("mr", 660, 560, 380, 110, "Модуль «Заявки»", fill=F[4], stroke=S[4], fs=15, bold=True,
          sub="Den5_7.exe --module=requests, порт 5102")
    d.box("db", 1180, 160, 430, 130, "БД «Киноточка»", fill=F[1], stroke=S[1], fs=16, bold=True,
          sub="16 таблиц, 3 представления")
    d.box("sql", 1180, 370, 430, 150,
          "Скрипты создания (по порядку):\n01_create.sql, 02_data.sql (день 4)\n04_den5.sql, 05_queries.sql (день 5)",
          fill="#FFFFFF", stroke=S[1], fs=13)
    d.box("cmd", 1180, 600, 430, 120, "sqlcmd -S \"(localdb)\\MSSQLLocalDB\" -E\n-b -f 65001 -i <файл>", fill="#FFFFFF",
          stroke="#94A3B8", fs=13)

    d.edge("exe", "cfg", [(240, 270), (240, 340)], labels=[("читает при запуске", 250, 310, "start")])
    d.edge("exe", "csv", [(90, 250), (65, 250), (65, 690), (90, 690)], labels=[("сохраняет", 73, 620, "start")])
    d.edge("exe", "db", [(420, 160), (420, 135), (1100, 135), (1100, 210), (1180, 210)],
           labels=[("ADO.NET (SqlClient): запросы с параметрами", 760, 127, "middle")])
    d.edge("exe", "mc", [(470, 215), (660, 215)], labels=[("HTTP, JSON", 565, 205, "middle")])
    d.edge("exe", "mr", [(470, 250), (600, 250), (600, 615), (660, 615)],
           labels=[("запуск процессов (--parent):\nмодули закрываются\nвместе с окном", 592, 460, "end")])
    d.edge("mc", "mr", [(720, 270), (720, 560)], labels=[("GET /api/requests?clientId", 728, 380, "start")])
    d.edge("mr", "mc", [(980, 560), (980, 270)], labels=[("GET /api/clients/{id}", 972, 440, "end")])
    d.edge("sql", "db", [(1395, 370), (1395, 290)], labels=[("создают", 1403, 335, "start")])
    d.edge("cmd", "sql", [(1395, 600), (1395, 520)], labels=[("выполняет", 1403, 565, "start")])
    d.text(60, 790, "Модули — тот же Den5_7.exe без окна; права администратора Windows не нужны (адрес 127.0.0.1).", 12,
           color=MUTED)
    return d


# ---------------------------------------------------------------- день 7: интеграция
def den7_integration():
    d = Diagram("den7_integration", 1760, 900, "Интеграция модулей «Клиенты» и «Заявки» через REST API (день 7)")
    d.box("ui", 640, 90, 480, 110, "Окно Den5_7.exe — вкладки дня 7", fill=F[0], stroke=S[0], fs=16, bold=True,
          sub="HTTP-клиент: запуск модулей, клиенты, новая заявка, тесты")
    d.box("mc", 60, 340, 560, 330,
          "GET  /api/health\nGET  /api/clients\nGET  /api/clients/{id}\nGET  /api/clients/{id}/card\nPOST /api/clients",
          fill="#FFFFFF", stroke=S[4], fs=14)
    d.box("mr", 1140, 340, 560, 330,
          "GET  /api/health\nGET  /api/requests[?clientId=]\nGET  /api/requests/{id}\nPOST /api/requests",
          fill="#FFFFFF", stroke=S[4], fs=14)
    d.box("mch", 60, 300, 560, 46, "Модуль «Клиенты» — http://127.0.0.1:5101", fill=F[4], stroke=S[4], fs=15, bold=True)
    d.box("mrh", 1140, 300, 560, 46, "Модуль «Заявки» — http://127.0.0.1:5102", fill=F[4], stroke=S[4], fs=15, bold=True)
    d.box("sc", 160, 720, 360, 70, "Своё хранилище клиентов\n(в памяти процесса)", fill=GROUP_FILL, stroke="#94A3B8", fs=13)
    d.box("sr", 1240, 720, 360, 70, "Своё хранилище заявок\n(в памяти процесса)", fill=GROUP_FILL, stroke="#94A3B8", fs=13)
    d.edge("mc", "sc", [(340, 670), (340, 720)], arrow=False)
    d.edge("mr", "sr", [(1420, 670), (1420, 720)], arrow=False)

    d.edge("ui", "mch", [(700, 200), (700, 250), (340, 250), (340, 300)],
           labels=[("GET /api/clients, GET /api/clients/{id}/card", 520, 240, "middle")])
    d.edge("ui", "mrh", [(1060, 200), (1060, 250), (1420, 250), (1420, 300)],
           labels=[("POST /api/requests {topic, description, clientId}", 1240, 240, "middle")])
    d.edge("mr", "mc", [(1140, 430), (620, 430)],
           labels=[("1) перед созданием заявки: GET /api/clients/{clientId}\n200 — клиент есть → 201; 404 → 422; нет ответа → 503",
                    880, 408, "middle")])
    d.edge("mc", "mr", [(620, 580), (1140, 580)],
           labels=[("2) карточка клиента: GET /api/requests?clientId={id}\nнет ответа → 503 «Модуль «Заявки» недоступен»",
                    880, 558, "middle")])
    d.text(60, 840, "Обмен — JSON в UTF-8; ошибки — {\"error\": текст, \"status\": код}. Каждый модуль — отдельный процесс "
                    "на своём порту; при отказе одного другой продолжает работать.", 12, color=MUTED)
    return d


# ---------------------------------------------------------------- день 7: последовательность
def den7_sequence():
    d = Diagram("den7_sequence", 1700, 1060, "Диаграмма последовательности: создание заявки (день 7)")
    parts = [("u", "Пользователь", 150), ("ui", "Окно: вкладка\n«Новая заявка»", 560),
             ("rq", "Модуль «Заявки»\n:5102", 1000), ("cl", "Модуль «Клиенты»\n:5101", 1450)]
    top, bottom = 80, 1000
    for nid, name, x in parts:
        d.box(nid, x - 120, top, 240, 64, name, fill=F[0] if nid in ("u", "ui") else F[4],
              stroke=S[0] if nid in ("u", "ui") else S[4], fs=14, bold=True)
        d.edge(nid, None, [(x, top + 64), (x, bottom)], arrow=False)
    X = {nid: x for nid, _, x in parts}

    def msg(a, b, y, text, anchor="middle"):
        xa, xb = X[a], X[b]
        d.edge(None, None, [(xa, y), (xb, y)], labels=[(text, (xa + xb) / 2, y - 10, anchor)])

    def self_note(who, y, text):
        x = X[who]
        d.edge(None, None, [(x, y), (x + 60, y), (x + 60, y + 30), (x, y + 30)],
               labels=[(text, x + 70, y + 20, "start")])

    msg("u", "ui", 200, "выбирает клиента, вводит тему и описание, «Создать заявку»")
    self_note("ui", 230, "проверка: тема не пустая")
    msg("ui", "rq", 320, "POST /api/requests {topic, description, clientId}")
    self_note("rq", 350, "JSON и поля верны? иначе → 400")
    msg("rq", "cl", 440, "GET /api/clients/{clientId}")
    # alt-фрейм
    d.box("alt", 300, 480, 1340, 430, "", fill="none", stroke="#94A3B8", rx=0)
    d.box("alt_t", 300, 480, 70, 26, "alt", fill="#E2E8F0", stroke="#94A3B8", fs=12, bold=True, rx=0)
    d.text(575, 494, "[клиент найден]", 12, color=MUTED)
    msg("cl", "rq", 540, "200 {id, fullName, phone, email}")
    self_note("rq", 560, "сохранить заявку с именем клиента")
    msg("rq", "ui", 640, "201 {id, topic, clientId, clientName, createdAt}")
    d.edge(None, None, [(300, 670), (1640, 670)], arrow=False)
    d.text(575, 690, "[клиента нет (404)]", 12, color=MUTED)
    msg("cl", "rq", 730, "404 {error}")
    msg("rq", "ui", 770, "422 «Клиент с id = … не найден: заявка не создана»")
    d.edge(None, None, [(300, 800), (1640, 800)], arrow=False)
    d.text(575, 820, "[модуль «Клиенты» не ответил за 3 с]", 12, color=MUTED)
    msg("rq", "ui", 880, "503 «Модуль «Клиенты» недоступен: заявка не создана»")
    msg("ui", "u", 960, "сообщение с результатом; при 201 — номер новой заявки")
    return d


if __name__ == "__main__":
    os.makedirs(g.OUT, exist_ok=True)
    for build in (den5_idef0_a0, den5_screen_map, den6_deployment, den7_integration, den7_sequence):
        g.build(build())
