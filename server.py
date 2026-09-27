#==============================================================================
# ** join/leave Alert **
# ==============================================================================
from .logger import CaveLevelAlertLogger
import json
import os
import threading
import time

from server.core.data_store import CONSOLE_TOKEN_FILE
from tavern_shared.ws_console_client import WsConsoleClient

logger = CaveLevelAlertLogger()
_stop_event = threading.Event()

global _ws_client
_ws_client = WsConsoleClient()

logger.init()

def on_shutdown():
    _stop_event.set()
    global _ws_client
    if _ws_client is not None:
        _ws_client.disconnect()
        _ws_client = None
    logger._log("join/leave Alert Logger shutting down.")

def on_line(line):
    try:
        logger._log(f"[join/leave Alert] received line: {line}")

        payload = line
        if isinstance(line, str):
            text = line.strip()
            if text.startswith("{") or text.startswith("["):
                try:
                    payload = json.loads(text)
                except Exception:
                    payload = line

        event_type = None
        event_data = None

        if isinstance(payload, dict):
            event_type = payload.get("type") or payload.get("Type")
            eventType = payload.get("eventType") or payload.get("EventType")
            event_data = payload.get("data") if "data" in payload else payload.get("Data")
        else:
            event_type = getattr(payload, "type", None)
            event_data = getattr(payload, "data", None)

        if event_type == "Subscription":

            if _ws_client is not None:

                if eventType == "PlayerJoined": # ignore

                    if event_data is not None:

                        Username = event_data.get("user").get("username")
                        logger._log(f"[join/leave Alert] Player joined: {Username}")
                        _ws_client.send(f'player message * "{Username} has joined the game!"')

                elif eventType == "PlayerLeft": # ignore
                    if event_data is not None:

                        Username = event_data.get("user").get("username")
                        logger._log(f"[join/leave Alert] Player left: {Username}")
                        _ws_client.send(f'player message * "{Username} has left the game!" 2.5')
    except Exception as e:
        logger._log(f"[join/leave Alert] on_line handler error: {e}")


def on_disc(reason=""):
    if reason:
        logger._log(f"[join/leave Alert] disconnected from console: {reason}")
    else:
        logger._log("[join/leave Alert] disconnected from console.")


def _wait_for_token(check_every=1.0):
    while not _stop_event.is_set():
        try:
            if os.path.isfile(CONSOLE_TOKEN_FILE):
                token = open(CONSOLE_TOKEN_FILE, "r").read().strip()
                if token:
                    return token
        except Exception:
            pass
        time.sleep(check_every)
    return ""

def startup():
    global _ws_client

    token = _wait_for_token()
    if not token:
        logger._log("[join/leave Alert] startup canceled before token became available.")
        return

    logger._log(f"Console token: {token}")
    logger._new_line()
    logger._log("[join/leave Alert] attempting to subscribe to PlayerJoined and PlayerLeft events.")

    if _ws_client is None:
        _ws_client = WsConsoleClient()

    while not _stop_event.is_set():
        client = _ws_client
        if client is None:
            logger._log("[join/leave Alert] websocket client unavailable; stopping startup loop.")
            return
        logger._log("[join/leave Alert] attempting to connect to console...")
        success, msg = client.connect("127.0.0.1", token, on_line=on_line, on_disc=on_disc)
        logger._log(f"[join/leave Alert] connect attempt result: {success}, message: {msg}")
        if not success:
            logger._log(f"[join/leave Alert] console not ready yet ({msg}); retrying in 2s...")
            time.sleep(2.0)
            continue
        if success:
            logger._log(f"[join/leave Alert] connected to console: {msg}")

            client.send("websocket subscribe PlayerJoined")
            client.send("websocket subscribe PlayerLeft")




            logger._log("[join/leave Alert] sent subscription request.")
            return

        logger._log(f"[join/leave Alert] console not ready yet ({msg}); retrying in 2s...")
        time.sleep(2.0)


threading.Thread(target=startup, daemon=True).start()
