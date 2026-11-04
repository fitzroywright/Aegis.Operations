#!/usr/bin/env python3
"""Fixed, read-only staff incident journal inspection. No application/configuration writes."""
import datetime
import json
import os
import re
import subprocess
import sys


def relevant(message):
    return any(value in message for value in ("/Administration/Staff", "/Administration/AddStaff", "StaffModel", "StaffAdministrationService", "Pages_Administration_Staff", "Pages_Administration_AddStaff"))


def redact(message):
    message = re.sub(r"(?i)Bearer\s+[A-Za-z0-9._~+/-]+=*", "Bearer [redacted]", message)
    message = re.sub(r"(?i)((?:password|client_secret|secret_id|client_token|access_token|refresh_token|id_token|authorization|selection|code|state|nonce)\s*[=:]\s*)(?:\"[^\"]*\"|'[^']*'|[^\s;&]+)", r"\1[redacted]", message)
    message = re.sub(r"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[redacted-jwt]", message)
    message = re.sub(r"(https?://)[^/@\s:]+:[^/@\s]+@", r"\1[redacted]@", message)
    return message[:18000]


def inspect(lines):
    for line in lines:
        try:
            event = json.loads(line)
        except ValueError:
            continue
        message = event.get("MESSAGE", "")
        if not isinstance(message, str) or not relevant(message):
            continue
        timestamp = event.get("__REALTIME_TIMESTAMP", "0")
        when = datetime.datetime.fromtimestamp(int(timestamp) / 1_000_000, datetime.timezone.utc).isoformat()
        yield {"time": when, "message": redact(message)}


def main():
    if len(sys.argv) != 1 or os.geteuid() != 0:
        raise RuntimeError("Use the approved fixed-scope deployment incident command.")
    result = subprocess.run(["/usr/bin/journalctl", "--unit=aegis-requestportal", "--since=6 hours ago",
                             "--no-pager", "--quiet", "--output=json", "--lines=5000"], capture_output=True, text=True, timeout=20)
    if result.returncode:
        raise RuntimeError("The Request Portal service journal could not be read.")
    events = list(inspect(result.stdout.splitlines()))
    for event in events[-40:]:
        print(json.dumps(event))
    print(json.dumps({"service": "aegis-requestportal", "matchingEvents": len(events), "readOnly": True}))


if __name__ == "__main__":
    try:
        main()
    except Exception as exception:
        print("Incident inspection failed: " + type(exception).__name__)
        sys.exit(1)
