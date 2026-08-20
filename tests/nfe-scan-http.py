#!/usr/bin/env python3
"""Serve the repo and wait for the browser decode self-test to POST a report."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse, parse_qs
import sys

ROOT = Path("/workspace")
REPORT = Path("/tmp/nfe-scan-report.txt")
if REPORT.exists():
    REPORT.unlink()


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=str(ROOT), **k)

    def log_message(self, fmt, *args):
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/report":
            qs = parse_qs(parsed.query)
            body = unquote(qs.get("s", [""])[0])
            REPORT.write_text(body + "\n", encoding="utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(b"ok")
            return
        return super().do_GET()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8766
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
