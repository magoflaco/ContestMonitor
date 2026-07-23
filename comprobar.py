#    python monitor_concursos.py            una sola vez
#    python monitor_concursos.py --loop     revisa cada CHECK_INTERVAL segundos

import requests
from bs4 import BeautifulSoup
import json
import os
import sys
import time
from datetime import datetime


URL = "https://registro.redprogramacioncompetitiva.com/contests"
WEBHOOK_URL = "https://discord.com/api/webhooks/142081405349...."
STATE_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "seen_contests.json")
CHECK_INTERVAL = 300  # segundos entre revisiones 
HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "Accept-Language": "es-ES,es;q=0.9",
}


def load_seen():
    if os.path.exists(STATE_FILE):
        try:
            with open(STATE_FILE, "r", encoding="utf-8") as f:
                return set(json.load(f))
        except (json.JSONDecodeError, OSError):
            return set()
    return set()


def save_seen(seen):
    with open(STATE_FILE, "w", encoding="utf-8") as f:
        json.dump(sorted(seen), f, ensure_ascii=False, indent=2)


def get_upcoming_contests():
    
    resp = requests.get(URL, headers=HEADERS, timeout=15)
    resp.raise_for_status()
    soup = BeautifulSoup(resp.text, "html.parser")

    heading = soup.find(
        lambda tag: tag.name in ("h1", "h2", "h3", "h4")
        and "Upcoming contests" in tag.get_text()
    )
    if not heading:
        return []

    table = heading.find_next("table")
    if not table:
        return []

    contests = []
    for row in table.find_all("tr"):
        cells = row.find_all("td")  
        if not cells:
            continue
        name = cells[0].get_text(strip=True)
        start = cells[1].get_text(strip=True) if len(cells) > 1 else ""
        end = cells[2].get_text(strip=True) if len(cells) > 2 else ""
        if name:
            contests.append({"name": name, "start": start, "end": end})
    return contests


def notify_discord(contest):
    content = (
        "**¡Nuevo concurso disponible!**\n"
        f"**Nombre:** {contest['name']}\n"
        f"**Inicio:** {contest['start'] or 'N/D'}\n"
        f"**Fin de inscripciones:** {contest['end'] or 'N/D'}\n"
        f"{URL}"
    )
    try:
        r = requests.post(WEBHOOK_URL, json={"content": content}, timeout=10)
        r.raise_for_status()
    except requests.RequestException as e:
        print(f"[{datetime.now()}] Error enviando aviso a Discord: {e}")


def notify_error(message):
    try:
        requests.post(WEBHOOK_URL, json={"content": f"Monitor de concursos: {message}"}, timeout=10)
    except requests.RequestException:
        pass


def check_once():
    seen = load_seen()
    contests = get_upcoming_contests()
    current_names = {c["name"] for c in contests}

    nuevos = [c for c in contests if c["name"] not in seen]

    if nuevos:
        for c in nuevos:
            print(f"[{datetime.now()}] Nuevo concurso: {c['name']}")
            notify_discord(c)

        seen.update(current_names)
        save_seen(seen)
    else:
        print(f"[{datetime.now()}] Sin concursos nuevos ({len(contests)} en la lista).")


def main():
    loop = "--loop" in sys.argv
    if not loop:
        check_once()
        return

    print(f"Monitoreando {URL} cada {CHECK_INTERVAL}s. Ctrl+C para detener.")
    fallos_seguidos = 0
    while True:
        try:
            check_once()
            fallos_seguidos = 0
        except requests.RequestException as e:
            fallos_seguidos += 1
            print(f"[{datetime.now()}] Error al consultar la página: {e}")
            if fallos_seguidos == 5:
                notify_error("no se pudo acceder a la página en los últimos 5 intentos.")
        except KeyboardInterrupt:
            print("\nDetenido por el usuario.")
            break
        time.sleep(CHECK_INTERVAL)


if __name__ == "__main__":
    main()
