"""ER-диаграмма базы «Киноточка» для дня 5: 14 таблиц дня 4 + ЖурналДействий и ПрофилиЗрителей (скрипт sql/04_den5.sql).

Отрисовка — тем же генератором, что и в дне 4 (UML_Den4/tools/build_er.py), столбцы берутся из
tools/schema.json — выгрузки из созданной базы (tools/schema_dump.sql), поэтому диаграмма совпадает с базой.
Результат: docs/diagrams/den5_er.drawio, .svg, .png, .pdf.
Запуск:  python tools/build_er5.py   (нужен пакет cairosvg)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "UML_Den4", "tools"))
import build_er as er  # noqa: E402
import cairosvg  # noqa: E402

OUT = os.path.join(HERE, "..", "docs", "diagrams")

er.HERE = HERE  # schema.json дня 5
er.M = 70        # слева место для линии связи 1:1
er.TABLES["ЖурналДействий"] = ("журнал действий (день 5)", "users")
er.TABLES["ПрофилиЗрителей"] = ("профиль зрителя, связь 1:1 (день 5)", "users")
er.GRID = [
    ["Роли", "Отзывы", "Фильмы", "Жанры"],
    ["Пользователи", "Заказы", "Сеансы", "Залы"],
    ["Промокоды", "Платежи", "Билеты", "Места"],
    ["ПрофилиЗрителей", "ЖурналДействий", "СтатусыЗаказа", "ТипыМест"],
]
base_relations = er.relations


def relations(b):
    # связь «СтатусыЗаказа — Заказы» прокладывается заново: таблица статусов переехала в третий столбец
    rel = [r for r in base_relations(b) if (r[0], r[1]) != ("СтатусыЗаказа", "Заказы")]
    W = er.W
    g01 = b["Пользователи"]["x"] + W
    g12 = b["Заказы"]["x"] + W
    u, j, p, st, z = (b[t] for t in ("Пользователи", "ЖурналДействий", "ПрофилиЗрителей", "СтатусыЗаказа", "Заказы"))
    rel.append(("СтатусыЗаказа", "Заказы",
                [(st["x"], st["y"] + 40), (g12 + 85, st["y"] + 40), (g12 + 85, z["y"] + 150), (z["x"] + W, z["y"] + 150)],
                "1", "0N", None))
    # журнал: линия идёт ниже входов в «Заказы» слева (+150, +186)
    rel.append(("Пользователи", "ЖурналДействий",
                [(u["x"] + W, u["y"] + 205), (g01 + 95, u["y"] + 205), (g01 + 95, j["y"] + 80), (j["x"], j["y"] + 80)],
                "01", "0N", None))
    # профиль: 1:1 — у пользователя ноль или один профиль, у профиля ровно один пользователь
    lane = er.M - 40
    rel.append(("Пользователи", "ПрофилиЗрителей",
                [(u["x"], u["y"] + 190), (lane, u["y"] + 190), (lane, p["y"] + 40), (p["x"], p["y"] + 40)],
                "1", "01", None))
    return rel


def main():
    cols = er.load()
    assert set(cols) == set(er.TABLES), set(cols) ^ set(er.TABLES)
    b = er.layout(cols)
    rel = relations(b)
    fks = sorted((r["fk"], r["tbl"]) for t in cols for r in cols[t] if r.get("fk"))
    assert sorted((p, c) for p, c, *_ in rel) == fks, "связи на диаграмме не совпадают с внешними ключами базы"
    w = er.M * 2 + 4 * er.W + 3 * er.GAP_X
    h = int(max(bx["y"] + bx["h"] for bx in b.values()) + 80)
    svg = er.render_svg(cols, b, rel, w, h).replace(
        "ER-диаграмма базы данных «Киноточка»</text>",
        "ER-диаграмма базы данных «Киноточка» (16 таблиц, день 5)</text>", 1)
    os.makedirs(OUT, exist_ok=True)
    base = os.path.join(OUT, "den5_er")
    open(base + ".svg", "w", encoding="utf-8").write(svg)
    open(base + ".drawio", "w", encoding="utf-8").write(
        er.render_drawio(cols, b, rel, w, h).replace(
            'value="ER-диаграмма базы данных «Киноточка»"',
            'value="ER-диаграмма базы данных «Киноточка» (16 таблиц, день 5)"', 1))
    cairosvg.svg2png(bytestring=svg.encode(), write_to=base + ".png", scale=2)
    cairosvg.svg2pdf(bytestring=svg.encode(), write_to=base + ".pdf")
    print("ok: den5_er", w, h, "связей:", len(rel))


if __name__ == "__main__":
    main()
