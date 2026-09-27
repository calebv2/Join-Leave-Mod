import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import unittest
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, HTTPServer

spec = importlib.util.spec_from_file_location('status_monitor', Path(__file__).resolve().parents[1] / 'status_monitor.py')
monitor = importlib.util.module_from_spec(spec)
spec.loader.exec_module(monitor)


class MonitorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name) / 'heartbeat.json'
        self.now = datetime(2026, 9, 26, 12, 0, 0, tzinfo=timezone.utc)
        self.snapshot = {
            'enabled': True, 'updated_at': '2026-09-26T11:56:00.0000000Z', 'online': True,
            'message_id': '123',
            'offline_payload': {'embeds': [{'title': 'Test', 'timestamp': 'old', 'fields': [{'name': 'Server', 'value': 'Offline'}]}]},
        }
        self.save()

    def save(self):
        self.path.write_text(json.dumps(self.snapshot))

    def update(self):
        return monitor.pending_update(self.path, 180, self.now, None)

    def tearDown(self):
        self.temp.cleanup()

    def test_fresh_heartbeat_is_not_offline(self):
        self.snapshot['updated_at'] = '2026-09-26T11:59:50Z'
        self.save()
        self.assertIsNone(self.update())

    def test_stale_heartbeat_posts_once_and_respects_hidden_timestamp(self):
        update = self.update()
        self.assertIsNotNone(update)
        key, message_id, payload = update
        self.assertEqual(message_id, '123')
        self.assertEqual(payload['embeds'][0]['timestamp'], '2026-09-26T12:00:00Z')
        self.assertIsNone(monitor.pending_update(self.path, 180, self.now, key))
        del self.snapshot['offline_payload']['embeds'][0]['timestamp']
        self.save()
        self.assertNotIn('timestamp', self.update()[2]['embeds'][0])

    def test_crash_activity_age_starts_at_detection(self):
        self.snapshot['offline_activity_marker'] = '<t:1790423760:R>'
        self.snapshot['offline_payload']['embeds'][0]['description'] = '**Latest activity**\nServer stopped responding • <t:1790423760:R>'
        self.save()
        self.assertEqual(self.update()[2]['embeds'][0]['description'], '**Latest activity**\nServer stopped responding • <t:1790424000:R>')

    def test_hidden_activity_age_stays_hidden_on_crash(self):
        self.snapshot['offline_activity_marker'] = '<t:1790423760:R>'
        self.snapshot['offline_payload']['embeds'][0]['description'] = '**Latest activity**\nServer stopped responding'
        self.save()
        self.assertNotIn('<t:', self.update()[2]['embeds'][0]['description'])

    def test_clean_shutdown_retains_original_activity_age(self):
        self.snapshot['offline_activity_marker'] = ''
        self.snapshot['offline_payload']['embeds'][0]['description'] = '**Latest activity**\nServer has stopped • <t:1790423760:R>'
        self.save()
        self.assertTrue(self.update()[2]['embeds'][0]['description'].endswith('<t:1790423760:R>'))

    def test_hidden_embed_can_be_cleared_without_new_sections(self):
        self.snapshot['offline_payload'] = {'embeds': []}
        self.save()
        self.assertEqual(self.update()[2], {'embeds': []})

    def test_missing_or_partial_heartbeat_is_ignored(self):
        self.path.write_text('{broken')
        self.assertIsNone(self.update())
        self.path.unlink()
        self.assertIsNone(self.update())

    def test_disabled_monitor_does_not_edit_old_embed(self):
        self.snapshot['enabled'] = False
        self.save()
        self.assertIsNone(self.update())

    def test_empty_message_id_is_ignored(self):
        self.snapshot['message_id'] = ''
        self.save()
        self.assertIsNone(self.update())

    def test_patch_preserves_existing_message_and_retries_failed_updates(self):
        received = []
        class Handler(BaseHTTPRequestHandler):
            def do_PATCH(self):
                received.append((self.path, json.loads(self.rfile.read(int(self.headers['Content-Length'])))))
                self.send_response(204)
                self.end_headers()
            def log_message(self, *args):
                pass
        server = HTTPServer(('127.0.0.1', 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            key, message_id, payload = self.update()
            monitor.send_update('http://127.0.0.1:%s/webhook' % server.server_port, message_id, payload)
            self.assertEqual(received, [('/webhook/messages/123', payload)])
            self.assertIsNotNone(self.update())
        finally:
            server.shutdown()
            server.server_close()
            thread.join()


if __name__ == '__main__':
    unittest.main()
