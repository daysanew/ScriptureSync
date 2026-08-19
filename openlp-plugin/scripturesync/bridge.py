"""Localhost-only HTTP transport for the ScriptureSync OpenLP plugin."""

import json
import logging
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit


log = logging.getLogger(__name__)


class BridgeRequest:
    """One HTTP request waiting for completion by OpenLP's Qt thread."""

    def __init__(self, operation, payload):
        self.operation = operation
        self.payload = payload
        self.response = None
        self.error = None
        self.completed = threading.Event()

    def succeed(self, response):
        self.response = response
        self.completed.set()

    def fail(self, message):
        self.error = message
        self.completed.set()


class LocalBridgeServer:
    """Serve a small JSON API that is inaccessible off this computer."""

    host = '127.0.0.1'
    port = 4317
    max_request_bytes = 64 * 1024
    request_timeout_seconds = 90

    def __init__(self, submit_request):
        self.submit_request = submit_request
        self.http_server = None
        self.server_thread = None

    def start(self):
        if self.http_server is not None:
            return

        bridge = self

        class RequestHandler(BaseHTTPRequestHandler):
            server_version = 'ScriptureSyncBridge/0.1'

            def do_GET(self):
                path = urlsplit(self.path).path
                if path == '/v1/health':
                    self._write_json(200, {
                        'status': 'ready',
                        'bridge_version': '0.1'
                    })
                elif path == '/v1/bibles':
                    self._submit('list_bibles', {})
                else:
                    self._write_json(404, {'error': 'Unknown ScriptureSync endpoint.'})

            def do_POST(self):
                path = urlsplit(self.path).path
                operations = {
                    '/v1/scriptures/search': 'search_scripture',
                    '/v1/scriptures/add': 'add_scripture'
                }
                operation = operations.get(path)
                if operation is None:
                    self._write_json(404, {'error': 'Unknown ScriptureSync endpoint.'})
                    return

                try:
                    content_length = int(self.headers.get('Content-Length', '0'))
                    if content_length <= 0 or content_length > bridge.max_request_bytes:
                        raise ValueError('The request body size is invalid.')
                    payload = json.loads(self.rfile.read(content_length).decode('utf-8'))
                    if not isinstance(payload, dict):
                        raise ValueError('The request body must be a JSON object.')
                    bible = payload.get('bible')
                    reference = payload.get('reference')
                    if not isinstance(bible, str) or not bible.strip():
                        raise ValueError('A Bible name is required.')
                    if not isinstance(reference, str) or not reference.strip():
                        raise ValueError('A scripture reference is required.')
                    payload = {'bible': bible.strip(), 'reference': reference.strip()}
                except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
                    self._write_json(400, {'error': str(error)})
                    return
                self._submit(operation, payload)

            def _submit(self, operation, payload):
                request = BridgeRequest(operation, payload)
                bridge.submit_request(request)
                if not request.completed.wait(bridge.request_timeout_seconds):
                    self._write_json(504, {'error': 'OpenLP did not finish the request in time.'})
                elif request.error:
                    self._write_json(422, {'error': request.error})
                else:
                    self._write_json(200, request.response)

            def _write_json(self, status, value):
                body = json.dumps(value).encode('utf-8')
                self.send_response(status)
                self.send_header('Content-Type', 'application/json; charset=utf-8')
                self.send_header('Content-Length', str(len(body)))
                self.send_header('Cache-Control', 'no-store')
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, message_format, *args):
                log.debug('Bridge HTTP: ' + message_format, *args)

        self.http_server = ThreadingHTTPServer((self.host, self.port), RequestHandler)
        self.http_server.daemon_threads = True
        self.server_thread = threading.Thread(
            target=self.http_server.serve_forever,
            name='ScriptureSyncBridge',
            daemon=True)
        self.server_thread.start()
        log.info('ScriptureSync bridge listening on http://%s:%s', self.host, self.port)

    def stop(self):
        server = self.http_server
        if server is None:
            return
        self.http_server = None
        server.shutdown()
        server.server_close()
        if self.server_thread is not None:
            self.server_thread.join(timeout=2)
        self.server_thread = None
        log.info('ScriptureSync bridge stopped')
