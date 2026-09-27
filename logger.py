# ===================================
# Logger for the Cave Level Alert addon
# ===================================


import os
import threading


class CaveLevelAlertLogger:
    
    def __init__(self):
        self.init()

    def init(self):
        self._log_file = "join_leave_alert.log"
        self._log_lock = threading.Lock()
        self._log("Cave Level Alert Logger initialized.") 
       
        self.path = self.set_path()
        

    def _log(self,message):
        with self._log_lock:
            with open(self._log_file, "a", encoding="utf-8") as log_file:
                log_file.write(f"{message}\n")

    def _new_line(self):
        with self._log_lock:
            with open(self._log_file, "a", encoding="utf-8") as log_file:
                log_file.write("==================================\n")

    def set_path(self):
        base = os.environ.get("APPDATA", os.path.join(os.path.expanduser("~"), "AppData", "Roaming"))
        path = os.path.join(base, "TheModdingTavern")
        try:
            os.makedirs(path, exist_ok=True)
        except Exception:
            pass
        return os.path.join(path,self._log_file)

    
